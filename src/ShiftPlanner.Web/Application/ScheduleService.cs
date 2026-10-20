using Microsoft.EntityFrameworkCore;

using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;
using ShiftPlanner.Data;
using ShiftPlanner.Scheduling;

namespace ShiftPlanner.Web.Application;

/// <summary>某月班表目前的狀態與即時驗證結果，畫面與 API 共用。</summary>
public sealed record ScheduleState(
    int Year,
    int Month,
    ScheduleStatus? Status,
    int Revision,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<Assignment> Assignments,
    PlanningData Data,
    ValidationResult Validation)
{
    public bool Exists => Status is not null;
    public bool IsPublished => Status == ScheduleStatus.Published;
}

/// <summary>
/// 班表的應用層操作。所有修改都經過 Schedule 領域物件，
/// 因此已發布的班表不論從頁面或 API 都無法直接修改。每次修改立即存成草稿。
/// </summary>
public sealed class ScheduleService(
    ShiftPlannerDbContext db,
    PlanningDataLoader loader,
    ShiftScheduleGenerator generator,
    ScheduleValidator validator,
    TimeProvider time)
{
    public async Task<ScheduleState> GetStateAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var schedule = await FindAsync(year, month, cancellationToken);
        var data = await loader.LoadAsync(year, month, cancellationToken);
        var assignments = schedule?.Assignments.ToList() ?? [];
        return new ScheduleState(
            year, month, schedule?.Status, schedule?.Revision ?? 0, schedule?.PublishedAt,
            assignments, data, validator.Validate(data.Snapshot(assignments)));
    }

    /// <summary>以產生器重新產生草稿；成功才會取代原本的草稿內容。</summary>
    public async Task<GenerationResult> GenerateDraftAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var schedule = await FindOrCreateAsync(year, month, cancellationToken);
        EnsureEditable(schedule);

        var data = await loader.LoadAsync(year, month, cancellationToken);
        var result = generator.Generate(data.GenerationRequest());
        if (result.Succeeded)
        {
            schedule.ReplaceAll(result.Assignments);
            await db.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    public Task AddAsync(int year, int month, Assignment assignment, CancellationToken cancellationToken = default) =>
        ModifyAsync(year, month, s => s.Add(assignment), [assignment.EmployeeId], cancellationToken);

    public Task RemoveAsync(int year, int month, Assignment assignment, CancellationToken cancellationToken = default) =>
        ModifyAsync(year, month, s => s.Remove(assignment), [], cancellationToken);

    /// <summary>換班：把一個班次改成另一個（可換人、換班別或換日期）。</summary>
    public Task ReplaceAsync(int year, int month, Assignment from, Assignment to, CancellationToken cancellationToken = default) =>
        ModifyAsync(year, month, s =>
        {
            s.Remove(from);
            s.Add(to);
        }, [to.EmployeeId], cancellationToken);

    /// <summary>兩位員工交換某天的班（任一方沒有班也可以，等於把班轉給對方）。</summary>
    public Task SwapAsync(int year, int month, DateOnly date, int employeeA, int employeeB, CancellationToken cancellationToken = default) =>
        ModifyAsync(year, month, s =>
        {
            var ofA = s.Assignments.Where(a => a.EmployeeId == employeeA && a.Date == date).ToList();
            var ofB = s.Assignments.Where(a => a.EmployeeId == employeeB && a.Date == date).ToList();
            foreach (var assignment in ofA.Concat(ofB))
            {
                s.Remove(assignment);
            }

            foreach (var assignment in ofA)
            {
                s.Add(assignment with { EmployeeId = employeeB });
            }

            foreach (var assignment in ofB)
            {
                s.Add(assignment with { EmployeeId = employeeA });
            }
        }, [employeeA, employeeB], cancellationToken);

    /// <summary>
    /// 重新驗證後發布；有任何硬性違規或人力缺口就拋出 SchedulePublishException。
    /// 必須依序發布：上個月已建立但還是草稿時不能發布，否則跨月限制沒有被檢查過。
    /// </summary>
    public async Task<ValidationResult> PublishAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var schedule = await FindAsync(year, month, cancellationToken)
            ?? throw new InvalidOperationException($"{year}/{month} 尚未建立班表");
        var data = await loader.LoadAsync(year, month, cancellationToken);
        if (data.PreviousMonthIsDraft)
        {
            throw new InvalidOperationException(
                $"{data.PreviousMonthStart.Year}/{data.PreviousMonthStart.Month} 班表尚未發布；請先發布上個月，跨月的休息與連續上班限制才能檢查");
        }
        var validation = validator.Validate(data.Snapshot(schedule.Assignments));

        schedule.Publish(validation, time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return validation;
    }

    private async Task ModifyAsync(
        int year, int month, Action<Schedule> change, IReadOnlyList<int> employeeIds, CancellationToken cancellationToken)
    {
        foreach (var id in employeeIds)
        {
            if (!await db.Employees.AnyAsync(e => e.Id == id, cancellationToken))
            {
                throw new ArgumentException($"員工 Id {id} 不存在");
            }
        }

        var schedule = await FindOrCreateAsync(year, month, cancellationToken);
        change(schedule);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureEditable(Schedule schedule)
    {
        if (schedule.IsPublished)
        {
            throw new ScheduleLockedException($"{schedule.Year}/{schedule.Month} 班表已發布，不可重新產生；請使用臨時重排");
        }
    }

    private Task<Schedule?> FindAsync(int year, int month, CancellationToken cancellationToken) =>
        db.Schedules.SingleOrDefaultAsync(s => s.Year == year && s.Month == month, cancellationToken);

    private async Task<Schedule> FindOrCreateAsync(int year, int month, CancellationToken cancellationToken)
    {
        var schedule = await FindAsync(year, month, cancellationToken);
        if (schedule is null)
        {
            schedule = new Schedule(year, month);
            db.Schedules.Add(schedule);
        }

        return schedule;
    }
}
