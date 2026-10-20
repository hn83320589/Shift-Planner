using Microsoft.Extensions.DependencyInjection;

using ShiftPlanner.Core.Domain;
using ShiftPlanner.Web.Application;

namespace ShiftPlanner.Web.Tests.Api;

public class ScheduleServiceTests
{
    private static readonly DateOnly Day = new(2026, 11, 10);

    [Fact]
    public async Task 兩人交換當天的班_一次完成()
    {
        using var factory = new ShiftPlannerApiFactory();
        await RunAsync(factory, async s =>
        {
            await s.AddAsync(2026, 11, new Assignment(1, Day, ShiftType.Early));
            await s.AddAsync(2026, 11, new Assignment(2, Day, ShiftType.Night));
            await s.SwapAsync(2026, 11, Day, 1, 2);
        });

        var state = await RunAsync(factory, s => s.GetStateAsync(2026, 11));

        Assert.Equal(
            [new Assignment(1, Day, ShiftType.Night), new Assignment(2, Day, ShiftType.Early)],
            state.Assignments.OrderBy(a => a.EmployeeId));
    }

    [Fact]
    public async Task 已發布班表無法交換()
    {
        using var factory = new ShiftPlannerApiFactory();
        var october = await RunAsync(factory, s => s.GetStateAsync(2026, 10));
        var first = october.Assignments[0];

        await Assert.ThrowsAsync<ScheduleLockedException>(() =>
            RunAsync(factory, s => s.SwapAsync(2026, 10, first.Date, first.EmployeeId, first.EmployeeId == 1 ? 2 : 1)));
    }

    [Fact]
    public async Task 預設規劃月份為台北時間今天的下個月()
    {
        using var factory = new ShiftPlannerApiFactory();
        factory.Time.SetUtcNow(new DateTimeOffset(2026, 10, 31, 16, 30, 0, TimeSpan.Zero)); // 台北 11/1 00:30

        Assert.Equal((2026, 12), PlanningMonth.Default(factory.Time));
    }

    private static async Task<T> RunAsync<T>(ShiftPlannerApiFactory factory, Func<ScheduleService, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ScheduleService>());
    }

    private static async Task RunAsync(ShiftPlannerApiFactory factory, Func<ScheduleService, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<ScheduleService>());
    }
}
