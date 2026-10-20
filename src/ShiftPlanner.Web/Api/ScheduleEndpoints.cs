using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;
using ShiftPlanner.Scheduling;
using ShiftPlanner.Web.Application;

namespace ShiftPlanner.Web.Api;

public sealed record ScheduleResponse(
    int Year,
    int Month,
    string Status,
    int Revision,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<Assignment> Assignments,
    IReadOnlyList<Violation> Violations,
    IReadOnlyList<CoverageGap> Gaps,
    bool CanPublish)
{
    public static ScheduleResponse From(ScheduleState state) => new(
        state.Year, state.Month, state.Status?.ToString() ?? "None", state.Revision, state.PublishedAt,
        state.Assignments, state.Validation.Violations, state.Validation.Gaps, state.Validation.CanPublish);
}

public sealed record ReplaceAssignmentRequest(Assignment From, Assignment To);

public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapScheduleApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/schedules/{year:int}/{month:int:range(1,12)}")
            .AddEndpointFilter(DomainErrorFilter.InvokeAsync);

        group.MapGet("/", async (int year, int month, ScheduleService service, CancellationToken ct) =>
            ScheduleResponse.From(await service.GetStateAsync(year, month, ct)));

        group.MapPost("/generate", async (int year, int month, ScheduleService service, CancellationToken ct) =>
        {
            var result = await service.GenerateDraftAsync(year, month, ct);
            return result.Succeeded
                ? Results.Ok(ScheduleResponse.From(await service.GetStateAsync(year, month, ct)))
                : Results.Problem(
                    title: "找不到可行的班表",
                    detail: string.Join("；", result.Reasons.Select(r => r.Message)),
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    extensions: new Dictionary<string, object?> { ["reasons"] = result.Reasons });
        });

        group.MapPost("/assignments", async (int year, int month, Assignment assignment, ScheduleService service, CancellationToken ct) =>
        {
            await service.AddAsync(year, month, assignment, ct);
            return ScheduleResponse.From(await service.GetStateAsync(year, month, ct));
        });

        group.MapPut("/assignments", async (int year, int month, ReplaceAssignmentRequest request, ScheduleService service, CancellationToken ct) =>
        {
            await service.ReplaceAsync(year, month, request.From, request.To, ct);
            return ScheduleResponse.From(await service.GetStateAsync(year, month, ct));
        });

        group.MapDelete("/assignments", async (int year, int month, int employeeId, DateOnly date, ShiftType shift, ScheduleService service, CancellationToken ct) =>
        {
            await service.RemoveAsync(year, month, new Assignment(employeeId, date, shift), ct);
            return ScheduleResponse.From(await service.GetStateAsync(year, month, ct));
        });

        group.MapPost("/publish", async (int year, int month, ScheduleService service, CancellationToken ct) =>
        {
            await service.PublishAsync(year, month, ct);
            return ScheduleResponse.From(await service.GetStateAsync(year, month, ct));
        });

        return app;
    }
}

/// <summary>把領域例外轉成對應的 HTTP 狀態碼與 ProblemDetails。</summary>
internal static class DomainErrorFilter
{
    public static async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ScheduleLockedException ex)
        {
            return Results.Problem(title: "班表已發布", detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (SchedulePublishException ex)
        {
            return Results.Problem(
                title: "班表未通過驗證，無法發布",
                detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity,
                extensions: new Dictionary<string, object?>
                {
                    ["violations"] = ex.Validation.Violations,
                    ["gaps"] = ex.Validation.Gaps,
                });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.Problem(title: "請求無效", detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
