using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Core.Validation;

/// <summary>
/// Validator 的輸入：要檢查的範圍、範圍內的班次，以及範圍之前的唯讀上下文
/// （通常是前一個月已發布的班表），讓跨月的週與連續上班能正確判斷。
/// </summary>
public sealed class ScheduleSnapshot
{
    private readonly Dictionary<int, Employee> _employees;
    private readonly ILookup<int, Assignment> _byEmployee;

    public ScheduleSnapshot(
        DateRange horizon,
        IReadOnlyList<Employee> employees,
        IReadOnlyList<Assignment> assignments,
        IStaffingCalendar calendar,
        IReadOnlyList<Assignment>? context = null,
        IReadOnlyList<Leave>? leaves = null,
        ShiftCatalog? catalog = null)
    {
        Horizon = horizon;
        Employees = employees;
        Assignments = assignments;
        Calendar = calendar;
        Context = context ?? [];
        Leaves = leaves ?? [];
        Catalog = catalog ?? ShiftCatalog.Standard;
        _employees = employees.ToDictionary(e => e.Id);

        var outside = assignments.FirstOrDefault(a => !horizon.Contains(a.Date));
        if (outside is not null)
        {
            throw new ArgumentException($"班次 {outside} 不在驗證範圍 {horizon} 內");
        }

        var lateContext = Context.FirstOrDefault(a => a.Date >= horizon.Start);
        if (lateContext is not null)
        {
            throw new ArgumentException($"上下文班次 {lateContext} 必須早於驗證範圍開始日 {horizon.Start}");
        }

        var unknown = assignments.Concat(Context).FirstOrDefault(a => !_employees.ContainsKey(a.EmployeeId));
        if (unknown is not null)
        {
            throw new ArgumentException($"班次 {unknown} 的員工 Id {unknown.EmployeeId} 不存在");
        }

        _byEmployee = Context.Concat(assignments).ToLookup(a => a.EmployeeId);
    }

    public DateRange Horizon { get; }
    public IReadOnlyList<Employee> Employees { get; }
    public IReadOnlyList<Assignment> Assignments { get; }
    public IReadOnlyList<Assignment> Context { get; }
    public IReadOnlyList<Leave> Leaves { get; }
    public IStaffingCalendar Calendar { get; }
    public ShiftCatalog Catalog { get; }

    public Employee GetEmployee(int id) => _employees[id];

    /// <summary>某員工的所有班次（上下文 + 範圍內）。</summary>
    public IEnumerable<Assignment> AssignmentsOf(int employeeId) => _byEmployee[employeeId];
}
