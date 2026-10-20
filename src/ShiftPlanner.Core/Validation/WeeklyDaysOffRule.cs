using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Core.Validation;

/// <summary>
/// H2：每週（週一到週日）至少 2 天沒有任何班次開始。
/// 範圍結束後的未知天數可以用來補足，因此月底不完整的週只要求 max(0, 2 − 未知天數)。
/// </summary>
public sealed class WeeklyDaysOffRule : IHardRule
{
    public const int RequiredDaysOff = 2;

    public IEnumerable<Violation> Check(ScheduleSnapshot snapshot)
    {
        var horizon = snapshot.Horizon;
        foreach (var employee in snapshot.Employees)
        {
            var workDates = snapshot.AssignmentsOf(employee.Id).Select(a => a.Date).ToHashSet();
            foreach (var monday in horizon.WeekStarts())
            {
                var sunday = monday.AddDays(6);
                var unknownDays = Math.Max(0, sunday.DayNumber - horizon.End.DayNumber);
                var required = Math.Max(0, RequiredDaysOff - unknownDays);
                var knownDays = 7 - unknownDays;
                var worked = Enumerable.Range(0, knownDays).Count(i => workDates.Contains(monday.AddDays(i)));
                var daysOff = knownDays - worked;
                if (daysOff >= required)
                {
                    continue;
                }

                yield return new Violation(
                    RuleCode.WeeklyDaysOff,
                    employee.Id,
                    monday < horizon.Start ? horizon.Start : monday,
                    null,
                    $"{employee.Name} {monday.ToLabel()}–{sunday.ToLabel()} 這週只有 {daysOff} 天不排班（需 ≥ {RequiredDaysOff} 天）");
            }
        }
    }
}
