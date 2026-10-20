using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;

namespace ShiftPlanner.Scheduling;

/// <summary>
/// 不需要求解器的靜態檢查，能直接指出「哪一天哪一班需要幾人、只有幾人可排」。
/// 可排 = 可上該班別、當天未請假、且不會與前後月份的上下文違反休息規定。
/// </summary>
internal static class FeasibilityPrecheck
{
    public static IReadOnlyList<InfeasibilityReason> Check(GenerationRequest request)
    {
        var reasons = new List<InfeasibilityReason>();
        reasons.AddRange(CheckSlots(request));
        reasons.AddRange(CheckWeeklyCapacity(request));
        return reasons;
    }

    private static IEnumerable<InfeasibilityReason> CheckSlots(GenerationRequest request)
    {
        var leaves = request.Leaves.ToLookup(l => l.EmployeeId);
        var context = request.Context.Select(a => (a.EmployeeId, a.Date, a.Shift)).ToHashSet();
        var following = (request.Following?.Assignments ?? []).Select(a => (a.EmployeeId, a.Date, a.Shift)).ToHashSet();
        var conflicts = RestConflicts.For(request.Catalog);

        bool BlockedByContext(int employeeId, DateOnly date, ShiftType shift) =>
            conflicts.Any(c =>
                (c.Next == shift
                    && date.AddDays(-c.Offset) < request.Horizon.Start
                    && context.Contains((employeeId, date.AddDays(-c.Offset), c.Prev)))
                || (c.Prev == shift
                    && date.AddDays(c.Offset) > request.Horizon.End
                    && following.Contains((employeeId, date.AddDays(c.Offset), c.Next))));

        foreach (var date in request.Horizon.Days())
        {
            foreach (var shift in DisplayNames.AllShifts)
            {
                var candidates = request.Employees
                    .Where(e => e.CanWork(shift)
                        && !leaves[e.Id].Any(l => l.Contains(date))
                        && !BlockedByContext(e.Id, date, shift))
                    .ToList();
                var slot = $"{date.ToLabel()} {shift.ToDisplayName()}";
                var who = $"可上{shift.ToDisplayName()}、未請假且不違反休息規定的";

                var required = request.Calendar.GetMinStaff(date, shift);
                if (candidates.Count < required)
                {
                    yield return new InfeasibilityReason(date, shift, required, candidates.Count,
                        $"{slot}需要 {required} 人，但{who}只有 {candidates.Count} 人");
                }

                foreach (var skill in request.Calendar.GetSkillRequirements(date, shift))
                {
                    var skilled = candidates.Count(e => e.HasSkill(skill.Skill));
                    if (skilled < skill.MinCount)
                    {
                        yield return new InfeasibilityReason(date, shift, skill.MinCount, skilled,
                            $"{slot}需要 {skill.MinCount} 位會{skill.Skill.ToDisplayName()}的人員，但{who}只有 {skilled} 位");
                    }
                }
            }
        }
    }

    /// <summary>完整落在範圍內的週：總需求班數 vs. 每人在工時與休息日限制下的最多班數總和。</summary>
    private static IEnumerable<InfeasibilityReason> CheckWeeklyCapacity(GenerationRequest request)
    {
        var maxDays = 7 - WeeklyDaysOffRule.RequiredDaysOff;
        foreach (var monday in request.Horizon.WeekStarts())
        {
            var sunday = monday.AddDays(6);
            if (monday < request.Horizon.Start || sunday > request.Horizon.End)
            {
                continue;
            }

            var week = new DateRange(monday, sunday);
            var demand = week.Days().Sum(d => DisplayNames.AllShifts.Sum(s => request.Calendar.GetMinStaff(d, s)));
            var capacity = request.Employees.Sum(e =>
            {
                var availableDays = week.Days().Count(d => !request.Leaves.Any(l => l.EmployeeId == e.Id && l.Contains(d)));
                var byHours = (int)(e.WeeklyHourLimit / request.Catalog.HoursOf(ShiftType.Early));
                return Math.Min(Math.Min(maxDays, availableDays), byHours);
            });

            if (demand > capacity)
            {
                yield return new InfeasibilityReason(null, null, demand, capacity,
                    $"{monday.ToLabel()}–{sunday.ToLabel()} 這週共需要 {demand} 班，但所有人在每週工時、休息日與請假限制下最多只能上 {capacity} 班");
            }
        }
    }
}
