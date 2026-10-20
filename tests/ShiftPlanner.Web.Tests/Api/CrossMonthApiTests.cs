using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using ShiftPlanner.Core.Domain;
using ShiftPlanner.Data;
using ShiftPlanner.Data.Seed;
using ShiftPlanner.Web.Api;

namespace ShiftPlanner.Web.Tests.Api;

/// <summary>跨月：強制依序發布、重排時保護已發布的下個月。</summary>
public class CrossMonthApiTests
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = ShiftPlannerApiFactory.Json;

    [Fact]
    public async Task 上個月仍是草稿時不可發布本月_上個月發布並重新產生後即可發布()
    {
        using var factory = new ShiftPlannerApiFactory();
        var client = factory.CreateClient();
        (await client.PostAsync("/api/schedules/2026/11/generate", null)).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/schedules/2026/12/generate", null)).EnsureSuccessStatusCode();

        var blocked = await client.PostAsync("/api/schedules/2026/12/publish", null);

        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("2026/11 班表尚未發布", await blocked.Content.ReadAsStringAsync());
        Assert.Equal("Draft", (await factory.GetScheduleAsync(client, 2026, 12)).Status);

        (await client.PostAsync("/api/schedules/2026/11/publish", null)).EnsureSuccessStatusCode();
        // 12 月草稿是在 11 月定案前產生的，以 11 月為上下文重新產生後才發布
        (await client.PostAsync("/api/schedules/2026/12/generate", null)).EnsureSuccessStatusCode();
        var published = await client.PostAsync("/api/schedules/2026/12/publish", null);

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
    }

    [Fact]
    public async Task 十二月已發布後重排十一月底_兩個月都仍然零違規()
    {
        using var factory = new ShiftPlannerApiFactory();
        var client = factory.CreateClient();
        await GenerateAndPublishAsync(client, 11);
        await GenerateAndPublishAsync(client, 12);
        var november = await factory.GetScheduleAsync(client, 2026, 11);
        var lastNight = november.Assignments.First(a => a.Date == new DateOnly(2026, 11, 30) && a.Shift == ShiftType.Night);

        var response = await client.PostAsJsonAsync("/api/schedules/2026/11/reschedule-proposals",
            new CreateProposalRequest(lastNight.EmployeeId, new DateOnly(2026, 11, 28), new DateOnly(2026, 11, 30)), Json);
        var proposal = (await response.Content.ReadFromJsonAsync<ProposalResponse>(Json))!;
        var apply = await client.PostAsync($"/api/reschedule-proposals/{proposal.Id}/apply", null);

        Assert.Equal(HttpStatusCode.OK, apply.StatusCode);
        Assert.True((await factory.GetScheduleAsync(client, 2026, 11)).CanPublish);
        var december = await factory.GetScheduleAsync(client, 2026, 12);
        Assert.Empty(december.Violations);
    }

    [Fact]
    public async Task 套用會讓已發布的下個月違規的提案_被拒絕且兩個月都不變()
    {
        using var factory = new ShiftPlannerApiFactory();
        var client = factory.CreateClient();
        await GenerateAndPublishAsync(client, 11);
        await GenerateAndPublishAsync(client, 12);
        var november = await factory.GetScheduleAsync(client, 2026, 11);
        var december = await factory.GetScheduleAsync(client, 2026, 12);

        // 手動做出一份壞提案：把 11/30 的英文夜班換給「12/1 上早班、11/30 沒班」的另一位英文人員。
        // 只看 11 月本身是合法的（人數、技能、休息、連續上班都沒問題），只有和 12 月開頭一起看才違規。
        var nov30 = new DateOnly(2026, 11, 30);
        var english = SampleData.Employees.Where(e => e.HasSkill(Skills.English)).Select(e => e.Id).ToHashSet();
        var worked = november.Assignments.Select(a => (a.EmployeeId, a.Date)).ToHashSet();
        var night = november.Assignments.First(a => a.Date == nov30 && a.Shift == ShiftType.Night && english.Contains(a.EmployeeId));
        var victim = december.Assignments.First(a =>
            a.Date == new DateOnly(2026, 12, 1) && a.Shift == ShiftType.Early
            && english.Contains(a.EmployeeId) && SampleData.Employees.Single(e => e.Id == a.EmployeeId).IsFullTime
            && !worked.Contains((a.EmployeeId, nov30))
            && !Enumerable.Range(1, 6).All(i => worked.Contains((a.EmployeeId, nov30.AddDays(-i))))).EmployeeId;
        int proposalId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShiftPlannerDbContext>();
            var schedule = db.Schedules.Single(s => s.Year == 2026 && s.Month == 11);
            var bad = new RescheduleProposal(schedule.Id, schedule.Revision, night.EmployeeId, nov30, nov30,
                [new(ChangeKind.Removed, night.EmployeeId, nov30, ShiftType.Night), new(ChangeKind.Added, victim, nov30, ShiftType.Night)],
                factory.Time.GetUtcNow());
            db.RescheduleProposals.Add(bad);
            await db.SaveChangesAsync();
            proposalId = bad.Id;
        }

        var apply = await client.PostAsync($"/api/reschedule-proposals/{proposalId}/apply", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, apply.StatusCode);
        Assert.Contains("12/1 早班", await apply.Content.ReadAsStringAsync());
        Assert.Equal(november.Assignments.ToHashSet(), (await factory.GetScheduleAsync(client, 2026, 11)).Assignments.ToHashSet());
        Assert.Empty((await factory.GetScheduleAsync(client, 2026, 12)).Violations);
    }

    private static async Task GenerateAndPublishAsync(HttpClient client, int month)
    {
        (await client.PostAsync($"/api/schedules/2026/{month}/generate", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/schedules/2026/{month}/publish", null)).EnsureSuccessStatusCode();
    }
}
