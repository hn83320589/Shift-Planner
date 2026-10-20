namespace ShiftPlanner.Core.Domain;

public sealed record Employee(
    int Id,
    string Name,
    ContractType ContractType,
    int WeeklyHourLimit,
    Skills Skills,
    ShiftTypes AllowedShifts)
{
    public bool IsFullTime => ContractType == ContractType.FullTime;

    public bool CanWork(ShiftType shift) => AllowedShifts.HasFlag(shift.ToFlag());

    public bool HasSkill(Skills skill) => (Skills & skill) == skill;
}
