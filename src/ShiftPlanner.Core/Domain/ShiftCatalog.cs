namespace ShiftPlanner.Core.Domain;

public sealed record ShiftDefinition(ShiftType Type, TimeOnly Start, TimeSpan Duration);

/// <summary>
/// 班別的時間定義。規則一律透過這裡換算時間區間，不寫死班別組合，
/// 測試也能用自訂時間驗證邊界（例如剛好 11 小時休息）。
/// </summary>
public sealed class ShiftCatalog
{
    private readonly Dictionary<ShiftType, ShiftDefinition> _definitions;

    public ShiftCatalog(IEnumerable<ShiftDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(d => d.Type);
        var missing = DisplayNames.AllShifts.Where(s => !_definitions.ContainsKey(s)).ToList();
        if (missing.Count > 0)
        {
            throw new ArgumentException($"班別定義缺少：{string.Join("、", missing)}");
        }
    }

    public static ShiftCatalog Standard { get; } = new(
    [
        new ShiftDefinition(ShiftType.Early, new TimeOnly(7, 0), TimeSpan.FromHours(8)),
        new ShiftDefinition(ShiftType.Late, new TimeOnly(15, 0), TimeSpan.FromHours(8)),
        new ShiftDefinition(ShiftType.Night, new TimeOnly(23, 0), TimeSpan.FromHours(8)),
    ]);

    public ShiftDefinition this[ShiftType shift] => _definitions[shift];

    public double HoursOf(ShiftType shift) => _definitions[shift].Duration.TotalHours;

    /// <summary>班次的實際起訖時間（台北當地時間）。夜班結束於隔天。</summary>
    public (DateTime Start, DateTime End) GetInterval(Assignment assignment)
    {
        var definition = _definitions[assignment.Shift];
        var start = assignment.Date.ToDateTime(definition.Start);
        return (start, start + definition.Duration);
    }
}
