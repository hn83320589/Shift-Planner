using ShiftPlanner.Core.Validation;

using static ShiftPlanner.Core.Tests.TestData;

namespace ShiftPlanner.Core.Tests.Validation;

public class ConsecutiveDaysRuleTests
{
    [Fact]
    public void 連續上班7天_違規()
    {
        // 11/5（四）～ 11/11（三），橫跨兩週，每週各自都休滿 2 天，只有連續上班規則會觸發
        var result = Validate(Snapshot(Consecutive(1, "2026-11-05", 7)));

        var violation = Assert.Single(result.ViolationsOf(RuleCode.ConsecutiveDays));
        Assert.Equal(D("2026-11-11"), violation.Date);
        Assert.Contains("連續上班 7 天", violation.Message);
    }

    [Fact]
    public void 連續上班剛好6天_不違規()
    {
        var result = Validate(Snapshot(Consecutive(1, "2026-11-05", 6)));

        Assert.Empty(result.ViolationsOf(RuleCode.ConsecutiveDays));
    }

    [Fact]
    public void 前月連續上班接到本月_合併計算()
    {
        var result = Validate(Snapshot(
            Consecutive(1, "2026-11-01", 2),
            context: Consecutive(1, "2026-10-27", 5)));

        var violation = Assert.Single(result.ViolationsOf(RuleCode.ConsecutiveDays));
        Assert.Equal(D("2026-11-02"), violation.Date);
    }

    [Fact]
    public void 只在前月內的連續上班_不在本次範圍報告()
    {
        var result = Validate(Snapshot([], context: Consecutive(1, "2026-10-20", 8)));

        Assert.Empty(result.ViolationsOf(RuleCode.ConsecutiveDays));
    }
}
