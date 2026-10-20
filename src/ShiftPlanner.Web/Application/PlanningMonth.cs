using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Web.Application;

/// <summary>主管通常在月中排下個月的班，因此預設顯示「台北時間今天」的下個月。</summary>
public static class PlanningMonth
{
    public static (int Year, int Month) Default(TimeProvider time)
    {
        var next = TaipeiTime.Today(time).AddMonths(1);
        return (next.Year, next.Month);
    }
}
