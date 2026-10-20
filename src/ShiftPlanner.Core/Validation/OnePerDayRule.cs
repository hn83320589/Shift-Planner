using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Core.Validation;

/// <summary>H6：每人每天（以班次開始日計）最多一班。</summary>
public sealed class OnePerDayRule : IHardRule
{
    public IEnumerable<Violation> Check(ScheduleSnapshot snapshot) =>
        from a in snapshot.Assignments
        group a by (a.EmployeeId, a.Date) into day
        where day.Count() > 1
        let employee = snapshot.GetEmployee(day.Key.EmployeeId)
        from a in day
        select new Violation(
            RuleCode.OnePerDay,
            employee.Id,
            a.Date,
            a.Shift,
            $"{employee.Name} {a.Date.ToLabel()} 排了 {day.Count()} 班（"
            + string.Join("、", day.Select(x => x.Shift.ToDisplayName())) + "）");
}
