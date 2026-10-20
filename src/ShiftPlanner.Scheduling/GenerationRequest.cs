using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Scheduling;

/// <summary>產生班表的輸入。臨時重排也使用同一個模型，只是多了 Baseline 與較短的 Horizon。</summary>
public sealed record GenerationRequest
{
    public required DateRange Horizon { get; init; }
    public required IReadOnlyList<Employee> Employees { get; init; }
    public required IStaffingCalendar Calendar { get; init; }

    /// <summary>Horizon 開始之前已確定的班次（前月已發布班表、重排時凍結的部分），視為常數。</summary>
    public IReadOnlyList<Assignment> Context { get; init; } = [];

    /// <summary>Horizon 結束之後已確定的班次（已發布的下個月），視為常數。</summary>
    public FollowingContext? Following { get; init; }

    public IReadOnlyList<Leave> Leaves { get; init; } = [];
    public IReadOnlyList<OffPreference> Preferences { get; init; } = [];
    public ShiftCatalog Catalog { get; init; } = ShiftCatalog.Standard;

    /// <summary>公平性（夜班、週末班數）的統計期間，預設等於 Horizon。重排時設為整個月，凍結部分也會計入。</summary>
    public DateRange? FairnessPeriod { get; init; }

    /// <summary>重排時的原班表；目標函數會盡量少偏離它。</summary>
    public IReadOnlyList<Assignment> Baseline { get; init; } = [];

    /// <summary>全職且可上夜班者之間，夜班數最多與最少的差距上限；null 表示只當軟性目標。</summary>
    public int? MaxNightSpread { get; init; } = 2;

    public TimeSpan TimeLimit { get; init; } = TimeSpan.FromSeconds(30);
}
