using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Scheduling;

public enum GenerationStatus
{
    /// <summary>已證明為最佳解。</summary>
    Optimal,

    /// <summary>時間內找到可行解，但未證明最佳。</summary>
    Feasible,

    /// <summary>無可行解，Reasons 會說明原因。</summary>
    Infeasible,

    /// <summary>時間內沒有找到任何解，也沒有證明無解。</summary>
    TimedOut,
}

/// <summary>無解原因。Date / Shift 可能為 null（例如整週產能不足）。</summary>
public sealed record InfeasibilityReason(DateOnly? Date, ShiftType? Shift, int Required, int Available, string Message);

public sealed record GenerationResult(
    GenerationStatus Status,
    IReadOnlyList<Assignment> Assignments,
    IReadOnlyList<InfeasibilityReason> Reasons,
    TimeSpan Elapsed)
{
    public bool Succeeded => Status is GenerationStatus.Optimal or GenerationStatus.Feasible;
}
