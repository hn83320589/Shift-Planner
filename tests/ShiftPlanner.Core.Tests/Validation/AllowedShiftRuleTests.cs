using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;

using static ShiftPlanner.Core.Tests.TestData;

namespace ShiftPlanner.Core.Tests.Validation;

public class AllowedShiftRuleTests
{
    private static readonly Employee EarlyOrLateOnly = FullTime(1, allowed: ShiftTypes.Early | ShiftTypes.Late);

    [Fact]
    public void 不可上夜班的人被排夜班_違規()
    {
        var result = Validate(Snapshot([A(1, "2026-11-10", ShiftType.Night)], employees: [EarlyOrLateOnly]));

        var violation = Assert.Single(result.ViolationsOf(RuleCode.AllowedShift));
        Assert.Equal(ShiftType.Night, violation.Shift);
    }

    [Fact]
    public void 排在可上的班別_不違規()
    {
        var result = Validate(Snapshot(
            [A(1, "2026-11-10", ShiftType.Early), A(1, "2026-11-12", ShiftType.Late)],
            employees: [EarlyOrLateOnly]));

        Assert.Empty(result.ViolationsOf(RuleCode.AllowedShift));
    }
}
