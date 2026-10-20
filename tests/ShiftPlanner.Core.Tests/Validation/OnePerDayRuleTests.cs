using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;

using static ShiftPlanner.Core.Tests.TestData;

namespace ShiftPlanner.Core.Tests.Validation;

public class OnePerDayRuleTests
{
    [Fact]
    public void 同一天排早班和夜班_違規()
    {
        var result = Validate(Snapshot([A(1, "2026-11-10", ShiftType.Early), A(1, "2026-11-10", ShiftType.Night)]));

        var violations = result.ViolationsOf(RuleCode.OnePerDay);
        Assert.Equal(2, violations.Count); // 兩個格子都要標示
        Assert.All(violations, v => Assert.Equal(D("2026-11-10"), v.Date));
    }

    [Fact]
    public void 夜班和隔天的班屬於不同日期_不算同一天兩班()
    {
        var result = Validate(Snapshot([A(1, "2026-11-10", ShiftType.Night), A(1, "2026-11-11", ShiftType.Night)]));

        Assert.Empty(result.ViolationsOf(RuleCode.OnePerDay));
    }
}
