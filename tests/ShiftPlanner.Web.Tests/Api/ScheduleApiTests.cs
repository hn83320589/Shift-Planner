using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using ShiftPlanner.Core.Calendar;
using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;
using ShiftPlanner.Data;
using ShiftPlanner.Web.Api;

namespace ShiftPlanner.Web.Tests.Api;

public class ScheduleApiTests
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = ShiftPlannerApiFactory.Json;

    [Fact]
    public async Task 啟動後10月班表已發布且零違規()
    {
        using var factory = new ShiftPlannerApiFactory();
        var client = factory.CreateClient();

        var october = await factory.GetScheduleAsync(client, 2026, 10);

        Assert.Equal("Published", october.Status);
        Assert.Equal(1, october.Revision);
        Assert.Equal(factory.Time.GetUtcNow(), october.PublishedAt);
        Assert.True(october.CanPublish);
    }

    [Fact]
    public async Task 已發布的班表_新增修改刪除與重新產生都回傳409且內容不變()
    {
        using var factory = new ShiftPlannerApiFactory();
        var client = factory.CreateClient();
        var before = await factory.GetScheduleAsync(client, 2026, 10);
        var existing = before.Assignments[0];
        var other = new Assignment(existing.EmployeeId == 1 ? 2 : 1, existing.Date, existing.Shift);

        HttpResponseMessage[] responses =
        [
            await client.PostAsJsonAsync("/api/schedules/2026/10/assignments", other, Json),
            await client.PutAsJsonAsync("/api/schedules/2026/10/assignments", new ReplaceAssignmentRequest(existing, other), Json),
            await client.DeleteAsync(
                $"/api/schedules/2026/10/assignments?employeeId={existing.EmployeeId}&date={existing.Date:yyyy-MM-dd}&shift={existing.Shift}"),
            await client.PostAsync("/api/schedules/2026/10/generate", null),
            await client.PostAsync("/api/schedules/2026/10/publish", null),
        ];

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        var after = await factory.GetScheduleAsync(client, 2026, 10);
        Assert.Equal(before.Assignments.ToHashSet(), after.Assignments.ToHashSet());
        Assert.Equal(1, after.Revision);
    }

    [Fact]
    public async Task 手動排出違規_標示違規且無法發布_修正後可以發布()
    {
        using var factory = new ShiftPlannerApiFactory();
        var client = factory.CreateClient();
        var draft = await (await client.PostAsync("/api/schedules/2026/11/generate", null))
            .Content.ReadFromJsonAsync<ScheduleResponse>(Json);
        Assert.True(draft!.CanPublish);

        // 找一位「某天夜班、隔天沒班」的人，硬把他排進隔天早班 → 休息 0 小時
        var working = draft.Assignments.Select(a => (a.EmployeeId, a.Date)).ToHashSet();
        var night = draft.Assignments.First(a =>
            a.Shift == ShiftType.Night && a.Date.Day < 30 && !working.Contains((a.EmployeeId, a.Date.AddDays(1))));
        var violating = new Assignment(night.EmployeeId, night.Date.AddDays(1), ShiftType.Early);

        var edited = await (await client.PostAsJsonAsync("/api/schedules/2026/11/assignments", violating, Json))
            .Content.ReadFromJsonAsync<ScheduleResponse>(Json);
        Assert.Contains(edited!.Violations, v =>
            v.Code == RuleCode.RestPeriod && v.EmployeeId == violating.EmployeeId && v.Date == violating.Date);
        Assert.False(edited.CanPublish);

        var rejected = await client.PostAsync("/api/schedules/2026/11/publish", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        Assert.Equal("Draft", (await factory.GetScheduleAsync(client, 2026, 11)).Status);

        await client.DeleteAsync(
            $"/api/schedules/2026/11/assignments?employeeId={violating.EmployeeId}&date={violating.Date:yyyy-MM-dd}&shift=Early");
        var published = await client.PostAsync("/api/schedules/2026/11/publish", null);

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Equal("Published", (await factory.GetScheduleAsync(client, 2026, 11)).Status);
    }

    [Fact]
    public async Task 刪班造成人力缺口_無法發布()
    {
        using var factory = new ShiftPlannerApiFactory();
        var client = factory.CreateClient();
        var draft = await (await client.PostAsync("/api/schedules/2026/11/generate", null))
            .Content.ReadFromJsonAsync<ScheduleResponse>(Json);
        var removed = draft!.Assignments[0];

        var edited = await (await client.DeleteAsync(
                $"/api/schedules/2026/11/assignments?employeeId={removed.EmployeeId}&date={removed.Date:yyyy-MM-dd}&shift={removed.Shift}"))
            .Content.ReadFromJsonAsync<ScheduleResponse>(Json);

        Assert.Empty(edited!.Violations);
        Assert.Contains(edited.Gaps, g => g.Date == removed.Date && g.Shift == removed.Shift);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync("/api/schedules/2026/11/publish", null)).StatusCode);
    }

    [Fact]
    public async Task 夜班需求超過可排人數_產生回傳422與具體原因()
    {
        using var factory = new ShiftPlannerApiFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShiftPlannerDbContext>();
            db.StaffingOverrides.Add(new StaffingOverride(new DateOnly(2026, 11, 14), ShiftType.Night, 20));
            await db.SaveChangesAsync();
        }

        var response = await factory.CreateClient().PostAsync("/api/schedules/2026/11/generate", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("11/14 夜班需要 20 人", await response.Content.ReadAsStringAsync());
    }
}
