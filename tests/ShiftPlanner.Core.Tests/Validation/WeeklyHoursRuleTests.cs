using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;

using static ShiftPlanner.Core.Tests.TestData;

namespace ShiftPlanner.Core.Tests.Validation;

public class WeeklyHoursRuleTests
{
    [Fact]
    public void 兼職上限24小時_排4班32小時_違規()
    {
        var result = Validate(Snapshot(Consecutive(1, "2026-11-02", 4), employees: [PartTime(1, 24)]));

        var violation = Assert.Single(result.ViolationsOf(RuleCode.WeeklyHours));
        Assert.Contains("32 小時", violation.Message);
    }

    [Fact]
    public void 兼職上限24小時_排3班剛好24小時_不違規()
    {
        var result = Validate(Snapshot(Consecutive(1, "2026-11-02", 3), employees: [PartTime(1, 24)]));

        Assert.Empty(result.ViolationsOf(RuleCode.WeeklyHours));
    }

    [Fact]
    public void 週日夜班的工時算在開始日那一週()
    {
        // 11/2～11/4 三班 + 11/8（日）夜班 = 32 小時，全部算在 11/2 那週
        var result = Validate(Snapshot(
            [.. Consecutive(1, "2026-11-02", 3), A(1, "2026-11-08", ShiftType.Night)],
            employees: [PartTime(1, 24)]));

        var violation = Assert.Single(result.ViolationsOf(RuleCode.WeeklyHours));
        Assert.Equal(D("2026-11-02"), violation.Date);
    }
}
