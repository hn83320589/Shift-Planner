using Microsoft.EntityFrameworkCore;

using ShiftPlanner.Core.Domain;
using ShiftPlanner.Data.Seed;

namespace ShiftPlanner.Web.Tests.Data;

public class DatabaseTests
{
    [Fact]
    public async Task 空資料庫載入範例資料_16位員工含12全職4兼職()
    {
        using var database = new SqliteTestDatabase();
        await using (var db = database.CreateContext())
        {
            Assert.True(await DatabaseSeeder.SeedReferenceDataAsync(db, SqliteTestDatabase.HolidaysJsonPath));
        }

        await using var verify = database.CreateContext();
        var employees = await verify.Employees.ToListAsync();
        Assert.Equal(16, employees.Count);
        Assert.Equal(12, employees.Count(e => e.IsFullTime));
        Assert.Equal(6, await verify.StaffingRequirements.CountAsync());
        Assert.Contains(await verify.Holidays.ToListAsync(), h => h.Date == new DateOnly(2026, 10, 26));
        Assert.True(await verify.OffPreferences.CountAsync(p => p.Priority == PreferencePriority.Strong) >= 16);
    }

    [Fact]
    public async Task 已有資料時不重複載入()
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();
        await DatabaseSeeder.SeedReferenceDataAsync(db, SqliteTestDatabase.HolidaysJsonPath);

        Assert.False(await DatabaseSeeder.SeedReferenceDataAsync(db, SqliteTestDatabase.HolidaysJsonPath));
        Assert.Equal(16, await db.Employees.CountAsync());
    }

    [Fact]
    public async Task 班表與班次可以存回並讀出_包含同一天兩班的違規草稿()
    {
        using var database = new SqliteTestDatabase();
        await using (var db = database.CreateContext())
        {
            await DatabaseSeeder.SeedReferenceDataAsync(db, SqliteTestDatabase.HolidaysJsonPath);
            var schedule = new Schedule(2026, 11);
            schedule.Add(new Assignment(1, new DateOnly(2026, 11, 10), ShiftType.Early));
            schedule.Add(new Assignment(1, new DateOnly(2026, 11, 10), ShiftType.Night));
            db.Schedules.Add(schedule);
            await db.SaveChangesAsync();
        }

        await using var verify = database.CreateContext();
        var loaded = await verify.Schedules.SingleAsync(s => s.Year == 2026 && s.Month == 11);
        Assert.Equal(ScheduleStatus.Draft, loaded.Status);
        Assert.Equal(2, loaded.Assignments.Count);
        Assert.Contains(new Assignment(1, new DateOnly(2026, 11, 10), ShiftType.Night), loaded.Assignments);
    }

    [Fact]
    public async Task 從班表移除班次後存檔_資料庫同步刪除()
    {
        using var database = new SqliteTestDatabase();
        var target = new Assignment(1, new DateOnly(2026, 11, 10), ShiftType.Early);
        await using (var db = database.CreateContext())
        {
            await DatabaseSeeder.SeedReferenceDataAsync(db, SqliteTestDatabase.HolidaysJsonPath);
            var schedule = new Schedule(2026, 11);
            schedule.Add(target);
            schedule.Add(new Assignment(2, new DateOnly(2026, 11, 10), ShiftType.Early));
            db.Schedules.Add(schedule);
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateContext())
        {
            var schedule = await db.Schedules.SingleAsync();
            schedule.Remove(target);
            await db.SaveChangesAsync();
        }

        await using var verify = database.CreateContext();
        Assert.DoesNotContain(target, (await verify.Schedules.SingleAsync()).Assignments);
        Assert.Single((await verify.Schedules.SingleAsync()).Assignments);
    }
}
