using System.Text;

using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Export;

using static ShiftPlanner.Core.Tests.TestData;

namespace ShiftPlanner.Core.Tests.Export;

public class ICalendarWriterTests
{
    private static readonly Employee Employee = new(7, "吳家豪", ContractType.FullTime, 40, Skills.Chinese, ShiftTypes.All);
    private static readonly DateTimeOffset GeneratedAt = new(2026, 10, 20, 9, 30, 0, TimeSpan.FromHours(8));

    private static string Write(params Assignment[] assignments) =>
        ICalendarWriter.Write(Employee, assignments, GeneratedAt, "吳家豪 2026/11 班表");

    private static string[] EventLines(string ics, int index) =>
        ics.Split("BEGIN:VEVENT")[index + 1].Split("END:VEVENT")[0].Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void 夜班結束時間為隔天0700_台北時區()
    {
        var ics = Write(A(7, "2026-11-14", ShiftType.Night));

        var lines = EventLines(ics, 0);
        Assert.Contains("DTSTART;TZID=Asia/Taipei:20261114T230000", lines);
        Assert.Contains("DTEND;TZID=Asia/Taipei:20261115T070000", lines);
    }

    [Fact]
    public void 早班與晚班在同一天開始結束()
    {
        var ics = Write(A(7, "2026-11-10", ShiftType.Early), A(7, "2026-11-12", ShiftType.Late));

        Assert.Contains("DTEND;TZID=Asia/Taipei:20261110T150000", EventLines(ics, 0));
        Assert.Contains("DTSTART;TZID=Asia/Taipei:20261112T150000", EventLines(ics, 1));
        Assert.Contains("DTEND;TZID=Asia/Taipei:20261112T230000", EventLines(ics, 1));
    }

    [Fact]
    public void 附上Asia_Taipei的VTIMEZONE定義_固定UTC加8()
    {
        var ics = Write(A(7, "2026-11-14", ShiftType.Night));

        Assert.Contains("BEGIN:VTIMEZONE\r\nTZID:Asia/Taipei\r\n", ics);
        Assert.Contains("TZOFFSETFROM:+0800\r\nTZOFFSETTO:+0800\r\n", ics);
    }

    [Fact]
    public void DTSTAMP為UTC_UID對同一班保持穩定()
    {
        var ics = Write(A(7, "2026-11-14", ShiftType.Night));

        Assert.Contains("DTSTAMP:20261020T013000Z", EventLines(ics, 0));
        Assert.Contains("UID:7-20261114-night@shiftplanner", EventLines(ics, 0));
        Assert.Equal(ics, Write(A(7, "2026-11-14", ShiftType.Night)));
    }

    [Fact]
    public void 只輸出該員工的班次()
    {
        var ics = Write(A(7, "2026-11-10"), A(8, "2026-11-10"));

        Assert.Equal(1, ics.Split("BEGIN:VEVENT").Length - 1);
    }

    [Fact]
    public void 每行以CRLF結束且不超過75個位元組()
    {
        var longName = new Employee(7, new string('長', 40), ContractType.FullTime, 40, Skills.Chinese, ShiftTypes.All);

        var ics = ICalendarWriter.Write(longName, [A(7, "2026-11-10")], GeneratedAt, "名稱,含;特殊字元");

        Assert.EndsWith("END:VCALENDAR\r\n", ics);
        Assert.DoesNotContain("\n", ics.Replace("\r\n", ""));
        Assert.All(ics.Split("\r\n"), line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, line));
        Assert.Contains(@"X-WR-CALNAME:名稱\,含\;特殊字元", ics);
    }
}
