using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Core.Validation;

/// <summary>H3：連續「有班次開始」的日期不得超過 6 天，會接上前後月份的上下文一起計算。</summary>
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
                var horizon = snapshot.Horizon;
                if (length <= MaxConsecutiveDays || end < horizon.Start || start > horizon.End)
                {
                    continue;
                }

                // 標示在第一個超過上限的日子；若落在範圍外，則標示在範圍內最接近的那天
                var firstExceeded = start.AddDays(MaxConsecutiveDays);
                var marked = firstExceeded < horizon.Start ? horizon.Start
                    : firstExceeded > horizon.End ? horizon.End
                    : firstExceeded;
                yield return new Violation(
                    RuleCode.ConsecutiveDays,
                    employee.Id,
                    marked,
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
