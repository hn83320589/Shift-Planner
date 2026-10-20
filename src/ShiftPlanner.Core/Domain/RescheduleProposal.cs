namespace ShiftPlanner.Core.Domain;

public enum ProposalStatus
{
    Pending = 0,
    Applied = 1,
    Discarded = 2,
}

public enum ChangeKind
{
    Removed = 0,
    Added = 1,
}

public sealed record ProposedChange(ChangeKind Kind, int EmployeeId, DateOnly Date, ShiftType Shift)
{
    public Assignment ToAssignment() => new(EmployeeId, Date, Shift);
}

/// <summary>
/// 臨時重排的提案：記錄請假內容與所有被更動的班次，主管確認後才套用到已發布班表。
/// BaseRevision 用來偵測提案產生後班表是否又被改過。
/// </summary>
public sealed class RescheduleProposal
{
    private readonly List<ProposedChange> _changes = [];

    public RescheduleProposal(
        int scheduleId, int baseRevision, int employeeId, DateOnly leaveStart, DateOnly leaveEnd,
        IEnumerable<ProposedChange> changes, DateTimeOffset createdAt)
    {
        ScheduleId = scheduleId;
        BaseRevision = baseRevision;
        EmployeeId = employeeId;
        LeaveStart = leaveStart;
        LeaveEnd = leaveEnd;
        CreatedAt = createdAt;
        Status = ProposalStatus.Pending;
        _changes.AddRange(changes.OrderBy(c => c.Date).ThenBy(c => c.Shift).ThenBy(c => c.Kind));
    }

    private RescheduleProposal()
    {
    }

    public int Id { get; private set; }
    public int ScheduleId { get; private set; }
    public int BaseRevision { get; private set; }
    public int EmployeeId { get; private set; }
    public DateOnly LeaveStart { get; private set; }
    public DateOnly LeaveEnd { get; private set; }
    public ProposalStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<ProposedChange> Changes => _changes;

    public IEnumerable<Assignment> Removed => _changes.Where(c => c.Kind == ChangeKind.Removed).Select(c => c.ToAssignment());
    public IEnumerable<Assignment> Added => _changes.Where(c => c.Kind == ChangeKind.Added).Select(c => c.ToAssignment());

    public Leave Leave => new(EmployeeId, LeaveStart, LeaveEnd);

    public void MarkApplied()
    {
        EnsurePending();
        Status = ProposalStatus.Applied;
    }

    public void Discard()
    {
        EnsurePending();
        Status = ProposalStatus.Discarded;
    }

    private void EnsurePending()
    {
        if (Status != ProposalStatus.Pending)
        {
            throw new InvalidOperationException($"重排提案 #{Id} 已經{(Status == ProposalStatus.Applied ? "套用" : "捨棄")}，不能再變更");
        }
    }
}
