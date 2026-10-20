using System.Net;
using System.Net.Http.Json;
using System.Text;

using ShiftPlanner.Core.Reporting;

namespace ShiftPlanner.Web.Tests.Api;

public class ReportApiTests
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = ShiftPlannerApiFactory.Json;

    [Fact]
    public async Task 月報CSV含BOM且每一列數字與月報資料一致()
    {
        using var factory = new ShiftPlannerApiFactory();
        var client = factory.CreateClient();
        (await client.PostAsync("/api/schedules/2026/11/generate", null)).EnsureSuccessStatusCode();

        var report = await client.GetFromJsonAsync<MonthlyReport>("/api/reports/2026/11", Json);
        var csv = await client.GetByteArrayAsync("/api/reports/2026/11/export.csv");

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, csv[..3]);
        var lines = Encoding.UTF8.GetString(csv[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(16, lines.Length - 1);
        foreach (var (row, line) in report!.Rows.Zip(lines.Skip(1)))
        {
            var fields = line.Split(',');
            Assert.Equal(row.Name, fields[1]);
            Assert.Equal(row.Shifts.ToString(), fields[3]);
            Assert.Equal(ReportFormat.Hours(row.TotalHours), fields[4]);
            Assert.Equal(row.NightShifts.ToString(), fields[5]);
            Assert.Equal(row.WeekendShifts.ToString(), fields[6]);
            Assert.Equal(ReportFormat.Percent(row.PreferenceSatisfaction), fields[9]);
        }
    }

    [Fact]
    public async Task 已發布班表可匯出個人ics_夜班結束在隔天0700()
    {
        using var factory = new ShiftPlannerApiFactory();
        var client = factory.CreateClient();
        var october = await factory.GetScheduleAsync(client, 2026, 10);
        var night = october.Assignments.First(a => a.Shift == Core.Domain.ShiftType.Night);

        var response = await client.GetAsync($"/api/employees/{night.EmployeeId}/schedules/2026/10/calendar.ics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/calendar", response.Content.Headers.ContentType!.MediaType);
        var ics = await response.Content.ReadAsStringAsync();
        Assert.Contains($"DTSTART;TZID=Asia/Taipei:{night.Date:yyyyMMdd}T230000", ics);
        Assert.Contains($"DTEND;TZID=Asia/Taipei:{night.Date.AddDays(1):yyyyMMdd}T070000", ics);
    }

    [Fact]
    public async Task 草稿班表不提供個人ics匯出()
    {
        using var factory = new ShiftPlannerApiFactory();
        var client = factory.CreateClient();
        await client.PostAsync("/api/schedules/2026/11/generate", null);

        var response = await client.GetAsync("/api/employees/1/schedules/2026/11/calendar.ics");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
