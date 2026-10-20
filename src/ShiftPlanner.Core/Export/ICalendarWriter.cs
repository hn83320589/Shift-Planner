using System.Globalization;
using System.Text;

using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Core.Export;

/// <summary>
/// 個人班表的 iCalendar（RFC 5545）。時間一律以 TZID=Asia/Taipei 表示並附 VTIMEZONE，
/// 夜班的 DTEND 為隔天 07:00。UID 由員工、日期、班別組成，重新匯入同一班會更新而非重複。
/// </summary>
public static class ICalendarWriter
{
    private const string DateTimeFormat = "yyyyMMdd'T'HHmmss";
    private const int MaxLineOctets = 75;

    public static string Write(
        Employee employee,
        IEnumerable<Assignment> assignments,
        DateTimeOffset generatedAt,
        string calendarName,
        ShiftCatalog? catalog = null)
    {
        catalog ??= ShiftCatalog.Standard;
        var stamp = generatedAt.UtcDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture) + "Z";
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//ShiftPlanner//Shift Planner//ZH-TW",
            "CALSCALE:GREGORIAN",
            "METHOD:PUBLISH",
            $"X-WR-CALNAME:{Escape(calendarName)}",
            $"X-WR-TIMEZONE:{TaipeiTime.TimeZoneId}",
            "BEGIN:VTIMEZONE",
            $"TZID:{TaipeiTime.TimeZoneId}",
            "BEGIN:STANDARD",
            "DTSTART:19700101T000000",
            "TZOFFSETFROM:+0800",
            "TZOFFSETTO:+0800",
            "TZNAME:CST",
            "END:STANDARD",
            "END:VTIMEZONE",
        };

        foreach (var assignment in assignments.Where(a => a.EmployeeId == employee.Id).OrderBy(a => a.Date).ThenBy(a => a.Shift))
        {
            var (start, end) = catalog.GetInterval(assignment);
            var title = $"{assignment.Shift.ToDisplayName()} {start:HH:mm}–{end:HH:mm}";
            lines.AddRange(
            [
                "BEGIN:VEVENT",
                $"UID:{employee.Id}-{assignment.Date:yyyyMMdd}-{assignment.Shift.ToString().ToLowerInvariant()}@shiftplanner",
                $"DTSTAMP:{stamp}",
                $"DTSTART;TZID={TaipeiTime.TimeZoneId}:{start.ToString(DateTimeFormat, CultureInfo.InvariantCulture)}",
                $"DTEND;TZID={TaipeiTime.TimeZoneId}:{end.ToString(DateTimeFormat, CultureInfo.InvariantCulture)}",
                $"SUMMARY:{Escape(title)}",
                $"DESCRIPTION:{Escape($"{employee.Name} 客服中心{title}")}",
                "TRANSP:OPAQUE",
                "END:VEVENT",
            ]);
        }

        lines.Add("END:VCALENDAR");
        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            Fold(builder, line);
        }

        return builder.ToString();
    }

    /// <summary>TEXT 值需跳脫反斜線、分號、逗號與換行。</summary>
    private static string Escape(string text) =>
        text.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n");

    /// <summary>每行不超過 75 octets（UTF-8），續行以一個空白開頭；不切斷多位元組字元。</summary>
    private static void Fold(StringBuilder builder, string line)
    {
        var octets = 0;
        var limit = MaxLineOctets;
        var runes = line.EnumerateRunes();
        foreach (var rune in runes)
        {
            var size = rune.Utf8SequenceLength;
            if (octets + size > limit)
            {
                builder.Append("\r\n ");
                octets = 0;
                limit = MaxLineOctets - 1; // 續行開頭的空白也算一個 octet
            }

            builder.Append(rune.ToString());
            octets += size;
        }

        builder.Append("\r\n");
    }
}
