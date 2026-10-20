using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Core.Validation;

/// <summary>H5：員工只能排在自己可上的班別。</summary>
public sealed class AllowedShiftRule : IHardRule
{
    public IEnumerable<Violation> Check(ScheduleSnapshot snapshot) =>
        from a in snapshot.Assignments
        let employee = snapshot.GetEmployee(a.EmployeeId)
        where !employee.CanWork(a.Shift)
        select new Violation(
            RuleCode.AllowedShift,
            employee.Id,
            a.Date,
            a.Shift,
            $"{employee.Name} 不可上{a.Shift.ToDisplayName()}，卻被排在 {a.Date.ToLabel()}");
}
