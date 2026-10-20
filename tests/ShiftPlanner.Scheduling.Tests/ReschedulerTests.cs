using ShiftPlanner.Core.Domain;
using ShiftPlanner.Data.Seed;

using Xunit.Abstractions;

namespace ShiftPlanner.Scheduling.Tests;

public class ReschedulerTests(GeneratedNovemberFixture fixture, ITestOutputHelper output) : IClassFixture<GeneratedNovemberFixture>
{
    private static readonly DateOnly LeaveStart = new(2026, 11, 16);
    private static readonly DateOnly LeaveEnd = new(2026, 11, 20);

    private readonly Rescheduler _rescheduler = new(new ShiftScheduleGenerator());

    /// <summary>11/16～11/20 上最多班的人臨時請 5 天假。</summary>
    private (int EmployeeId, RescheduleRequest Request) ScenarioWithLeave()
    {
        var published = fixture.NovemberResult.Assignments;
        var employeeId = published
            .Where(a => a.Date >= LeaveStart && a.Date <= LeaveEnd)
            .GroupBy(a => a.EmployeeId)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .First().Key;

        return (employeeId, new RescheduleRequest
        {
            Month = SampleScenario.November,
            Employees = SampleData.Employees,
            Calendar = fixture.NovemberRequest.Calendar,
            Published = published,
            FromDate = LeaveStart,
            PreviousMonth = fixture.OctoberResult.Assignments,
            Leaves = [new Leave(employeeId, LeaveStart, LeaveEnd)],
            Preferences = SampleData.NovemberPreferences,
        });
    }

    [Fact]
    public void 請假5天_1116之前的班次完全沒變()
    {
        var (_, request) = ScenarioWithLeave();

        var result = _rescheduler.Reschedule(request);

        Assert.True(result.Succeeded, string.Join("；", result.Reasons.Select(r => r.Message)));
        var before = request.Published.Where(a => a.Date < LeaveStart).ToHashSet();
        var after = result.Schedule.Where(a => a.Date < LeaveStart).ToHashSet();
        Assert.Equal(before, after);
        Assert.All(result.Removed.Concat(result.Added), a => Assert.True(a.Date >= LeaveStart));
    }

    [Fact]
    public void 重排後整月仍零違規_且請假者在請假期間沒有班()
    {
        var (employeeId, request) = ScenarioWithLeave();

        var result = _rescheduler.Reschedule(request);

        var validation = SampleScenario.Validate(
            fixture.NovemberRequest with { Leaves = request.Leaves }, result.Schedule);
        Assert.Empty(validation.Violations);
        Assert.Empty(validation.Gaps);
        Assert.DoesNotContain(result.Schedule, a => a.EmployeeId == employeeId && a.Date >= LeaveStart && a.Date <= LeaveEnd);
    }

    [Fact]
    public void 列出所有更動的班次_且只動到必要的少數人()
    {
        var (employeeId, request) = ScenarioWithLeave();
        var leaveShifts = request.Published.Count(a => a.EmployeeId == employeeId && a.Date >= LeaveStart && a.Date <= LeaveEnd);

        var result = _rescheduler.Reschedule(request);

        output.WriteLine($"請假者在 11/16–20 原有 {leaveShifts} 班；移除 {result.Removed.Count}、新增 {result.Added.Count}");
        foreach (var a in result.Removed)
        {
            output.WriteLine($"  - {a.Date:MM/dd} {a.Shift} #{a.EmployeeId}");
        }

        foreach (var a in result.Added)
        {
            output.WriteLine($"  + {a.Date:MM/dd} {a.Shift} #{a.EmployeeId}");
        }

        // 請假者的班一定被移除，且都列在更動清單中
        Assert.Equal(leaveShifts, result.Removed.Count(a => a.EmployeeId == employeeId));
        // 新增的班至少要補上被移除的空缺
        Assert.True(result.Added.Count >= leaveShifts);
        var tail = request.Published.Count(a => a.Date >= LeaveStart);
        Assert.True(result.Removed.Count + result.Added.Count < tail / 2, "更動數量應遠少於重新產生整段班表");
    }
}
