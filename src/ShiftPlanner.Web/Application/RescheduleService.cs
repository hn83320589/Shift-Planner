using Microsoft.EntityFrameworkCore;

using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;
using ShiftPlanner.Data;
using ShiftPlanner.Scheduling;

namespace ShiftPlanner.Web.Application;

/// <summary>產生重排提案的結果：成功時有提案，失敗時有無解原因。</summary>
public sealed record ProposalOutcome(RescheduleProposal? Proposal, IReadOnlyList<InfeasibilityReason> Reasons);

/// <summary>
/// 已發布班表的臨時重排流程：登記請假 → 產生提案（列出所有更動）→ 主管確認後套用。
/// 套用前班表完全不變。
/// </summary>
public sealed class RescheduleService(
    ShiftPlannerDbContext db,
    PlanningDataLoader loader,
    Rescheduler rescheduler,
    ScheduleValidator validator,
    TimeProvider time)
{
    public async Task<ProposalOutcome> ProposeAsync(
        int year, int month, int employeeId, DateOnly leaveStart, DateOnly leaveEnd, CancellationToken cancellationToken = default)
    {
        var schedule = await FindPublishedAsync(year, month, cancellationToken);
        if (leaveEnd < leaveStart)
        {
            throw new ArgumentException("請假結束日不可早於開始日");
        }

        if (!schedule.Period.Contains(leaveStart))
        {
            throw new ArgumentException($"請假開始日 {leaveStart:yyyy-MM-dd} 不在 {year}/{month} 班表內");
        }

        var today = TaipeiTime.Today(time);
        if (leaveStart < today)
        {
            throw new ArgumentException($"不能重排已經過去的日期（今天是 {today:yyyy-MM-dd}）");
        }

        if (!await db.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken))
        {
            throw new ArgumentException($"員工 Id {employeeId} 不存在");
        }

        var data = await loader.LoadAsync(year, month, cancellationToken);
        var result = rescheduler.Reschedule(new RescheduleRequest
        {
            Month = schedule.Period,
            Employees = data.Employees,
            Calendar = data.Calendar,
            Published = schedule.Assignments,
            FromDate = leaveStart,
            PreviousMonth = data.PreviousMonth,
            Leaves = [.. data.Leaves, new Leave(employeeId, leaveStart, leaveEnd)],
            Preferences = data.Preferences,
        });

        if (!result.Succeeded)
        {
            return new ProposalOutcome(null, result.Reasons);
        }

        var proposal = new RescheduleProposal(
            schedule.Id, schedule.Revision, employeeId, leaveStart, leaveEnd,
            [
                .. result.Removed.Select(a => new ProposedChange(ChangeKind.Removed, a.EmployeeId, a.Date, a.Shift)),
                .. result.Added.Select(a => new ProposedChange(ChangeKind.Added, a.EmployeeId, a.Date, a.Shift)),
            ],
            time.GetUtcNow());
        db.RescheduleProposals.Add(proposal);
        await db.SaveChangesAsync(cancellationToken);
        return new ProposalOutcome(proposal, []);
    }

    public Task<RescheduleProposal?> FindAsync(int proposalId, CancellationToken cancellationToken = default) =>
        db.RescheduleProposals.SingleOrDefaultAsync(p => p.Id == proposalId, cancellationToken);

    /// <summary>主管確認後套用：登記請假、更新班表、版本 +1。提案產生後班表若又變動則拒絕。</summary>
    public async Task ApplyAsync(int proposalId, CancellationToken cancellationToken = default)
    {
        var proposal = await FindAsync(proposalId, cancellationToken)
            ?? throw new ArgumentException($"重排提案 #{proposalId} 不存在");
        var schedule = await db.Schedules.SingleAsync(s => s.Id == proposal.ScheduleId, cancellationToken);
        if (schedule.Revision != proposal.BaseRevision)
        {
            throw new InvalidOperationException(
                $"提案產生後班表已更新為第 {schedule.Revision} 版（提案根據第 {proposal.BaseRevision} 版），請重新產生提案");
        }

        var data = await loader.LoadAsync(schedule.Year, schedule.Month, cancellationToken);
        var after = schedule.Assignments.Except(proposal.Removed).Concat(proposal.Added).ToList();
        var leaves = data.Leaves.Append(proposal.Leave).ToList();
        var validation = validator.Validate(
            new ScheduleSnapshot(data.Period, data.Employees, after, data.Calendar, data.PreviousMonth, leaves));

        schedule.ApplyRevision(proposal.Removed, proposal.Added, validation);
        db.Leaves.Add(proposal.Leave);
        proposal.MarkApplied();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DiscardAsync(int proposalId, CancellationToken cancellationToken = default)
    {
        var proposal = await FindAsync(proposalId, cancellationToken)
            ?? throw new ArgumentException($"重排提案 #{proposalId} 不存在");
        proposal.Discard();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Schedule> FindPublishedAsync(int year, int month, CancellationToken cancellationToken)
    {
        var schedule = await db.Schedules.SingleOrDefaultAsync(s => s.Year == year && s.Month == month, cancellationToken);
        if (schedule is not { IsPublished: true })
        {
            throw new InvalidOperationException($"{year}/{month} 班表尚未發布；草稿請直接修改，不需要臨時重排");
        }

        return schedule;
    }
}
