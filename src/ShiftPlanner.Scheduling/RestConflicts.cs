using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;

namespace ShiftPlanner.Scheduling;

/// <summary>由班別時間推導出「前一班 → 隔 offset 天的後一班」休息不足 11 小時的組合。</summary>
internal static class RestConflicts
{
    public static IReadOnlyList<(int Offset, ShiftType Prev, ShiftType Next)> For(ShiftCatalog catalog)
    {
        var reference = new DateOnly(2000, 1, 3);
        var conflicts = new List<(int, ShiftType, ShiftType)>();
        for (var offset = 1; offset <= 2; offset++)
        {
            foreach (var prev in DisplayNames.AllShifts)
            {
                foreach (var next in DisplayNames.AllShifts)
                {
                    var prevEnd = catalog.GetInterval(new Assignment(0, reference, prev)).End;
                    var nextStart = catalog.GetInterval(new Assignment(0, reference.AddDays(offset), next)).Start;
                    if (nextStart - prevEnd < RestPeriodRule.DefaultMinimumRest)
                    {
                        conflicts.Add((offset, prev, next));
                    }
                }
            }
        }

        return conflicts;
    }
}
