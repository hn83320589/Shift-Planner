using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Core.Validation;

/// <summary>H3：連續「有班次開始」的日期不得超過 6 天，會接上前月上下文一起計算。</summary>
public sealed class ConsecutiveDaysRule : IHardRule
{
    public const int MaxConsecutiveDays = 6;

    public IEnumerable<Violation> Check(ScheduleSnapshot snapshot)
    {
        foreach (var employee in snapshot.Employees)
        {
            var dates = snapshot.AssignmentsOf(employee.Id).Select(a => a.Date).Distinct().Order().ToList();
            foreach (var (start, end) in Runs(dates))
            {
                var length = end.DayNumber - start.DayNumber + 1;
                if (length <= MaxConsecutiveDays || end < snapshot.Horizon.Start)
                {
                    continue;
                }

                var firstExceeded = start.AddDays(MaxConsecutiveDays);
                yield return new Violation(
                    RuleCode.ConsecutiveDays,
                    employee.Id,
                    firstExceeded < snapshot.Horizon.Start ? snapshot.Horizon.Start : firstExceeded,
                    null,
                    $"{employee.Name} {start.ToLabel()}–{end.ToLabel()} 連續上班 {length} 天（上限 {MaxConsecutiveDays} 天）");
            }
        }
    }

    private static IEnumerable<(DateOnly Start, DateOnly End)> Runs(IReadOnlyList<DateOnly> sortedDates)
    {
        if (sortedDates.Count == 0)
        {
            yield break;
        }

        var start = sortedDates[0];
        for (var i = 1; i < sortedDates.Count; i++)
        {
            if (sortedDates[i] != sortedDates[i - 1].AddDays(1))
            {
                yield return (start, sortedDates[i - 1]);
                start = sortedDates[i];
            }
        }

        yield return (start, sortedDates[^1]);
    }
}
