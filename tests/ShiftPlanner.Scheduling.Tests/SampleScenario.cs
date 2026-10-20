using ShiftPlanner.Core.Calendar;
using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;
using ShiftPlanner.Data.Seed;

namespace ShiftPlanner.Scheduling.Tests;

/// <summary>以範例資料組出 2026 年 10、11 月的產生請求。</summary>
internal static class SampleScenario
{
    public static readonly DateRange October = DateRange.ForMonth(2026, 10);
    public static readonly DateRange November = DateRange.ForMonth(2026, 11);

    private static readonly Lazy<IReadOnlyList<Holiday>> Holidays = new(() =>
        HolidayJsonParser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "holidays-2026.json"))));

    public static StaffingCalendar Calendar(IEnumerable<StaffingOverride>? overrides = null) =>
        new(Holidays.Value, SampleData.StaffingRequirements, SampleData.SkillRequirements, overrides);

    public static GenerationRequest OctoberRequest() => new()
    {
        Horizon = October,
        Employees = SampleData.Employees,
        Calendar = Calendar(),
    };

    public static GenerationRequest NovemberRequest(IReadOnlyList<Assignment> octoberContext, IStaffingCalendar? calendar = null) => new()
    {
        Horizon = November,
        Employees = SampleData.Employees,
        Calendar = calendar ?? Calendar(),
        Context = octoberContext,
        Preferences = SampleData.NovemberPreferences,
    };

    public static ValidationResult Validate(GenerationRequest request, IReadOnlyList<Assignment> assignments) =>
        new ScheduleValidator().Validate(new ScheduleSnapshot(
            request.Horizon, request.Employees, assignments, request.Calendar, request.Context, request.Leaves, request.Catalog));
}

/// <summary>10、11 月只產生一次，讓多個測試共用（求解需要數秒）。</summary>
public sealed class GeneratedNovemberFixture
{
    public GeneratedNovemberFixture()
    {
        var generator = new ShiftScheduleGenerator(TimeProvider.System);
        OctoberResult = generator.Generate(SampleScenario.OctoberRequest());
        NovemberRequest = SampleScenario.NovemberRequest(OctoberResult.Assignments);
        NovemberResult = generator.Generate(NovemberRequest);
    }

    public GenerationResult OctoberResult { get; }
    public GenerationRequest NovemberRequest { get; }
    public GenerationResult NovemberResult { get; }
}
