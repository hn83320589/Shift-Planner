using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;

using static ShiftPlanner.Core.Tests.TestData;

namespace ShiftPlanner.Core.Tests.Validation;

public class CoverageCheckerTests
{
    private static readonly DateRange OneDay = new(D("2026-11-10"), D("2026-11-10"));

    [Fact]
    public void 人數不足_回報缺口與需要和實際人數()
    {
        var result = Validate(Snapshot(
            [A(1, "2026-11-10", ShiftType.Early), A(2, "2026-11-10", ShiftType.Late), A(3, "2026-11-10", ShiftType.Night)],
            employees: [FullTime(1), FullTime(2), FullTime(3)],
            calendar: new FixedStaffingCalendar(minStaff: 2),
            horizon: OneDay));

        Assert.Equal(3, result.Gaps.Count);
        var early = result.Gaps.Single(g => g.Shift == ShiftType.Early);
        Assert.Equal((2, 1), (early.Required, early.Actual));
        Assert.Null(early.Skill);
        Assert.False(result.CanPublish);
    }

    [Fact]
    public void 人數夠但沒有會英文的人_回報技能缺口()
    {
        var result = Validate(Snapshot(
            [A(1, "2026-11-10", ShiftType.Early), A(2, "2026-11-10", ShiftType.Late), A(3, "2026-11-10", ShiftType.Night)],
            employees: [FullTime(1, Skills.Chinese | Skills.English), FullTime(2, Skills.Chinese | Skills.English), FullTime(3)],
            calendar: new FixedStaffingCalendar(1, Skills.English),
            horizon: OneDay));

        var gap = Assert.Single(result.Gaps);
        Assert.Equal((ShiftType.Night, Skills.English), (gap.Shift, gap.Skill));
        Assert.Contains("英文", gap.Message);
    }

    [Fact]
    public void 人數與技能都滿足且無違規_可以發布()
    {
        var result = Validate(Snapshot(
            [A(1, "2026-11-10", ShiftType.Early), A(2, "2026-11-10", ShiftType.Late), A(3, "2026-11-10", ShiftType.Night)],
            employees: [FullTime(1, Skills.English), FullTime(2, Skills.English), FullTime(3, Skills.English)],
            calendar: new FixedStaffingCalendar(1, Skills.English),
            horizon: OneDay));

        Assert.True(result.CanPublish);
    }
}
