using System.Globalization;

namespace ShiftPlanner.Core.Reporting;

/// <summary>畫面與 CSV 共用的數字格式，確保兩邊顯示的數字完全一致。</summary>
public static class ReportFormat
{
    public static string Hours(double hours) => hours.ToString("0.#", CultureInfo.InvariantCulture);

    public static string StdDev(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>百分比數值（不含 % 符號），沒有資料時為空字串。</summary>
    public static string Percent(double? ratio) =>
        ratio is { } r ? (r * 100).ToString("0.0", CultureInfo.InvariantCulture) : "";

    public static string Fraction(int satisfied, int requested) => requested == 0 ? "" : $"{satisfied}/{requested}";
}
