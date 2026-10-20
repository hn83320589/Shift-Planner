namespace ShiftPlanner.Core.Domain;

/// <summary>畫面與訊息用的中文名稱與日期格式。</summary>
public static class DisplayNames
{
    public static IReadOnlyList<ShiftType> AllShifts { get; } = [ShiftType.Early, ShiftType.Late, ShiftType.Night];

    public static string ToDisplayName(this ShiftType shift) => shift switch
    {
        ShiftType.Early => "早班",
        ShiftType.Late => "晚班",
        ShiftType.Night => "夜班",
        _ => throw new ArgumentOutOfRangeException(nameof(shift), shift, null),
    };

    public static string ToDisplayName(this Skills skill) => skill switch
    {
        Skills.Chinese => "中文",
        Skills.English => "英文",
        Skills.Japanese => "日文",
        _ => string.Join("、", Enum.GetValues<Skills>()
            .Where(s => s != Skills.None && skill.HasFlag(s))
            .Select(s => s.ToDisplayName())),
    };

    public static string ToDisplayName(this ContractType contract) =>
        contract == ContractType.FullTime ? "全職" : "兼職";

    public static string ToDisplayName(this PreferencePriority priority) =>
        priority == PreferencePriority.Strong ? "強烈" : "一般";

    public static ShiftTypes ToFlag(this ShiftType shift) => shift switch
    {
        ShiftType.Early => ShiftTypes.Early,
        ShiftType.Late => ShiftTypes.Late,
        ShiftType.Night => ShiftTypes.Night,
        _ => throw new ArgumentOutOfRangeException(nameof(shift), shift, null),
    };

    /// <summary>訊息用的短日期，例如 11/14。</summary>
    public static string ToLabel(this DateOnly date) => $"{date.Month}/{date.Day}";
}
