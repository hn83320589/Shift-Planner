using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Core.Validation;

/// <summary>H4：每週（週一到週日）班數 × 每班時數 ≤ 員工的每週工時上限。夜班算在開始日所屬的週。</summary>
public sealed class WeeklyHoursRule : IHardRule
{
    public IEnumerable<Violation> Check(ScheduleSnapshot snapshot)
    {
        var horizon = snapshot.Horizon;
        foreach (var employee in snapshot.Employees)
        {
            var byWeek = snapshot.AssignmentsOf(employee.Id).ToLookup(a => a.Date.WeekStart());
            foreach (var monday in horizon.WeekStarts())
            {
                var hours = byWeek[monday].Sum(a => snapshot.Catalog.HoursOf(a.Shift));
                if (hours <= employee.WeeklyHourLimit)
                {
                    continue;
                }

                yield return new Violation(
                    RuleCode.WeeklyHours,
                    employee.Id,
                    monday < horizon.Start ? horizon.Start : monday,
                    null,
                    $"{employee.Name} {monday.ToLabel()}–{monday.AddDays(6).ToLabel()} 這週排了 {hours:0.#} 小時（上限 {employee.WeeklyHourLimit} 小時）");
            }
        }
    }
}
