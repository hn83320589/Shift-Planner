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
    public static readonly DateRange December = DateRange.ForMonth(2026, 12);

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

    public static GenerationRequest DecemberRequest(IReadOnlyList<Assignment> novemberContext) => new()
    {
        Horizon = December,
        Employees = SampleData.Employees,
        Calendar = Calendar(),
        Context = novemberContext,
    };

    public static ValidationResult Validate(GenerationRequest request, IReadOnlyList<Assignment> assignments) =>
        new ScheduleValidator().Validate(new ScheduleSnapshot(
            request.Horizon, request.Employees, assignments, request.Calendar, request.Context, request.Leaves, request.Catalog,
            request.Following));
}

/// <summary>
/// 10、11、12 月只產生一次，由所有需要範例班表的測試類別共用。
/// 放在同一個 collection 中，這些吃 CPU 的求解測試會依序執行，不會同時搶 CPU 導致逾時。
/// </summary>
public sealed class GeneratedNovemberFixture
{
    public GeneratedNovemberFixture()
    {
        var generator = new ShiftScheduleGenerator(TimeProvider.System);
        OctoberResult = EnsureSucceeded("10 月", generator.Generate(SampleScenario.OctoberRequest()));
        NovemberRequest = SampleScenario.NovemberRequest(OctoberResult.Assignments);
        NovemberResult = EnsureSucceeded("11 月", generator.Generate(NovemberRequest));
        DecemberResult = EnsureSucceeded("12 月", generator.Generate(SampleScenario.DecemberRequest(NovemberResult.Assignments)));
    }

    /// <summary>範例班表產生失敗時立即說明原因，而不是讓後面的測試拿到空班表、出現誤導的錯誤。</summary>
    private static GenerationResult EnsureSucceeded(string month, GenerationResult result) =>
        result.Succeeded
            ? result
            : throw new InvalidOperationException(
                $"範例 {month}班表產生失敗：{result.Status}，耗時 {result.Elapsed.TotalSeconds:0.0} 秒，"
                + string.Join("；", result.Reasons.Select(r => r.Message)));

    public GenerationResult OctoberResult { get; }
    public GenerationRequest NovemberRequest { get; }
    public GenerationResult NovemberResult { get; }
    public GenerationResult DecemberResult { get; }
}

[CollectionDefinition(Name)]
public sealed class SampleScheduleCollection : ICollectionFixture<GeneratedNovemberFixture>
{
    public const string Name = "範例班表";
}
