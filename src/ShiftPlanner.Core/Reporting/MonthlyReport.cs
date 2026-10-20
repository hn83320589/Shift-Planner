using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Core.Reporting;

public sealed record EmployeeReportRow(
    int EmployeeId,
    string Name,
    ContractType ContractType,
    int Shifts,
    double TotalHours,
    int NightShifts,
    int WeekendShifts,
    int StrongRequested,
    int StrongSatisfied,
    int NormalRequested,
    int NormalSatisfied)
{
    /// <summary>此人所有休假偏好的滿足率；沒有登記偏好時為 null。</summary>
    public double? PreferenceSatisfaction => Ratio(StrongSatisfied + NormalSatisfied, StrongRequested + NormalRequested);

    internal static double? Ratio(int satisfied, int requested) => requested == 0 ? null : (double)satisfied / requested;
}

public sealed record UnsatisfiedPreference(int EmployeeId, string Name, DateOnly Date, PreferencePriority Priority, ShiftType AssignedShift);

/// <summary>
/// 月報與公平性指標。夜班、週末班的標準差只計算全職員工（母體標準差）。
/// 週末班 = 開始日為週六或週日的班。
/// </summary>
public sealed record MonthlyReport(
    int Year,
    int Month,
    IReadOnlyList<EmployeeReportRow> Rows,
    double NightShiftStdDev,
    double WeekendShiftStdDev,
    int NightShiftSpread,
    double? StrongSatisfaction,
    double? NormalSatisfaction,
    IReadOnlyList<UnsatisfiedPreference> Unsatisfied);

public static class MonthlyReportBuilder
{
    public static MonthlyReport Build(
        DateRange month,
        IReadOnlyList<Employee> employees,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyList<OffPreference> preferences,
        ShiftCatalog? catalog = null)
    {
        catalog ??= ShiftCatalog.Standard;
        var inMonth = assignments.Where(a => month.Contains(a.Date)).ToList();
        var byEmployee = inMonth.ToLookup(a => a.EmployeeId);
        var working = inMonth.ToDictionary(a => (a.EmployeeId, a.Date), a => a.Shift);
        var monthPreferences = preferences.Where(p => month.Contains(p.Date)).ToList();
        var names = employees.ToDictionary(e => e.Id, e => e.Name);

        var rows = employees.Select(e =>
        {
            var shifts = byEmployee[e.Id].ToList();
            var prefs = monthPreferences.Where(p => p.EmployeeId == e.Id).ToList();
            int Requested(PreferencePriority priority) => prefs.Count(p => p.Priority == priority);
            int Satisfied(PreferencePriority priority) => prefs.Count(p => p.Priority == priority && !working.ContainsKey((e.Id, p.Date)));

            return new EmployeeReportRow(
                e.Id, e.Name, e.ContractType,
                shifts.Count,
                shifts.Sum(a => catalog.HoursOf(a.Shift)),
                shifts.Count(a => a.Shift == ShiftType.Night),
                shifts.Count(a => a.Date.IsWeekend()),
                Requested(PreferencePriority.Strong), Satisfied(PreferencePriority.Strong),
                Requested(PreferencePriority.Normal), Satisfied(PreferencePriority.Normal));
        }).ToList();

        var fullTime = rows.Where(r => r.ContractType == ContractType.FullTime).ToList();
        var nightCapable = employees.Where(e => e.IsFullTime && e.CanWork(ShiftType.Night)).Select(e => e.Id).ToHashSet();
        var nights = fullTime.Where(r => nightCapable.Contains(r.EmployeeId)).Select(r => r.NightShifts).ToList();

        var unsatisfied = monthPreferences
            .Where(p => working.ContainsKey((p.EmployeeId, p.Date)))
            .OrderByDescending(p => p.Priority).ThenBy(p => p.Date).ThenBy(p => p.EmployeeId)
            .Select(p => new UnsatisfiedPreference(p.EmployeeId, names[p.EmployeeId], p.Date, p.Priority, working[(p.EmployeeId, p.Date)]))
            .ToList();

        return new MonthlyReport(
            month.Start.Year,
            month.Start.Month,
            rows,
            PopulationStdDev(fullTime.Select(r => (double)r.NightShifts)),
            PopulationStdDev(fullTime.Select(r => (double)r.WeekendShifts)),
            nights.Count == 0 ? 0 : nights.Max() - nights.Min(),
            EmployeeReportRow.Ratio(rows.Sum(r => r.StrongSatisfied), rows.Sum(r => r.StrongRequested)),
            EmployeeReportRow.Ratio(rows.Sum(r => r.NormalSatisfied), rows.Sum(r => r.NormalRequested)),
            unsatisfied);
    }

    public static double PopulationStdDev(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        var mean = list.Average();
        return Math.Sqrt(list.Sum(v => (v - mean) * (v - mean)) / list.Count);
    }
}
