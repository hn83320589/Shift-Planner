namespace ShiftPlanner.Core.Calendar;

/// <summary>
/// 行事曆上的特殊日期。IsHoliday = true 為國定假日或補假；
/// IsHoliday = false 代表補班日（週末但要上班），套用平日人力需求。
/// </summary>
public sealed record Holiday(DateOnly Date, string Name, bool IsHoliday);
