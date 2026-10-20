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
        reasons.AddRange(CheckDailyHeadcount(request));
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

    /// <summary>
    /// 每天的總需求人數 vs. 當天能上班的人數。
    /// 前後月份已確定的班可能讓某些人當天「必須休假」（跨月的週休息日、連續上班、週工時），
    /// 這是月底不完整的週把排休推給下個月時最常見的無解原因，要明確指出人數。
    /// </summary>
    private static IEnumerable<InfeasibilityReason> CheckDailyHeadcount(GenerationRequest request)
    {
        var horizon = request.Horizon;
        var knownUntil = request.Following?.KnownUntil ?? horizon.End;
        var context = request.Context.ToLookup(a => a.EmployeeId);
        var following = (request.Following?.Assignments ?? []).ToLookup(a => a.EmployeeId);
        var shortestShiftHours = DisplayNames.AllShifts.Min(request.Catalog.HoursOf);

        foreach (var date in horizon.Days())
        {
            var required = DisplayNames.AllShifts.Sum(s => request.Calendar.GetMinStaff(date, s));
            var available = 0;
            var forcedByPrevious = 0;
            var forcedByNext = 0;
            foreach (var employee in request.Employees)
            {
                if (request.Leaves.Any(l => l.EmployeeId == employee.Id && l.Contains(date))
                    || !DisplayNames.AllShifts.Any(employee.CanWork))
                {
                    continue;
                }

                var before = context[employee.Id].Select(a => a.Date).ToHashSet();
                var after = following[employee.Id].Select(a => a.Date).ToHashSet();
                if (ForcedOff(date, before, after, employee.WeeklyHourLimit, out var byPrevious))
                {
                    if (byPrevious)
                    {
                        forcedByPrevious++;
                    }
                    else
                    {
                        forcedByNext++;
                    }
                }
                else
                {
                    available++;
                }
            }

            if (required <= available)
            {
                continue;
            }

            var causes = new List<string>();
            if (forcedByPrevious > 0)
            {
                causes.Add($"{forcedByPrevious} 人因上個月月底的排班當天必須休假");
            }

            if (forcedByNext > 0)
            {
                causes.Add($"{forcedByNext} 人因下個月月初的排班當天必須休假");
            }

            var because = causes.Count > 0 ? $"，其中{string.Join("、", causes)}（每週休息日、連續上班或週工時限制）" : "";
            yield return new InfeasibilityReason(date, null, required, available,
                $"{date.ToLabel()} 共需要 {required} 人上班，但只有 {available} 人可排{because}");
        }

        // 當天是否因前後月份的班而必須休假；byPrevious 表示原因在上個月（跨越月初的週或連續段）
        bool ForcedOff(DateOnly date, HashSet<DateOnly> before, HashSet<DateOnly> after, int weeklyHourLimit, out bool byPrevious)
        {
            var monday = date.WeekStart();
            var sunday = monday.AddDays(6);
            var weekDays = Enumerable.Range(0, 7).Select(monday.AddDays).ToList();
            var horizonDays = weekDays.Count(d => horizon.Contains(d));
            var unknownDays = Math.Max(0, sunday.DayNumber - knownUntil.DayNumber);
            var requiredOff = Math.Max(0, WeeklyDaysOffRule.RequiredDaysOff - unknownDays);
            var offOutside = weekDays.Count(d => (d < horizon.Start && !before.Contains(d)) || (d > horizon.End && d <= knownUntil && !after.Contains(d)));
            var workedOutside = weekDays.Count(d => before.Contains(d) || after.Contains(d));
            var forcedByWeek = requiredOff - offOutside >= horizonDays
                || (workedOutside + 1) * shortestShiftHours > weeklyHourLimit;

            var runBefore = 0;
            while (before.Contains(horizon.Start.AddDays(-runBefore - 1)))
            {
                runBefore++;
            }

            var runAfter = 0;
            while (after.Contains(horizon.End.AddDays(runAfter + 1)))
            {
                runAfter++;
            }

            var max = ConsecutiveDaysRule.MaxConsecutiveDays;
            var forcedByRunBefore = runBefore >= max && date == horizon.Start;
            var forcedByRunAfter = runAfter >= max && date == horizon.End;

            byPrevious = forcedByRunBefore || (forcedByWeek && monday < horizon.Start);
            return forcedByWeek || forcedByRunBefore || forcedByRunAfter;
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
