using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using ShiftPlanner.Data;

namespace ShiftPlanner.Web.Tests;

/// <summary>每個測試一個 in-memory SQLite，連線開著資料庫就存在，並套用真正的 migration。</summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public SqliteTestDatabase()
    {
        _connection.Open();
        using var db = CreateContext();
        db.Database.Migrate();
    }

    public static string HolidaysJsonPath => Path.Combine(AppContext.BaseDirectory, "data", "holidays-2026.json");

    public ShiftPlannerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ShiftPlannerDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
