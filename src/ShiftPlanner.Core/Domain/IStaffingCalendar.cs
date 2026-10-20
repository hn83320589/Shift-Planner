namespace ShiftPlanner.Core.Domain;

public sealed record SkillRequirement(DayType DayType, ShiftType Shift, Skills Skill, int MinCount);

/// <summary>回答「某天某班需要多少人、需要哪些技能」。</summary>
public interface IStaffingCalendar
{
    DayType GetDayType(DateOnly date);

    int GetMinStaff(DateOnly date, ShiftType shift);

    IReadOnlyList<SkillRequirement> GetSkillRequirements(DateOnly date, ShiftType shift);
}
