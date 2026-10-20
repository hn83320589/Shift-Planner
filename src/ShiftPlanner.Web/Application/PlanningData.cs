using Microsoft.EntityFrameworkCore;

using ShiftPlanner.Core.Calendar;
using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;
using ShiftPlanner.Data;
using ShiftPlanner.Scheduling;

namespace ShiftPlanner.Web.Application;

/// <summary>排某個月所需的全部資料，產生器、validator、報表都從這裡取得，確保輸入一致。</summary>
public sealed record PlanningData(
    DateRange Period,
    IReadOnlyList<Employee> Employees,
    StaffingCalendar Calendar,
    IReadOnlyList<Leave> Leaves,
    IReadOnlyList<OffPreference> Preferences,
    IReadOnlyList<Assignment> PreviousMonth)
{
    public ScheduleSnapshot Snapshot(IReadOnlyList<Assignment> assignments) =>
        new(Period, Employees, assignments, Calendar, PreviousMonth, Leaves);

    public GenerationRequest GenerationRequest() => new()
    {
        Horizon = Period,
        Employees = Employees,
        Calendar = Calendar,
        Context = PreviousMonth,
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

        var calendar = new StaffingCalendar(
            await db.Holidays.AsNoTracking().ToListAsync(cancellationToken),
            await db.StaffingRequirements.AsNoTracking().ToListAsync(cancellationToken),
            await db.SkillRequirements.AsNoTracking().ToListAsync(cancellationToken),
            await db.StaffingOverrides.AsNoTracking()
                .Where(o => o.Date >= period.Start && o.Date <= period.End).ToListAsync(cancellationToken));

        // 只有已發布的前月班表才是確定的上下文；前月還是草稿時視為沒有上下文。
        var previousMonth = await db.Schedules.AsNoTracking()
            .Where(s => s.Year == previous.Year && s.Month == previous.Month && s.Status == ScheduleStatus.Published)
            .SelectMany(s => s.Assignments)
            .ToListAsync(cancellationToken);

        return new PlanningData(
            period,
            await db.Employees.AsNoTracking().OrderBy(e => e.Id).ToListAsync(cancellationToken),
            calendar,
            await db.Leaves.AsNoTracking()
                .Where(l => l.Start <= period.End && l.End >= period.Start).ToListAsync(cancellationToken),
            await db.OffPreferences.AsNoTracking()
                .Where(p => p.Date >= period.Start && p.Date <= period.End).ToListAsync(cancellationToken),
            previousMonth);
    }
}
