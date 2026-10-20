using Microsoft.EntityFrameworkCore;

using ShiftPlanner.Core.Calendar;
using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;
using ShiftPlanner.Data;
using ShiftPlanner.Scheduling;

namespace ShiftPlanner.Web.Application;

/// <summary>
/// 排某個月所需的全部資料，產生器、validator、報表都從這裡取得，確保輸入一致。
/// 跨月上下文只採用「已發布」的前後月份；PreviousMonthStatus 用來提示與強制依序發布。
/// </summary>
public sealed record PlanningData(
    DateRange Period,
    IReadOnlyList<Employee> Employees,
    StaffingCalendar Calendar,
    IReadOnlyList<Leave> Leaves,
    IReadOnlyList<OffPreference> Preferences,
    IReadOnlyList<Assignment> PreviousMonth,
    ScheduleStatus? PreviousMonthStatus = null,
    FollowingContext? NextMonth = null)
{
    public DateOnly PreviousMonthStart => Period.Start.AddMonths(-1);

    /// <summary>上個月已建立但還是草稿：跨月限制無法檢查，本月也不能發布。</summary>
    public bool PreviousMonthIsDraft => PreviousMonthStatus == ScheduleStatus.Draft;

    public ScheduleSnapshot Snapshot(IReadOnlyList<Assignment> assignments) =>
        new(Period, Employees, assignments, Calendar, PreviousMonth, Leaves, following: NextMonth);

    public GenerationRequest GenerationRequest() => new()
    {
        Horizon = Period,
        Employees = Employees,
        Calendar = Calendar,
        Context = PreviousMonth,
        Following = NextMonth,
        Leaves = Leaves,
        Preferences = Preferences,
    };
}

public sealed class PlanningDataLoader(ShiftPlannerDbContext db)
{
    public async Task<PlanningData> LoadAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var period = DateRange.ForMonth(year, month);
        var previous = period.Start.AddMonths(-1);
        var next = DateRange.ForMonth(period.Start.AddMonths(1).Year, period.Start.AddMonths(1).Month);

        var calendar = new StaffingCalendar(
            await db.Holidays.AsNoTracking().ToListAsync(cancellationToken),
            await db.StaffingRequirements.AsNoTracking().ToListAsync(cancellationToken),
            await db.SkillRequirements.AsNoTracking().ToListAsync(cancellationToken),
            await db.StaffingOverrides.AsNoTracking()
                .Where(o => o.Date >= period.Start && o.Date <= period.End).ToListAsync(cancellationToken));

        // 只有已發布的前後月份才是確定的上下文；草稿還會變動，不能拿來限制本月。
        var previousStatus = await StatusAsync(previous.Year, previous.Month, cancellationToken);
        var previousMonth = previousStatus == ScheduleStatus.Published
            ? await PublishedAssignmentsAsync(previous.Year, previous.Month, cancellationToken)
            : [];
        var nextMonth = await StatusAsync(next.Start.Year, next.Start.Month, cancellationToken) == ScheduleStatus.Published
            ? new FollowingContext(next.End, await PublishedAssignmentsAsync(next.Start.Year, next.Start.Month, cancellationToken))
            : null;

        return new PlanningData(
            period,
            await db.Employees.AsNoTracking().OrderBy(e => e.Id).ToListAsync(cancellationToken),
            calendar,
            await db.Leaves.AsNoTracking()
                .Where(l => l.Start <= period.End && l.End >= period.Start).ToListAsync(cancellationToken),
            await db.OffPreferences.AsNoTracking()
                .Where(p => p.Date >= period.Start && p.Date <= period.End).ToListAsync(cancellationToken),
            previousMonth,
            previousStatus,
            nextMonth);
    }

    public async Task<IReadOnlyList<Assignment>> PublishedAssignmentsAsync(int year, int month, CancellationToken cancellationToken = default) =>
        await db.Schedules.AsNoTracking()
            .Where(s => s.Year == year && s.Month == month && s.Status == ScheduleStatus.Published)
            .SelectMany(s => s.Assignments)
            .ToListAsync(cancellationToken);

    private Task<ScheduleStatus?> StatusAsync(int year, int month, CancellationToken cancellationToken) =>
        db.Schedules.AsNoTracking()
            .Where(s => s.Year == year && s.Month == month)
            .Select(s => (ScheduleStatus?)s.Status)
            .SingleOrDefaultAsync(cancellationToken);
}
