using Microsoft.EntityFrameworkCore;

using ShiftPlanner.Data;
using ShiftPlanner.Data.Seed;

namespace ShiftPlanner.Web.Infrastructure;

/// <summary>啟動時套用 migration；資料庫是空的就載入範例資料。</summary>
public static class DatabaseInitializer
{
    public static string HolidaysJsonPath => Path.Combine(AppContext.BaseDirectory, "data", "holidays-2026.json");

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShiftPlannerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));

        await db.Database.MigrateAsync(cancellationToken);
        if (await DatabaseSeeder.SeedReferenceDataAsync(db, HolidaysJsonPath, cancellationToken))
        {
            logger.LogInformation("已載入範例資料");
        }
    }
}
