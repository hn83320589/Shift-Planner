using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;

using static ShiftPlanner.Core.Tests.TestData;

namespace ShiftPlanner.Core.Tests.Validation;

public class WeeklyDaysOffRuleTests
{
    // 2026/11/2（一）～ 11/8（日）是完整的一週
    [Fact]
    public void 一週上6天只休1天_違規()
    {
        var result = Validate(Snapshot(Consecutive(1, "2026-11-02", 6)));

        var violation = Assert.Single(result.ViolationsOf(RuleCode.WeeklyDaysOff));
        Assert.Equal(D("2026-11-02"), violation.Date);
        Assert.Contains("只有 1 天", violation.Message);
    }

    [Fact]
    public void 一週上5天剛好休2天_不違規()
    {
        var result = Validate(Snapshot(Consecutive(1, "2026-11-02", 5)));

        Assert.Empty(result.ViolationsOf(RuleCode.WeeklyDaysOff));
    }

    [Fact]
    public void 跨月的週會合併前月上下文計算()
    {
        // 10/26（一）～ 11/1（日）：10 月上 5 天，11/1 再上一天 → 只休 1 天
        var result = Validate(Snapshot(
            [A(1, "2026-11-01")],
            context: Consecutive(1, "2026-10-26", 5)));

        var violation = Assert.Single(result.ViolationsOf(RuleCode.WeeklyDaysOff));
        Assert.Equal(D("2026-11-01"), violation.Date);
    }

    [Fact]
    public void 月底不完整的週_只檢查已知天數()
    {
        // 11/30 是週一，該週其餘 6 天在範圍外，還有機會補足休息日
        var result = Validate(Snapshot([A(1, "2026-11-30")]));

        Assert.Empty(result.ViolationsOf(RuleCode.WeeklyDaysOff));
    }

    [Fact]
    public void 週日夜班隔天週一沒有班次開始_週一算作不排班日()
    {
        // 週一～週五上早班，週日上夜班：上班日是一～五、日，只休週六 → 違規
        var result = Validate(Snapshot([.. Consecutive(1, "2026-11-02", 5), A(1, "2026-11-08", ShiftType.Night)]));
        Assert.Single(result.ViolationsOf(RuleCode.WeeklyDaysOff));

        // 下一週只有週一凌晨是夜班的延續，沒有班次開始 → 不算上班日
        var nextWeek = Validate(Snapshot([A(1, "2026-11-08", ShiftType.Night), .. Consecutive(1, "2026-11-10", 5)]));
        Assert.Empty(nextWeek.ViolationsOf(RuleCode.WeeklyDaysOff));
    }
}
