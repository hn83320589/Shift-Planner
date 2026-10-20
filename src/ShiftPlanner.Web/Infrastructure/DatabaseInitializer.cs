using Microsoft.EntityFrameworkCore;

using ShiftPlanner.Data;
using ShiftPlanner.Data.Seed;
using ShiftPlanner.Web.Application;

namespace ShiftPlanner.Web.Infrastructure;

/// <summary>啟動時套用 migration；資料庫是空的就載入範例資料，並產生、發布 2026 年 10 月班表。</summary>
public static class DatabaseInitializer
{
    public const int SampleYear = 2026;
    public const int SamplePublishedMonth = 10;

    public static string HolidaysJsonPath => Path.Combine(AppContext.BaseDirectory, "data", "holidays-2026.json");

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<ShiftPlannerDbContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));

        await db.Database.MigrateAsync(cancellationToken);
        if (!await DatabaseSeeder.SeedReferenceDataAsync(db, HolidaysJsonPath, cancellationToken))
        {
            return;
        }

        logger.LogInformation("已載入範例資料");
        var schedules = provider.GetRequiredService<ScheduleService>();
        var result = await schedules.GenerateDraftAsync(SampleYear, SamplePublishedMonth, cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"範例 {SampleYear}/{SamplePublishedMonth} 班表產生失敗：{string.Join("；", result.Reasons.Select(r => r.Message))}");
        }

        await schedules.PublishAsync(SampleYear, SamplePublishedMonth, cancellationToken);
        logger.LogInformation("已產生並發布 {Year}/{Month} 範例班表，共 {Count} 班", SampleYear, SamplePublishedMonth, result.Assignments.Count);
    }
}
