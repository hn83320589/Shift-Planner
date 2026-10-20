namespace ShiftPlanner.Core.Domain;

/// <summary>主管登記的請假，區間內為硬性不可排班。</summary>
public sealed record Leave(int EmployeeId, DateOnly Start, DateOnly End)
{
    public int Id { get; init; }

    public bool Contains(DateOnly date) => date >= Start && date <= End;
}

/// <summary>員工希望休假的日期，屬於軟性偏好。</summary>
public sealed record OffPreference(int EmployeeId, DateOnly Date, PreferencePriority Priority)
{
    public int Id { get; init; }
}
