using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Core.Calendar;

/// <summary>(DayType, ShiftType) 的每日最低人數。</summary>
public sealed record StaffingRequirement(DayType DayType, ShiftType Shift, int MinStaff);

/// <summary>單日覆寫某班的最低人數，優先於 StaffingRequirement。</summary>
public sealed record StaffingOverride(DateOnly Date, ShiftType Shift, int MinStaff);

/// <summary>
/// 依國定假日判斷 DayType，再查出人力與技能需求。
/// 判斷順序：行事曆有登錄 → 依 IsHoliday；沒有登錄 → 週六日為假日，其餘為平日。
/// </summary>
public sealed class StaffingCalendar : IStaffingCalendar
{
    private readonly Dictionary<DateOnly, Holiday> _holidays;
    private readonly Dictionary<(DayType, ShiftType), int> _requirements;
    private readonly Dictionary<(DateOnly, ShiftType), int> _overrides;
    private readonly ILookup<(DayType, ShiftType), SkillRequirement> _skills;

    public StaffingCalendar(
        IEnumerable<Holiday> holidays,
        IEnumerable<StaffingRequirement> requirements,
        IEnumerable<SkillRequirement> skillRequirements,
        IEnumerable<StaffingOverride>? overrides = null)
    {
        _holidays = holidays.ToDictionary(h => h.Date);
        _requirements = requirements.ToDictionary(r => (r.DayType, r.Shift), r => r.MinStaff);
        _overrides = (overrides ?? []).ToDictionary(o => (o.Date, o.Shift), o => o.MinStaff);
        _skills = skillRequirements.ToLookup(s => (s.DayType, s.Shift));

        var missing = (from dayType in Enum.GetValues<DayType>()
                       from shift in DisplayNames.AllShifts
                       where !_requirements.ContainsKey((dayType, shift))
                       select $"{dayType}/{shift}").ToList();
        if (missing.Count > 0)
        {
            throw new ArgumentException($"人力需求設定不完整，缺少：{string.Join("、", missing)}");
        }
    }

    public DayType GetDayType(DateOnly date)
    {
        if (_holidays.TryGetValue(date, out var holiday))
        {
            return holiday.IsHoliday ? DayType.Holiday : DayType.Weekday;
        }

        return date.IsWeekend() ? DayType.Holiday : DayType.Weekday;
    }

    public string? GetHolidayName(DateOnly date) =>
        _holidays.TryGetValue(date, out var holiday) ? holiday.Name : null;

    public int GetMinStaff(DateOnly date, ShiftType shift) =>
        _overrides.TryGetValue((date, shift), out var overridden)
            ? overridden
            : _requirements[(GetDayType(date), shift)];

    public IReadOnlyList<SkillRequirement> GetSkillRequirements(DateOnly date, ShiftType shift) =>
        _skills[(GetDayType(date), shift)].ToList();
}
