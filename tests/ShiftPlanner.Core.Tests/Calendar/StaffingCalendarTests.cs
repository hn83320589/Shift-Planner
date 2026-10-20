using ShiftPlanner.Core.Calendar;
using ShiftPlanner.Core.Domain;

using static ShiftPlanner.Core.Tests.TestData;

namespace ShiftPlanner.Core.Tests.Calendar;

public class StaffingCalendarTests
{
    private static readonly StaffingRequirement[] Requirements =
    [
        new(DayType.Weekday, ShiftType.Early, 4),
        new(DayType.Weekday, ShiftType.Late, 4),
        new(DayType.Weekday, ShiftType.Night, 2),
        new(DayType.Holiday, ShiftType.Early, 3),
        new(DayType.Holiday, ShiftType.Late, 3),
        new(DayType.Holiday, ShiftType.Night, 2),
    ];

    private static readonly SkillRequirement[] SkillRules =
    [
        new(DayType.Weekday, ShiftType.Early, Skills.Japanese, 1),
        new(DayType.Weekday, ShiftType.Late, Skills.Japanese, 1),
    ];

    private static StaffingCalendar Create(IEnumerable<Holiday>? holidays = null, IEnumerable<StaffingOverride>? overrides = null) =>
        new(holidays ?? LoadHolidays2026(), Requirements, SkillRules, overrides);

    private static IReadOnlyList<Holiday> LoadHolidays2026() =>
        HolidayJsonParser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "holidays-2026.json")));

    [Fact]
    public void 國定假日補假落在週一_套用假日人力需求()
    {
        var calendar = Create();
        var restorationDayObserved = D("2026-10-26"); // 週一，光復節補假

        Assert.Equal(DayType.Holiday, calendar.GetDayType(restorationDayObserved));
        Assert.Equal(3, calendar.GetMinStaff(restorationDayObserved, ShiftType.Early));
        Assert.Equal(2, calendar.GetMinStaff(restorationDayObserved, ShiftType.Night));
    }

    [Fact]
    public void 一般平日_套用平日人力需求()
    {
        var calendar = Create();

        Assert.Equal(DayType.Weekday, calendar.GetDayType(D("2026-10-27")));
        Assert.Equal(4, calendar.GetMinStaff(D("2026-10-27"), ShiftType.Early));
    }

    [Fact]
    public void 週末沒有登錄在行事曆_仍視為假日()
    {
        var calendar = Create();

        Assert.Equal(DayType.Holiday, calendar.GetDayType(D("2026-11-14")));
        Assert.Equal(3, calendar.GetMinStaff(D("2026-11-14"), ShiftType.Late));
    }

    [Fact]
    public void 補班日即使是週六_也套用平日人力需求()
    {
        var calendar = Create([new Holiday(D("2026-11-14"), "補班", IsHoliday: false)]);

        Assert.Equal(DayType.Weekday, calendar.GetDayType(D("2026-11-14")));
        Assert.Equal(4, calendar.GetMinStaff(D("2026-11-14"), ShiftType.Early));
    }

    [Fact]
    public void 日文需求只在平日早班與晚班_國定假日不需要()
    {
        var calendar = Create();

        Assert.Single(calendar.GetSkillRequirements(D("2026-10-27"), ShiftType.Early));
        Assert.Empty(calendar.GetSkillRequirements(D("2026-10-27"), ShiftType.Night));
        Assert.Empty(calendar.GetSkillRequirements(D("2026-10-26"), ShiftType.Early)); // 補假
    }

    [Fact]
    public void 單日覆寫優先於一般需求()
    {
        var calendar = Create(overrides: [new StaffingOverride(D("2026-11-14"), ShiftType.Night, 20)]);

        Assert.Equal(20, calendar.GetMinStaff(D("2026-11-14"), ShiftType.Night));
        Assert.Equal(2, calendar.GetMinStaff(D("2026-11-15"), ShiftType.Night));
    }

    [Fact]
    public void 人力需求缺少任何一種組合_建立時就報錯()
    {
        var ex = Assert.Throws<ArgumentException>(() => new StaffingCalendar([], Requirements[..5], []));

        Assert.Contains("Holiday/Night", ex.Message);
    }
}
