using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Core.Validation;

/// <summary>H7：請假區間內不可排班。</summary>
public sealed class LeaveRule : IHardRule
{
    public IEnumerable<Violation> Check(ScheduleSnapshot snapshot)
    {
        var leaves = snapshot.Leaves.ToLookup(l => l.EmployeeId);
        return
            from a in snapshot.Assignments
            where leaves[a.EmployeeId].Any(l => l.Contains(a.Date))
            let employee = snapshot.GetEmployee(a.EmployeeId)
            select new Violation(
                RuleCode.OnLeave,
                employee.Id,
                a.Date,
                a.Shift,
                $"{employee.Name} {a.Date.ToLabel()} 請假，卻被排了{a.Shift.ToDisplayName()}");
    }
}
