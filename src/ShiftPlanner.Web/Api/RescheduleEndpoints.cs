using ShiftPlanner.Core.Domain;
using ShiftPlanner.Web.Application;

namespace ShiftPlanner.Web.Api;

public sealed record CreateProposalRequest(int EmployeeId, DateOnly LeaveStart, DateOnly LeaveEnd);

public sealed record ProposalResponse(
    int Id,
    int EmployeeId,
    DateOnly LeaveStart,
    DateOnly LeaveEnd,
    string Status,
    int BaseRevision,
    IReadOnlyList<ProposedChange> Changes)
{
    public static ProposalResponse From(RescheduleProposal p) =>
        new(p.Id, p.EmployeeId, p.LeaveStart, p.LeaveEnd, p.Status.ToString(), p.BaseRevision, p.Changes);
}

public static class RescheduleEndpoints
{
    public static IEndpointRouteBuilder MapRescheduleApi(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/schedules/{year:int}/{month:int:range(1,12)}/reschedule-proposals",
            async (int year, int month, CreateProposalRequest request, RescheduleService service, CancellationToken ct) =>
            {
                var outcome = await service.ProposeAsync(year, month, request.EmployeeId, request.LeaveStart, request.LeaveEnd, ct);
                return outcome.Proposal is { } proposal
                    ? Results.Ok(ProposalResponse.From(proposal))
                    : Results.Problem(
                        title: "找不到可行的重排",
                        detail: string.Join("；", outcome.Reasons.Select(r => r.Message)),
                        statusCode: StatusCodes.Status422UnprocessableEntity,
                        extensions: new Dictionary<string, object?> { ["reasons"] = outcome.Reasons });
            })
            .AddEndpointFilter(DomainErrorFilter.InvokeAsync);

        var proposals = app.MapGroup("/api/reschedule-proposals/{id:int}").AddEndpointFilter(DomainErrorFilter.InvokeAsync);

        proposals.MapGet("/", async (int id, RescheduleService service, CancellationToken ct) =>
            await service.FindAsync(id, ct) is { } proposal ? Results.Ok(ProposalResponse.From(proposal)) : Results.NotFound());

        proposals.MapPost("/apply", async (int id, RescheduleService service, CancellationToken ct) =>
        {
            await service.ApplyAsync(id, ct);
            return Results.Ok(ProposalResponse.From((await service.FindAsync(id, ct))!));
        });

        proposals.MapPost("/discard", async (int id, RescheduleService service, CancellationToken ct) =>
        {
            await service.DiscardAsync(id, ct);
            return Results.Ok(ProposalResponse.From((await service.FindAsync(id, ct))!));
        });

        return app;
    }
}
