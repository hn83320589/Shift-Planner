using ShiftPlanner.Core.Domain;
using ShiftPlanner.Data.Seed;

namespace ShiftPlanner.Scheduling.Tests;

/// <summary>
/// 月底不完整的週（10/26～11/1）：10 月只要求休 1 天，11/1 才補上第 2 天。
/// 若太多人在 10 月底只休 1 天，11/1 就排不出人。
/// </summary>
[Collection(SampleScheduleCollection.Name)]
public class MonthBoundaryWeekTests(GeneratedNovemberFixture fixture)
{
    private static readonly DateOnly Nov1 = new(2026, 11, 1);

    [Fact]
    public void 前月月底多數人只休1天_診斷指出11月1日因此必須休假的人數()
    {
        // 員工 1～10 在 10/26～10/30 連上 5 天，該週只剩 11/1 能補第 2 天休假
        var context = Enumerable.Range(1, 10)
            .SelectMany(id => Enumerable.Range(0, 5).Select(i => new Assignment(id, new DateOnly(2026, 10, 26).AddDays(i), ShiftType.Early)))
            .ToList();

        var result = new ShiftScheduleGenerator(TimeProvider.System).Generate(SampleScenario.NovemberRequest(context));

        Assert.Equal(GenerationStatus.Infeasible, result.Status);
        var reason = Assert.Single(result.Reasons, r => r.Date == Nov1 && r.Shift is null);
        Assert.Equal((8, 6), (reason.Required, reason.Available));
        Assert.Equal("11/1 共需要 8 人上班，但只有 6 人可排，其中 10 人因上個月月底的排班當天必須休假（每週休息日、連續上班或週工時限制）", reason.Message);
    }

    [Fact]
    public void 產生10月時_月底不完整的週為下個月預留休息日()
    {
        var october = fixture.OctoberResult.Assignments;
        var lastDays = new DateRange(new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 31));

        var onlyOneDayOff = SampleData.Employees
            .Where(e => lastDays.Days().Count(d => october.Any(a => a.EmployeeId == e.Id && a.Date == d)) >= 5)
            .Select(e => e.Name)
            .ToList();

        Assert.True(onlyOneDayOff.Count == 0, $"10/26～10/31 只休 1 天、11/1 被迫休假的人：{string.Join("、", onlyOneDayOff)}");
    }
}
