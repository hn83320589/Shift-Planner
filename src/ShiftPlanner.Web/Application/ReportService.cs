using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Export;
using ShiftPlanner.Core.Reporting;

namespace ShiftPlanner.Web.Application;

/// <summary>月報與匯出。畫面、JSON、CSV 都由同一個 MonthlyReport 產生，數字必然一致。</summary>
public sealed class ReportService(ScheduleService schedules, TimeProvider time)
{
    public async Task<MonthlyReport?> GetMonthlyReportAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var state = await schedules.GetStateAsync(year, month, cancellationToken);
        return state.Exists
            ? MonthlyReportBuilder.Build(state.Data.Period, state.Data.Employees, state.Assignments, state.Data.Preferences)
            : null;
    }

    public async Task<bool> IsPublishedAsync(int year, int month, CancellationToken cancellationToken = default) =>
        (await schedules.GetStateAsync(year, month, cancellationToken)).IsPublished;

    /// <summary>員工個人班表（只匯出已發布的班表，避免員工拿到還在調整的草稿）。</summary>
    public async Task<string> GetCalendarAsync(int employeeId, int year, int month, CancellationToken cancellationToken = default)
    {
        var state = await schedules.GetStateAsync(year, month, cancellationToken);
        if (!state.IsPublished)
        {
            throw new InvalidOperationException($"{year}/{month} 班表尚未發布，無法匯出個人班表");
        }

        var employee = state.Data.Employees.SingleOrDefault(e => e.Id == employeeId)
            ?? throw new ArgumentException($"員工 Id {employeeId} 不存在");
        return ICalendarWriter.Write(employee, state.Assignments, time.GetUtcNow(), $"{employee.Name} {year}/{month} 班表");
    }

    public static string CsvFileName(int year, int month) => $"monthly-report-{year}-{month:00}.csv";

    public static string CalendarFileName(int employeeId, int year, int month) => $"shifts-{employeeId}-{year}-{month:00}.ics";
}
