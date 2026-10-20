using ShiftPlanner.Core.Domain;
using ShiftPlanner.Data.Seed;

using Xunit.Abstractions;

namespace ShiftPlanner.Scheduling.Tests;

public class ShiftScheduleGeneratorTests(GeneratedNovemberFixture fixture, ITestOutputHelper output) : IClassFixture<GeneratedNovemberFixture>
{
    [Fact]
    public void 範例資料產生11月班表_60秒內完成且validator零違規()
    {
        var result = fixture.NovemberResult;
        output.WriteLine($"10 月：{fixture.OctoberResult.Status}，{fixture.OctoberResult.Elapsed.TotalSeconds:0.00} 秒");
        output.WriteLine($"11 月：{result.Status}，{result.Elapsed.TotalSeconds:0.00} 秒，{result.Assignments.Count} 班");

        Assert.True(result.Succeeded, string.Join("；", result.Reasons.Select(r => r.Message)));
        Assert.True(result.Elapsed < TimeSpan.FromSeconds(60), $"耗時 {result.Elapsed}");
        var validation = SampleScenario.Validate(fixture.NovemberRequest, result.Assignments);
        Assert.Empty(validation.Violations);
        Assert.Empty(validation.Gaps);
    }

    [Fact]
    public void 含國定假日的10月也能產生_且假日套用假日人力()
    {
        var result = fixture.OctoberResult;

        Assert.True(result.Succeeded);
        var restorationDayEarly = result.Assignments.Count(a => a.Date == new DateOnly(2026, 10, 26) && a.Shift == ShiftType.Early);
        Assert.Equal(3, restorationDayEarly); // 補假套用假日需求 3 人，且產生器不會無謂多排
    }

    [Fact]
    public void 全職員工之間的夜班數差距不超過2()
    {
        var nights = SampleData.Employees
            .Where(e => e.IsFullTime && e.CanWork(ShiftType.Night))
            .Select(e => fixture.NovemberResult.Assignments.Count(a => a.EmployeeId == e.Id && a.Shift == ShiftType.Night))
            .ToList();

        output.WriteLine($"全職夜班數：{string.Join(",", nights)}");
        Assert.True(nights.Max() - nights.Min() <= 2, $"夜班數：{string.Join(",", nights)}");
    }

    [Fact]
    public void 強烈休假偏好滿足率達90百分比以上()
    {
        var working = fixture.NovemberResult.Assignments.Select(a => (a.EmployeeId, a.Date)).ToHashSet();
        var strong = SampleData.NovemberPreferences.Where(p => p.Priority == PreferencePriority.Strong).ToList();

        var satisfied = strong.Count(p => !working.Contains((p.EmployeeId, p.Date)));

        output.WriteLine($"強烈偏好滿足 {satisfied}/{strong.Count}");
        Assert.True(satisfied >= strong.Count * 0.9, $"滿足 {satisfied}/{strong.Count}");
    }

    [Fact]
    public void 請假日不會被排班()
    {
        var request = SampleScenario.NovemberRequest(fixture.OctoberResult.Assignments) with
        {
            Leaves = [new Leave(1, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 6))],
        };

        var result = new ShiftScheduleGenerator(TimeProvider.System).Generate(request);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(result.Assignments, a => a.EmployeeId == 1 && a.Date.Day is >= 2 and <= 6);
    }
}
