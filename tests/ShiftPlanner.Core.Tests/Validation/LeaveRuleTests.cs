using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;

using static ShiftPlanner.Core.Tests.TestData;

namespace ShiftPlanner.Core.Tests.Validation;

public class LeaveRuleTests
{
    private static readonly Leave Leave = new(1, D("2026-11-16"), D("2026-11-20"));

    [Fact]
    public void 請假最後一天被排班_違規()
    {
        var result = Validate(Snapshot([A(1, "2026-11-20")], leaves: [Leave]));

        Assert.Single(result.ViolationsOf(RuleCode.OnLeave));
    }

    [Fact]
    public void 請假結束隔天排班_不違規()
    {
        var result = Validate(Snapshot([A(1, "2026-11-21")], leaves: [Leave]));

        Assert.Empty(result.ViolationsOf(RuleCode.OnLeave));
    }
}
