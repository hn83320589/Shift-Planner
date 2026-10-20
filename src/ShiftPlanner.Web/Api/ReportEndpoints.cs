using ShiftPlanner.Core.Export;
using ShiftPlanner.Web.Application;

namespace ShiftPlanner.Web.Api;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportApi(this IEndpointRouteBuilder app)
    {
        var reports = app.MapGroup("/api/reports/{year:int}/{month:int:range(1,12)}");

        reports.MapGet("/", async (int year, int month, ReportService service, CancellationToken ct) =>
            await service.GetMonthlyReportAsync(year, month, ct) is { } report ? Results.Ok(report) : Results.NotFound());

        reports.MapGet("/export.csv", async (int year, int month, ReportService service, CancellationToken ct) =>
            await service.GetMonthlyReportAsync(year, month, ct) is { } report
                ? Results.File(MonthlyReportCsv.Write(report), "text/csv; charset=utf-8", ReportService.CsvFileName(year, month))
                : Results.NotFound());

        app.MapGet("/api/employees/{employeeId:int}/schedules/{year:int}/{month:int:range(1,12)}/calendar.ics",
            async (int employeeId, int year, int month, ReportService service, CancellationToken ct) =>
            {
                var ics = await service.GetCalendarAsync(employeeId, year, month, ct);
                return Results.File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8",
                    ReportService.CalendarFileName(employeeId, year, month));
            })
            .AddEndpointFilter(DomainErrorFilter.InvokeAsync);

        return app;
    }
}
