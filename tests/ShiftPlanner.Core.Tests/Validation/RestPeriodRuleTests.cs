using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;

using static ShiftPlanner.Core.Tests.TestData;

namespace ShiftPlanner.Core.Tests.Validation;

public class RestPeriodRuleTests
{
    [Fact]
    public void 夜班接隔天早班_休息0小時_違規()
    {
        var result = Validate(Snapshot([A(1, "2026-11-10", ShiftType.Night), A(1, "2026-11-11", ShiftType.Early)]));

        var violation = Assert.Single(result.ViolationsOf(RuleCode.RestPeriod));
        Assert.Equal(D("2026-11-11"), violation.Date);
        Assert.Equal(ShiftType.Early, violation.Shift);
        Assert.Contains("只休息 0 小時", violation.Message);
    }

    [Fact]
    public void 晚班接隔天早班_休息8小時_違規()
    {
        var result = Validate(Snapshot([A(1, "2026-11-10", ShiftType.Late), A(1, "2026-11-11", ShiftType.Early)]));

        var violation = Assert.Single(result.ViolationsOf(RuleCode.RestPeriod));
        Assert.Contains("只休息 8 小時", violation.Message);
    }

    [Fact]
    public void 夜班接隔天晚班_休息8小時_違規()
    {
        var result = Validate(Snapshot([A(1, "2026-11-10", ShiftType.Night), A(1, "2026-11-11", ShiftType.Late)]));

        Assert.Single(result.ViolationsOf(RuleCode.RestPeriod));
    }

    [Theory]
    [InlineData(ShiftType.Early, ShiftType.Early)] // 16 小時
    [InlineData(ShiftType.Late, ShiftType.Late)]   // 16 小時
    [InlineData(ShiftType.Night, ShiftType.Night)] // 16 小時
    [InlineData(ShiftType.Early, ShiftType.Night)] // 32 小時
    [InlineData(ShiftType.Late, ShiftType.Night)]  // 24 小時
    public void 標準班別的合法組合_不違規(ShiftType first, ShiftType second)
    {
        var result = Validate(Snapshot([A(1, "2026-11-10", first), A(1, "2026-11-11", second)]));

        Assert.Empty(result.ViolationsOf(RuleCode.RestPeriod));
    }

    // 標準班別的合法組合最短是 16 小時，測不到「剛好 11 小時」，所以用自訂班別時間驗證邊界。
    [Fact]
    public void 休息剛好11小時_不違規()
    {
        var catalog = CatalogWithLateEndingAt(new TimeOnly(20, 0)); // 20:00 → 隔天 07:00 = 11h

        var result = Validate(Snapshot(
            [A(1, "2026-11-10", ShiftType.Late), A(1, "2026-11-11", ShiftType.Early)], catalog: catalog));

        Assert.Empty(result.ViolationsOf(RuleCode.RestPeriod));
    }

    [Fact]
    public void 休息差1分鐘才滿11小時_違規()
    {
        var catalog = CatalogWithLateEndingAt(new TimeOnly(20, 1)); // 10h59m

        var result = Validate(Snapshot(
            [A(1, "2026-11-10", ShiftType.Late), A(1, "2026-11-11", ShiftType.Early)], catalog: catalog));

        Assert.Single(result.ViolationsOf(RuleCode.RestPeriod));
    }

    [Fact]
    public void 前月最後一天夜班接本月第一天早班_跨月也會判定違規()
    {
        var result = Validate(Snapshot(
            [A(1, "2026-11-01", ShiftType.Early)],
            context: [A(1, "2026-10-31", ShiftType.Night)]));

        var violation = Assert.Single(result.ViolationsOf(RuleCode.RestPeriod));
        Assert.Equal(D("2026-11-01"), violation.Date);
    }

    private static ShiftCatalog CatalogWithLateEndingAt(TimeOnly end)
    {
        var start = end.AddHours(-8);
        return new ShiftCatalog(
        [
            ShiftCatalog.Standard[ShiftType.Early],
            new ShiftDefinition(ShiftType.Late, start, TimeSpan.FromHours(8)),
            ShiftCatalog.Standard[ShiftType.Night],
        ]);
    }
}
