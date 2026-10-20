using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;

using static ShiftPlanner.Core.Tests.TestData;

namespace ShiftPlanner.Core.Tests.Validation;

/// <summary>下個月已發布時，本月的驗證也要往後檢查跨月的限制。</summary>
public class FollowingContextTests
{
    [Fact]
    public void 月底夜班接下個月第一天早班_在本月標示休息不足()
    {
        var result = Validate(Snapshot(
            [A(1, "2026-11-30", ShiftType.Night)],
            following: December(A(1, "2026-12-01", ShiftType.Early))));

        var violation = Assert.Single(result.ViolationsOf(RuleCode.RestPeriod));
        Assert.Equal((D("2026-11-30"), ShiftType.Night), (violation.Date, violation.Shift));
        Assert.Contains("12/1 早班", violation.Message);
    }

    [Fact]
    public void 月底早班接下個月第一天早班_休息足夠不違規()
    {
        var result = Validate(Snapshot(
            [A(1, "2026-11-30", ShiftType.Early)],
            following: December(A(1, "2026-12-01", ShiftType.Early))));

        Assert.Empty(result.ViolationsOf(RuleCode.RestPeriod));
    }

    [Fact]
    public void 跨月的週加上下個月的班_休息日不足()
    {
        // 11/30（一）上班，下個月 12/1～12/5 再上 5 天 → 11/30～12/6 這週只休 1 天
        var result = Validate(Snapshot(
            [A(1, "2026-11-30")],
            following: December(Consecutive(1, "2026-12-01", 5))));

        var violation = Assert.Single(result.ViolationsOf(RuleCode.WeeklyDaysOff));
        Assert.Equal(D("2026-11-30"), violation.Date);
    }

    [Fact]
    public void 跨月的週下個月有排休_不違規()
    {
        var result = Validate(Snapshot(
            [A(1, "2026-11-30")],
            following: December(Consecutive(1, "2026-12-01", 4))));

        Assert.Empty(result.ViolationsOf(RuleCode.WeeklyDaysOff));
    }

    [Fact]
    public void 連續上班延續到下個月超過6天_在本月最後一天標示()
    {
        var result = Validate(Snapshot(
            Consecutive(1, "2026-11-27", 4),
            following: December(Consecutive(1, "2026-12-01", 3))));

        var violation = Assert.Single(result.ViolationsOf(RuleCode.ConsecutiveDays));
        Assert.Equal(D("2026-11-30"), violation.Date);
        Assert.Contains("11/27–12/3 連續上班 7 天", violation.Message);
    }

    [Fact]
    public void 跨月的週工時加上下個月的班超過上限()
    {
        var result = Validate(Snapshot(
            [A(1, "2026-11-30")],
            employees: [PartTime(1, 24)],
            following: December(Consecutive(1, "2026-12-01", 3))));

        Assert.Single(result.ViolationsOf(RuleCode.WeeklyHours));
    }

    [Fact]
    public void 只在下個月內的違規_不在本月報告()
    {
        var result = Validate(Snapshot([], following: December(Consecutive(1, "2026-12-07", 7))));

        Assert.Empty(result.Violations);
    }

    [Fact]
    public void 後續上下文的班次必須晚於驗證範圍()
    {
        Assert.Throws<ArgumentException>(() => Snapshot([], following: December(A(1, "2026-11-30"))));
    }
}
