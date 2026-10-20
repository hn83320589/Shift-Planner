using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Reporting;

using static ShiftPlanner.Core.Tests.TestData;

namespace ShiftPlanner.Core.Tests.Reporting;

public class MonthlyReportTests
{
    private static readonly Employee[] Employees = [FullTime(1), FullTime(2), PartTime(3, 24)];

    // 11/7、11/8 為週末
    private static readonly Assignment[] Assignments =
    [
        A(1, "2026-11-02", ShiftType.Night), A(1, "2026-11-03", ShiftType.Night), A(1, "2026-11-07", ShiftType.Early),
        A(2, "2026-11-02", ShiftType.Early), A(2, "2026-11-08", ShiftType.Late),
        A(3, "2026-11-07", ShiftType.Night), A(3, "2026-11-08", ShiftType.Night),
    ];

    private static readonly OffPreference[] Preferences =
    [
        new(1, D("2026-11-07"), PreferencePriority.Strong), // 未滿足：當天上早班
        new(1, D("2026-11-10"), PreferencePriority.Strong),
        new(2, D("2026-11-04"), PreferencePriority.Normal),
        new(2, D("2026-11-08"), PreferencePriority.Normal), // 未滿足
    ];

    private static MonthlyReport Build() => MonthlyReportBuilder.Build(November, Employees, Assignments, Preferences);

    [Fact]
    public void 每人總工時夜班數週末班數()
    {
        var row = Build().Rows.Single(r => r.EmployeeId == 1);

        Assert.Equal((3, 24.0, 2, 1), (row.Shifts, row.TotalHours, row.NightShifts, row.WeekendShifts));
    }

    [Fact]
    public void 公平性標準差只計算全職員工()
    {
        var report = Build();

        // 全職夜班 [2, 0] → 母體標準差 1；週末班 [1, 1] → 0。兼職的 2 個夜班週末班不計入。
        Assert.Equal(1.0, report.NightShiftStdDev, precision: 10);
        Assert.Equal(0.0, report.WeekendShiftStdDev, precision: 10);
        Assert.Equal(2, report.NightShiftSpread);
    }

    [Fact]
    public void 休假偏好滿足率分強烈與一般_並列出未滿足項目()
    {
        var report = Build();

        Assert.Equal(0.5, report.StrongSatisfaction);
        Assert.Equal(0.5, report.NormalSatisfaction);
        Assert.Equal(0.5, report.Rows.Single(r => r.EmployeeId == 1).PreferenceSatisfaction);
        Assert.Null(report.Rows.Single(r => r.EmployeeId == 3).PreferenceSatisfaction);

        Assert.Collection(report.Unsatisfied,
            u => Assert.Equal((1, D("2026-11-07"), PreferencePriority.Strong, ShiftType.Early), (u.EmployeeId, u.Date, u.Priority, u.AssignedShift)),
            u => Assert.Equal((2, D("2026-11-08"), PreferencePriority.Normal), (u.EmployeeId, u.Date, u.Priority)));
    }
}
