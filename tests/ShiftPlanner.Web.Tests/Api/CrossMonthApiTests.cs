using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using ShiftPlanner.Core.Domain;
using ShiftPlanner.Data;
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
        var nightWorkers = november.Assignments
            .Where(a => a.Date == new DateOnly(2026, 11, 30) && a.Shift == ShiftType.Night).Select(a => a.EmployeeId).ToList();

        // 班表由 CP-SAT 產生，細節每次可能不同：逐一嘗試 11/30 夜班的人，第一個有可行重排的就套用
        HttpResponseMessage? apply = null;
        foreach (var employeeId in nightWorkers)
        {
            var response = await client.PostAsJsonAsync("/api/schedules/2026/11/reschedule-proposals",
                new CreateProposalRequest(employeeId, new DateOnly(2026, 11, 28), new DateOnly(2026, 11, 30)), Json);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode); // 無解時必須附原因
                continue;
            }

            var proposal = (await response.Content.ReadFromJsonAsync<ProposalResponse>(Json))!;
            apply = await client.PostAsync($"/api/reschedule-proposals/{proposal.Id}/apply", null);
            break;
        }

        Assert.NotNull(apply);
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

        // 手動做出一份壞提案：把 11/30 的夜班換給 12/1 上早班的人（夜班 → 隔天早班，休息 0 小時）。
        // 只有把已發布的 12 月開頭一起看才看得出這個違規；人選不依賴求解結果的細節，避免測試不穩定。
        var nov30 = new DateOnly(2026, 11, 30);
        var night = november.Assignments.First(a => a.Date == nov30 && a.Shift == ShiftType.Night);
        var victim = december.Assignments.First(a =>
            a.Date == new DateOnly(2026, 12, 1) && a.Shift == ShiftType.Early && a.EmployeeId != night.EmployeeId).EmployeeId;
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
