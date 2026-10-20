namespace ShiftPlanner.Core.Validation;

/// <summary>
/// 獨立的班表驗證器：可以對任意班表執行，與產生器完全分開實作。
/// </summary>
public sealed class ScheduleValidator(IReadOnlyList<IHardRule> rules)
{
    private readonly CoverageChecker _coverage = new();

    public ScheduleValidator() : this(DefaultRules())
    {
    }

    public static IReadOnlyList<IHardRule> DefaultRules() =>
    [
        new RestPeriodRule(),
        new WeeklyDaysOffRule(),
        new ConsecutiveDaysRule(),
        new WeeklyHoursRule(),
        new AllowedShiftRule(),
        new OnePerDayRule(),
        new LeaveRule(),
    ];

    public ValidationResult Validate(ScheduleSnapshot snapshot)
    {
        var violations = rules
            .SelectMany(r => r.Check(snapshot))
            .OrderBy(v => v.Date)
            .ThenBy(v => v.EmployeeId)
            .ToList();
        return new ValidationResult(violations, _coverage.Check(snapshot).ToList());
    }
}
