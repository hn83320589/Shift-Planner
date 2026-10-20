using ShiftPlanner.Core.Calendar;
using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Data.Seed;

/// <summary>
/// 範例資料：16 位員工（12 全職、4 兼職）、人力與技能需求、2026 年 11 月休假偏好。
/// 姓名皆為虛構。
/// </summary>
public static class SampleData
{
    private const Skills Zh = Skills.Chinese;
    private const Skills En = Skills.English;
    private const Skills Ja = Skills.Japanese;

    public static IReadOnlyList<Employee> Employees { get; } =
    [
        FullTime(1, "王小明", Zh | En),
        FullTime(2, "李美玲", Zh | En),
        FullTime(3, "張志豪", Zh | En),
        FullTime(4, "陳怡君", Zh | En),
        FullTime(5, "林建宏", Zh | En | Ja),
        FullTime(6, "黃淑芬", Zh | En),
        FullTime(7, "吳家豪", Zh | Ja),
        FullTime(8, "劉雅婷", Zh | Ja),
        FullTime(9, "蔡宗翰", Zh),
        FullTime(10, "楊佩珊", Zh),
        FullTime(11, "許文傑", Zh),
        FullTime(12, "鄭惠如", Zh | En),
        new(13, "謝依婷", ContractType.PartTime, 24, Zh | En, ShiftTypes.Early | ShiftTypes.Late),
        new(14, "郭俊宏", ContractType.PartTime, 32, Zh | Ja, ShiftTypes.Early | ShiftTypes.Late),
        new(15, "洪詩涵", ContractType.PartTime, 24, Zh, ShiftTypes.Early),
        new(16, "曾冠宇", ContractType.PartTime, 32, Zh | En, ShiftTypes.Late | ShiftTypes.Night),
    ];

    public static IReadOnlyList<StaffingRequirement> StaffingRequirements { get; } =
    [
        new(DayType.Weekday, ShiftType.Early, 4),
        new(DayType.Weekday, ShiftType.Late, 4),
        new(DayType.Weekday, ShiftType.Night, 2),
        new(DayType.Holiday, ShiftType.Early, 3),
        new(DayType.Holiday, ShiftType.Late, 3),
        new(DayType.Holiday, ShiftType.Night, 2),
    ];

    /// <summary>每一班至少 1 位會英文；日文只在平日的早班與晚班，至少 1 位。</summary>
    public static IReadOnlyList<SkillRequirement> SkillRequirements { get; } =
    [
        .. from dayType in Enum.GetValues<DayType>()
           from shift in DisplayNames.AllShifts
           select new SkillRequirement(dayType, shift, En, 1),
        new SkillRequirement(DayType.Weekday, ShiftType.Early, Ja, 1),
        new SkillRequirement(DayType.Weekday, ShiftType.Late, Ja, 1),
    ];

    /// <summary>2026 年 11 月的休假偏好。11/14（六）刻意集中多筆，製造競爭。</summary>
    public static IReadOnlyList<OffPreference> NovemberPreferences { get; } =
    [
        .. Strong(1, 7, 21),
        .. Strong(2, 14),
        .. Strong(3, 11, 12),
        .. Strong(4, 28),
        .. Strong(5, 6),
        .. Strong(6, 14, 15),
        .. Strong(7, 20),
        .. Strong(8, 14),
        .. Strong(9, 1, 22),
        .. Strong(10, 29),
        .. Strong(11, 14),
        .. Strong(12, 25),
        .. Strong(13, 18),
        .. Strong(14, 9),
        .. Strong(15, 23),
        .. Strong(16, 14),

        .. Normal(1, 3, 15),
        .. Normal(2, 8, 22, 29),
        .. Normal(3, 1, 24),
        .. Normal(4, 5, 13, 21),
        .. Normal(5, 14, 27),
        .. Normal(6, 2, 26),
        .. Normal(7, 7, 8, 21),
        .. Normal(8, 15, 28),
        .. Normal(9, 10, 17),
        .. Normal(10, 6, 7, 14),
        .. Normal(11, 4, 21),
        .. Normal(12, 1, 8, 14),
        .. Normal(13, 7, 28),
        .. Normal(14, 21, 22),
        .. Normal(15, 14, 30),
        .. Normal(16, 7, 21),
    ];

    private static Employee FullTime(int id, string name, Skills skills) =>
        new(id, name, ContractType.FullTime, 40, skills, ShiftTypes.All);

    private static IEnumerable<OffPreference> Strong(int employeeId, params int[] days) =>
        days.Select(d => new OffPreference(employeeId, new DateOnly(2026, 11, d), PreferencePriority.Strong));

    private static IEnumerable<OffPreference> Normal(int employeeId, params int[] days) =>
        days.Select(d => new OffPreference(employeeId, new DateOnly(2026, 11, d), PreferencePriority.Normal));
}
