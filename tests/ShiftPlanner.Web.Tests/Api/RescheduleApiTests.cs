using System.Net;
using System.Net.Http.Json;

using ShiftPlanner.Core.Domain;
using ShiftPlanner.Web.Api;

namespace ShiftPlanner.Web.Tests.Api;

public class RescheduleApiTests
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = ShiftPlannerApiFactory.Json;
    private static readonly DateOnly LeaveStart = new(2026, 11, 16);
    private static readonly DateOnly LeaveEnd = new(2026, 11, 20);

    [Fact]
    public async Task 產生提案不會改動班表_確認套用後版本加1且1116之前完全不變()
    {
        using var factory = new ShiftPlannerApiFactory();
        var client = factory.CreateClient();
        var published = await PublishNovemberAsync(factory, client);
        var employeeId = BusiestEmployeeDuringLeave(published);

        var proposal = await ProposeAsync(client, employeeId);

        Assert.Equal("Pending", proposal.Status);
        Assert.Contains(proposal.Changes, c => c.Kind == ChangeKind.Removed && c.EmployeeId == employeeId);
        Assert.All(proposal.Changes, c => Assert.True(c.Date >= LeaveStart));
        var unchanged = await factory.GetScheduleAsync(client, 2026, 11);
        Assert.Equal(published.Assignments.ToHashSet(), unchanged.Assignments.ToHashSet());

        var apply = await client.PostAsync($"/api/reschedule-proposals/{proposal.Id}/apply", null);

        Assert.Equal(HttpStatusCode.OK, apply.StatusCode);
        var after = await factory.GetScheduleAsync(client, 2026, 11);
        Assert.Equal(("Published", 2), (after.Status, after.Revision));
        Assert.True(after.CanPublish); // 重新驗證（含新登記的請假）仍零違規、零缺口
        Assert.Equal(
            published.Assignments.Where(a => a.Date < LeaveStart).ToHashSet(),
            after.Assignments.Where(a => a.Date < LeaveStart).ToHashSet());
        Assert.DoesNotContain(after.Assignments, a => a.EmployeeId == employeeId && a.Date >= LeaveStart && a.Date <= LeaveEnd);
    }

    [Fact]
    public async Task 班表在提案後已被其他提案更新_舊提案不可套用()
    {
        using var factory = new ShiftPlannerApiFactory();
        var client = factory.CreateClient();
        var published = await PublishNovemberAsync(factory, client);
        var employeeId = BusiestEmployeeDuringLeave(published);
        var first = await ProposeAsync(client, employeeId);
        var second = await ProposeAsync(client, employeeId);

        await client.PostAsync($"/api/reschedule-proposals/{first.Id}/apply", null);
        var stale = await client.PostAsync($"/api/reschedule-proposals/{second.Id}/apply", null);

        Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);
        Assert.Contains("請重新產生提案", await stale.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task 捨棄的提案不可再套用()
    {
        using var factory = new ShiftPlannerApiFactory();
        var client = factory.CreateClient();
        var published = await PublishNovemberAsync(factory, client);
        var proposal = await ProposeAsync(client, BusiestEmployeeDuringLeave(published));

        await client.PostAsync($"/api/reschedule-proposals/{proposal.Id}/discard", null);
        var apply = await client.PostAsync($"/api/reschedule-proposals/{proposal.Id}/apply", null);

        Assert.Equal(HttpStatusCode.BadRequest, apply.StatusCode);
        Assert.Equal(1, (await factory.GetScheduleAsync(client, 2026, 11)).Revision);
    }

    [Fact]
    public async Task 草稿不需要臨時重排()
    {
        using var factory = new ShiftPlannerApiFactory();
        var client = factory.CreateClient();
        await client.PostAsync("/api/schedules/2026/11/generate", null);

        var response = await client.PostAsJsonAsync("/api/schedules/2026/11/reschedule-proposals",
            new CreateProposalRequest(1, LeaveStart, LeaveEnd), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 不能重排已經過去的日期_以TimeProvider判斷今天()
    {
        using var factory = new ShiftPlannerApiFactory();
        var client = factory.CreateClient();
        await PublishNovemberAsync(factory, client);
        factory.Time.SetUtcNow(new DateTimeOffset(2026, 11, 17, 1, 0, 0, TimeSpan.Zero)); // 台北 11/17 09:00

        var response = await client.PostAsJsonAsync("/api/schedules/2026/11/reschedule-proposals",
            new CreateProposalRequest(1, LeaveStart, LeaveEnd), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("2026-11-17", await response.Content.ReadAsStringAsync());
    }

    private static async Task<ScheduleResponse> PublishNovemberAsync(ShiftPlannerApiFactory factory, HttpClient client)
    {
        (await client.PostAsync("/api/schedules/2026/11/generate", null)).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/schedules/2026/11/publish", null)).EnsureSuccessStatusCode();
        return await factory.GetScheduleAsync(client, 2026, 11);
    }

    private static async Task<ProposalResponse> ProposeAsync(HttpClient client, int employeeId)
    {
        var response = await client.PostAsJsonAsync("/api/schedules/2026/11/reschedule-proposals",
            new CreateProposalRequest(employeeId, LeaveStart, LeaveEnd), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProposalResponse>(Json))!;
    }

    private static int BusiestEmployeeDuringLeave(ScheduleResponse schedule) =>
        schedule.Assignments
            .Where(a => a.Date >= LeaveStart && a.Date <= LeaveEnd)
            .GroupBy(a => a.EmployeeId)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .First().Key;
}
