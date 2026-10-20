using Microsoft.EntityFrameworkCore;

using ShiftPlanner.Core.Calendar;

namespace ShiftPlanner.Data.Seed;

/// <summary>資料庫是空的時候，載入範例資料（員工、需求、國定假日、休假偏好）。</summary>
public static class DatabaseSeeder
{
    public static async Task<bool> SeedReferenceDataAsync(
        ShiftPlannerDbContext db, string holidaysJsonPath, CancellationToken cancellationToken = default)
    {
        if (await db.Employees.AnyAsync(cancellationToken))
        {
            return false;
        }

        var holidays = HolidayJsonParser.Parse(await File.ReadAllTextAsync(holidaysJsonPath, cancellationToken));

        db.Employees.AddRange(SampleData.Employees);
        db.StaffingRequirements.AddRange(SampleData.StaffingRequirements);
        db.SkillRequirements.AddRange(SampleData.SkillRequirements);
        db.Holidays.AddRange(holidays);
        db.OffPreferences.AddRange(SampleData.NovemberPreferences);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
