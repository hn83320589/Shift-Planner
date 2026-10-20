using Microsoft.EntityFrameworkCore;

using ShiftPlanner.Core.Calendar;
using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Data;

/// <summary>
/// 直接對應 Core 的領域型別，不另外維護一套資料表專用的類別。
/// Assignment 沒有自己的 Id（以值相等比較），在資料庫中以 shadow key 儲存。
/// </summary>
public sealed class ShiftPlannerDbContext(DbContextOptions<ShiftPlannerDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<StaffingRequirement> StaffingRequirements => Set<StaffingRequirement>();
    public DbSet<StaffingOverride> StaffingOverrides => Set<StaffingOverride>();
    public DbSet<SkillRequirement> SkillRequirements => Set<SkillRequirement>();
    public DbSet<Leave> Leaves => Set<Leave>();
    public DbSet<OffPreference> OffPreferences => Set<OffPreference>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<RescheduleProposal> RescheduleProposals => Set<RescheduleProposal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<Holiday>(e =>
        {
            e.HasKey(x => x.Date);
            e.Property(x => x.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<StaffingRequirement>().HasKey(x => new { x.DayType, x.Shift });
        modelBuilder.Entity<StaffingOverride>().HasKey(x => new { x.Date, x.Shift });
        modelBuilder.Entity<SkillRequirement>().HasKey(x => new { x.DayType, x.Shift, x.Skill });

        modelBuilder.Entity<Leave>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.EmployeeId);
        });

        modelBuilder.Entity<OffPreference>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
        });

        modelBuilder.Entity<Schedule>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Year, x.Month }).IsUnique();
            e.Ignore(x => x.Period);
            e.Ignore(x => x.IsPublished);

            // 刻意不在 (EmployeeId, Date) 設唯一索引：主管手動排出「同一天兩班」的草稿
            // 必須能儲存，validator 才能標示出來。
            e.OwnsMany(x => x.Assignments, a =>
            {
                a.ToTable("Assignments");
                a.WithOwner().HasForeignKey("ScheduleId");
                a.Property<int>("Id");
                a.HasKey("Id");
                a.HasIndex("ScheduleId", nameof(Assignment.Date));
            });
            e.Navigation(x => x.Assignments).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<RescheduleProposal>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne<Schedule>().WithMany().HasForeignKey(x => x.ScheduleId);
            e.Ignore(x => x.Removed);
            e.Ignore(x => x.Added);
            e.Ignore(x => x.Leave);
            e.OwnsMany(x => x.Changes, c =>
            {
                c.ToTable("ProposedChanges");
                c.WithOwner().HasForeignKey("ProposalId");
                c.Property<int>("Id");
                c.HasKey("Id");
            });
            e.Navigation(x => x.Changes).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
