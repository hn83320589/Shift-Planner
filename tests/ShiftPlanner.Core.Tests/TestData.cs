using System.Globalization;

using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;

namespace ShiftPlanner.Core.Tests;

/// <summary>測試用的精簡資料建構工具。日期預設都在 2026 年 11 月。</summary>
internal static class TestData
{
    public static readonly DateRange November = DateRange.ForMonth(2026, 11);

    public static DateOnly D(string date) => DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static Employee FullTime(int id, Skills skills = Skills.Chinese, ShiftTypes allowed = ShiftTypes.All) =>
        new(id, $"員工{id}", ContractType.FullTime, 40, skills, allowed);

    public static Employee PartTime(int id, int weeklyHours, ShiftTypes allowed = ShiftTypes.All, Skills skills = Skills.Chinese) =>
        new(id, $"兼職{id}", ContractType.PartTime, weeklyHours, skills, allowed);

    public static Assignment A(int employeeId, string date, ShiftType shift = ShiftType.Early) =>
        new(employeeId, D(date), shift);

    /// <summary>從 startDate 起連續 days 天的同一班別。</summary>
    public static Assignment[] Consecutive(int employeeId, string startDate, int days, ShiftType shift = ShiftType.Early) =>
        Enumerable.Range(0, days).Select(i => new Assignment(employeeId, D(startDate).AddDays(i), shift)).ToArray();

    public static ScheduleSnapshot Snapshot(
        IEnumerable<Assignment> assignments,
        IEnumerable<Employee>? employees = null,
        IEnumerable<Assignment>? context = null,
        IEnumerable<Leave>? leaves = null,
        IStaffingCalendar? calendar = null,
        ShiftCatalog? catalog = null,
        DateRange? horizon = null,
        FollowingContext? following = null) =>
        new(
            horizon ?? November,
            (employees ?? [FullTime(1)]).ToList(),
            assignments.ToList(),
            calendar ?? new FixedStaffingCalendar(),
            context?.ToList(),
            leaves?.ToList(),
            catalog,
            following);

    /// <summary>12 月整月已發布，作為 11 月的後續上下文。</summary>
    public static FollowingContext December(params Assignment[] assignments) => new(D("2026-12-31"), assignments);

    public static List<Violation> ViolationsOf(this ValidationResult result, RuleCode code) =>
        result.Violations.Where(v => v.Code == code).ToList();

    public static ValidationResult Validate(ScheduleSnapshot snapshot) => new ScheduleValidator().Validate(snapshot);
}

/// <summary>每天每班相同需求的簡易日曆，預設不需要任何人。</summary>
internal sealed class FixedStaffingCalendar(int minStaff = 0, params Skills[] requiredSkills) : IStaffingCalendar
{
    public DayType GetDayType(DateOnly date) => DayType.Weekday;

    public int GetMinStaff(DateOnly date, ShiftType shift) => minStaff;

    public IReadOnlyList<SkillRequirement> GetSkillRequirements(DateOnly date, ShiftType shift) =>
        requiredSkills.Select(s => new SkillRequirement(DayType.Weekday, shift, s, 1)).ToList();
}
