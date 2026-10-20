using ShiftPlanner.Core.Calendar;
using ShiftPlanner.Core.Domain;
using ShiftPlanner.Data.Seed;

namespace ShiftPlanner.Scheduling.Tests;

public class InfeasibilityDiagnosisTests(GeneratedNovemberFixture fixture) : IClassFixture<GeneratedNovemberFixture>
{
    private static readonly DateOnly Nov14 = new(2026, 11, 14);

    private readonly ShiftScheduleGenerator _generator = new(TimeProvider.System);

    [Fact]
    public void 夜班需求超過可排人數_原因包含日期班別需要人數與可用人數()
    {
        var calendar = SampleScenario.Calendar([new StaffingOverride(Nov14, ShiftType.Night, 20)]);

        var result = _generator.Generate(SampleScenario.NovemberRequest(fixture.OctoberResult.Assignments, calendar));

        Assert.Equal(GenerationStatus.Infeasible, result.Status);
        var reason = Assert.Single(result.Reasons, r => r.Date == Nov14 && r.Shift == ShiftType.Night);
        var nightCapable = SampleData.Employees.Count(e => e.CanWork(ShiftType.Night));
        Assert.Equal((20, nightCapable), (reason.Required, reason.Available));
        Assert.Equal($"11/14 夜班需要 20 人，但可上夜班、未請假且不違反休息規定的只有 {nightCapable} 人", reason.Message);
    }

    [Fact]
    public void 前月最後一天上夜班的人_不會被算進本月第一天早班的可用人數()
    {
        Employee[] employees = [Person(1), Person(2)];
        var calendar = OnlyEarlyShift(minStaff: 1);
        var request = new GenerationRequest
        {
            Horizon = new DateRange(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 1)),
            Employees = employees,
            Calendar = calendar,
            Context = [new Assignment(1, new DateOnly(2026, 10, 31), ShiftType.Night), new Assignment(2, new DateOnly(2026, 10, 31), ShiftType.Late)],
        };

        var result = _generator.Generate(request);

        var reason = Assert.Single(result.Reasons);
        Assert.Equal((1, 0), (reason.Required, reason.Available));
        Assert.Contains("11/1 早班需要 1 人", reason.Message);
    }

    [Fact]
    public void 會英文的人當天都請假_回報技能不足()
    {
        var englishSpeakers = SampleData.Employees.Where(e => e.HasSkill(Skills.English));
        var request = SampleScenario.NovemberRequest(fixture.OctoberResult.Assignments) with
        {
            Leaves = [.. englishSpeakers.Select(e => new Leave(e.Id, Nov14, Nov14))],
        };

        var result = _generator.Generate(request);

        Assert.Equal(GenerationStatus.Infeasible, result.Status);
        Assert.Contains(result.Reasons, r => r.Date == Nov14 && r.Message.Contains("需要 1 位會英文的人員") && r.Available == 0);
    }

    [Fact]
    public void 單格都排得到但受休息規定連鎖影響_由診斷模型指出排不滿的格子()
    {
        // 11/10 夜班 7 人：這些人 11/11 只能上夜班或休息，
        // 11/11 早晚班合計 10 人卻只剩 9 人可用 —— 單看任一格人數都夠，靜態預檢抓不到。
        var calendar = SampleScenario.Calendar(
        [
            new StaffingOverride(new DateOnly(2026, 11, 10), ShiftType.Night, 7),
            new StaffingOverride(new DateOnly(2026, 11, 11), ShiftType.Early, 5),
            new StaffingOverride(new DateOnly(2026, 11, 11), ShiftType.Late, 5),
        ]);

        var result = _generator.Generate(SampleScenario.NovemberRequest(fixture.OctoberResult.Assignments, calendar));

        Assert.Equal(GenerationStatus.Infeasible, result.Status);
        Assert.NotEmpty(result.Reasons);
        Assert.All(result.Reasons, r =>
        {
            Assert.NotNull(r.Date);
            Assert.True(r.Available < r.Required);
            Assert.Contains("最多只能排", r.Message);
        });
    }

    [Fact]
    public void 整週需求超過全體可上班數_回報該週產能不足()
    {
        var week = new DateRange(new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 15));
        var calendar = SampleScenario.Calendar(week.Days().Select(d => new StaffingOverride(d, ShiftType.Early, 7)));

        var result = _generator.Generate(SampleScenario.NovemberRequest(fixture.OctoberResult.Assignments, calendar));

        var reason = Assert.Single(result.Reasons, r => r.Date is null);
        Assert.StartsWith("11/9–11/15 這週共需要", reason.Message);
        Assert.True(reason.Required > reason.Available);
    }

    private static Employee Person(int id) =>
        new(id, $"員工{id}", ContractType.FullTime, 40, Skills.Chinese, ShiftTypes.All);

    private static StaffingCalendar OnlyEarlyShift(int minStaff) =>
        new(
            [],
            [
                new(DayType.Weekday, ShiftType.Early, minStaff), new(DayType.Weekday, ShiftType.Late, 0), new(DayType.Weekday, ShiftType.Night, 0),
                new(DayType.Holiday, ShiftType.Early, minStaff), new(DayType.Holiday, ShiftType.Late, 0), new(DayType.Holiday, ShiftType.Night, 0),
            ],
            []);
}
