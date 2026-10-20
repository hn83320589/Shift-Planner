namespace ShiftPlanner.Core.Domain;

/// <summary>客服中心位於台灣，所有「今天」與時刻都以台北時間解讀。</summary>
public static class TaipeiTime
{
    public const string TimeZoneId = "Asia/Taipei";

    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public static DateOnly Today(TimeProvider time) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), Zone).DateTime);
}
