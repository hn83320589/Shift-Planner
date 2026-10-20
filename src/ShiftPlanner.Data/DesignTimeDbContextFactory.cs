using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShiftPlanner.Data;

/// <summary>讓 dotnet ef migrations 不需要啟動 Web 專案就能建立 DbContext。</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ShiftPlannerDbContext>
{
    public ShiftPlannerDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ShiftPlannerDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
