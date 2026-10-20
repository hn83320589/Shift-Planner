using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

using ShiftPlanner.Web.Api;

namespace ShiftPlanner.Web.Tests;

/// <summary>每個 factory 使用獨立的暫存 SQLite 檔，啟動時跑真正的 migration 與範例資料（含已發布的 10 月）。</summary>
public sealed class ShiftPlannerApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"shiftplanner-test-{Guid.NewGuid():N}.db");

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.FromHours(8)));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:ShiftPlanner", $"Data Source={_databasePath}");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    public async Task<ScheduleResponse> GetScheduleAsync(HttpClient client, int year, int month) =>
        (await client.GetFromJsonAsync<ScheduleResponse>($"/api/schedules/{year}/{month}", Json))!;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }
}
