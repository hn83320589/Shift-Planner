using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Core.Validation;

public enum RuleCode
{
    RestPeriod,
    WeeklyDaysOff,
    ConsecutiveDays,
    WeeklyHours,
    AllowedShift,
    OnePerDay,
    OnLeave,
}

/// <summary>硬性違規。Date / Shift 指向畫面上應標示的格子。</summary>
public sealed record Violation(RuleCode Code, int EmployeeId, DateOnly Date, ShiftType? Shift, string Message);

/// <summary>人力缺口。Skill 為 null 表示總人數不足，否則為該技能人數不足。</summary>
public sealed record CoverageGap(DateOnly Date, ShiftType Shift, Skills? Skill, int Required, int Actual, string Message);

public sealed record ValidationResult(IReadOnlyList<Violation> Violations, IReadOnlyList<CoverageGap> Gaps)
{
    public bool HasHardViolations => Violations.Count > 0;

    /// <summary>硬性違規與人力缺口都沒有，才可以發布。</summary>
    public bool CanPublish => Violations.Count == 0 && Gaps.Count == 0;
}

public static class RuleCodeExtensions
{
    public static string ToDisplayName(this RuleCode code) => code switch
    {
        RuleCode.RestPeriod => "休息不足 11 小時",
        RuleCode.WeeklyDaysOff => "每週休息少於 2 天",
        RuleCode.ConsecutiveDays => "連續上班超過 6 天",
        RuleCode.WeeklyHours => "超過每週工時上限",
        RuleCode.AllowedShift => "排到不可上的班別",
        RuleCode.OnePerDay => "同一天超過一班",
        RuleCode.OnLeave => "請假日被排班",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
