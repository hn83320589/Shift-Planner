namespace ShiftPlanner.Core.Domain;

/// <summary>包含起訖兩端的日期區間。</summary>
public readonly record struct DateRange
{
    public DateRange(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new ArgumentException($"日期區間結束日 {end} 早於開始日 {start}");
        }

        Start = start;
        End = end;
    }

    public DateOnly Start { get; }
    public DateOnly End { get; }

    public int DayCount => End.DayNumber - Start.DayNumber + 1;

    public static DateRange ForMonth(int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        return new DateRange(start, start.AddMonths(1).AddDays(-1));
    }

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    public IEnumerable<DateOnly> Days()
    {
        for (var d = Start; d <= End; d = d.AddDays(1))
        {
            yield return d;
        }
    }

    /// <summary>與此區間有交集的每一週（週一開始）的週一日期。</summary>
    public IEnumerable<DateOnly> WeekStarts()
    {
        for (var monday = Start.WeekStart(); monday <= End; monday = monday.AddDays(7))
        {
            yield return monday;
        }
    }

    public override string ToString() => $"{Start:yyyy-MM-dd}~{End:yyyy-MM-dd}";
}

public static class DateOnlyExtensions
{
    /// <summary>該日所屬週（週一到週日）的週一。</summary>
    public static DateOnly WeekStart(this DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static bool IsWeekend(this DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}
