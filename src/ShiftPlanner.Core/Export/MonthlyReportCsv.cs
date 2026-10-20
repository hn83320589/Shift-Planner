using System.Globalization;
using System.Text;

using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Reporting;

namespace ShiftPlanner.Core.Export;

/// <summary>
/// 月報 CSV：UTF-8 含 BOM（Excel 才能正確辨識中文）、CRLF 換行、RFC 4180 引號規則。
/// 除了姓名與合約類型，每個欄位都是純數字：Excel 會把「2/2」這類字串自動轉成日期。
/// </summary>
public static class MonthlyReportCsv
{
    public static readonly string[] Headers =
    [
        "員工編號", "姓名", "合約類型", "班數", "總工時", "夜班數", "週末班數",
        "強烈偏好滿足數", "強烈偏好登記數", "一般偏好滿足數", "一般偏好登記數", "休假偏好滿足率(%)",
    ];

    public static byte[] Write(MonthlyReport report)
    {
        var builder = new StringBuilder();
        AppendLine(builder, Headers);
        foreach (var row in report.Rows)
        {
            AppendLine(builder, Cells(row));
        }

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(builder.ToString())];
    }

    /// <summary>一列的顯示內容，順序對應 Headers。畫面上的月報表格也用這個方法，確保兩邊一致。</summary>
    public static IReadOnlyList<string> Cells(EmployeeReportRow row) =>
    [
        Number(row.EmployeeId),
        row.Name,
        row.ContractType.ToDisplayName(),
        Number(row.Shifts),
        ReportFormat.Hours(row.TotalHours),
        Number(row.NightShifts),
        Number(row.WeekendShifts),
        Number(row.StrongSatisfied),
        Number(row.StrongRequested),
        Number(row.NormalSatisfied),
        Number(row.NormalRequested),
        ReportFormat.Percent(row.PreferenceSatisfaction),
    ];

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static void AppendLine(StringBuilder builder, IEnumerable<string> fields) =>
        builder.Append(string.Join(",", fields.Select(Escape))).Append("\r\n");

    private static string Escape(string field) =>
        field.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{field.Replace("\"", "\"\"")}\"" : field;
}
