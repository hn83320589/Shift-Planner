using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Web.Components;

/// <summary>畫面上的簡短中文標籤。</summary>
public static class Format
{
    private const string WeekdayNames = "日一二三四五六";

    public static string Weekday(DateOnly date) => WeekdayNames[(int)date.DayOfWeek].ToString();

    public static string ShortName(this ShiftType shift) => shift.ToDisplayName()[..1];

    public static string CssClass(this ShiftType shift) => shift.ToString().ToLowerInvariant();

    public static string DateWithWeekday(DateOnly date) => $"{date.Month}/{date.Day}（{Weekday(date)}）";
}
