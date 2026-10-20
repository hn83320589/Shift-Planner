using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Scheduling;

public sealed record RescheduleRequest
{
    public required DateRange Month { get; init; }
    public required IReadOnlyList<Employee> Employees { get; init; }
    public required IStaffingCalendar Calendar { get; init; }

    /// <summary>目前已發布的整月班表。</summary>
    public required IReadOnlyList<Assignment> Published { get; init; }

    /// <summary>從這天起（含）才允許更動。</summary>
    public required DateOnly FromDate { get; init; }

    public IReadOnlyList<Assignment> PreviousMonth { get; init; } = [];

    /// <summary>所有請假，須包含這次新登記的請假。</summary>
    public IReadOnlyList<Leave> Leaves { get; init; } = [];

    public IReadOnlyList<OffPreference> Preferences { get; init; } = [];
}

public sealed record RescheduleResult(
    GenerationStatus Status,
    IReadOnlyList<Assignment> Removed,
    IReadOnlyList<Assignment> Added,
    IReadOnlyList<Assignment> Schedule,
    IReadOnlyList<InfeasibilityReason> Reasons)
{
    public bool Succeeded => Status is GenerationStatus.Optimal or GenerationStatus.Feasible;
}

/// <summary>
/// 臨時重排：FromDate 之前的班次（與前月）全部凍結為常數，只重解 FromDate 到月底；
/// 目標函數以「與原班表不同的格子數」為主要成本，讓其他人的班盡量不動。
/// </summary>
public sealed class Rescheduler(ShiftScheduleGenerator generator)
{
    public RescheduleResult Reschedule(RescheduleRequest request)
    {
        if (!request.Month.Contains(request.FromDate))
        {
            throw new ArgumentException($"重排起始日 {request.FromDate} 不在 {request.Month} 內");
        }

        var frozen = request.Published.Where(a => a.Date < request.FromDate).ToList();
        var original = request.Published.Where(a => a.Date >= request.FromDate).ToList();

        var result = generator.Generate(new GenerationRequest
        {
            Horizon = new DateRange(request.FromDate, request.Month.End),
            Employees = request.Employees,
            Calendar = request.Calendar,
            Context = [.. request.PreviousMonth, .. frozen],
            Leaves = request.Leaves,
            Preferences = request.Preferences,
            FairnessPeriod = request.Month,
            Baseline = original,
            // 夜班差距在重排時只當軟性目標：硬性要求會迫使更多人為了重新平衡而被更動
            MaxNightSpread = null,
        });

        if (!result.Succeeded)
        {
            return new RescheduleResult(result.Status, [], [], [], result.Reasons);
        }

        var removed = original.Except(result.Assignments).ToList();
        var added = result.Assignments.Except(original).ToList();
        return new RescheduleResult(result.Status, removed, added, [.. frozen, .. result.Assignments], []);
    }
}
