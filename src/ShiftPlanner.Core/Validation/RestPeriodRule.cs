using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Core.Validation;

/// <summary>
/// H1：同一人相鄰兩班，下一班開始時間 − 上一班結束時間 ≥ 11 小時。
/// 只要其中一班在範圍內就檢查，標示在範圍內的那一班（跨到下個月時標示本月的班）。
/// </summary>
public sealed class RestPeriodRule(TimeSpan minimumRest) : IHardRule
{
    public static readonly TimeSpan DefaultMinimumRest = TimeSpan.FromHours(11);

    public RestPeriodRule() : this(DefaultMinimumRest)
    {
    }

    public IEnumerable<Violation> Check(ScheduleSnapshot snapshot)
    {
        foreach (var employee in snapshot.Employees)
        {
            var shifts = snapshot.AssignmentsOf(employee.Id)
                .Select(a => (Assignment: a, Interval: snapshot.Catalog.GetInterval(a)))
                .OrderBy(x => x.Interval.Start)
                .ToList();

            for (var i = 1; i < shifts.Count; i++)
            {
                var (prev, prevInterval) = shifts[i - 1];
                var (next, nextInterval) = shifts[i];
                var nextInHorizon = snapshot.Horizon.Contains(next.Date);
                if (!nextInHorizon && !snapshot.Horizon.Contains(prev.Date))
                {
                    continue;
                }

                var rest = nextInterval.Start - prevInterval.End;
                if (rest >= minimumRest)
                {
                    continue;
                }

                var restText = rest < TimeSpan.Zero ? "時段重疊" : $"只休息 {rest.TotalHours:0.#} 小時";
                var marked = nextInHorizon ? next : prev;
                yield return new Violation(
                    RuleCode.RestPeriod,
                    employee.Id,
                    marked.Date,
                    marked.Shift,
                    $"{employee.Name} {prev.Date.ToLabel()} {prev.Shift.ToDisplayName()} → "
                    + $"{next.Date.ToLabel()} {next.Shift.ToDisplayName()} {restText}（需 ≥ {minimumRest.TotalHours:0.#} 小時）");
            }
        }
    }
}
