using System.Globalization;
using System.Text;

using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Reporting;

namespace ShiftPlanner.Core.Export;

/// <summary>月報 CSV：UTF-8 含 BOM（Excel 才能正確辨識中文）、CRLF 換行、RFC 4180 引號規則。</summary>
public static class MonthlyReportCsv
{
    public static readonly string[] Headers =
    [
        "員工編號", "姓名", "合約類型", "班數", "總工時", "夜班數", "週末班數",
        "強烈偏好(滿足/登記)", "一般偏好(滿足/登記)", "休假偏好滿足率(%)",
    ];

    public static byte[] Write(MonthlyReport report)
    {
        var builder = new StringBuilder();
        AppendLine(builder, Headers);
        foreach (var row in report.Rows)
        {
            AppendLine(builder,
            [
                row.EmployeeId.ToString(CultureInfo.InvariantCulture),
                row.Name,
                row.ContractType.ToDisplayName(),
                row.Shifts.ToString(CultureInfo.InvariantCulture),
                ReportFormat.Hours(row.TotalHours),
                row.NightShifts.ToString(CultureInfo.InvariantCulture),
                row.WeekendShifts.ToString(CultureInfo.InvariantCulture),
                ReportFormat.Fraction(row.StrongSatisfied, row.StrongRequested),
                ReportFormat.Fraction(row.NormalSatisfied, row.NormalRequested),
                ReportFormat.Percent(row.PreferenceSatisfaction),
            ]);
        }

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(builder.ToString())];
    }

    private static void AppendLine(StringBuilder builder, IEnumerable<string> fields) =>
        builder.Append(string.Join(",", fields.Select(Escape))).Append("\r\n");

    private static string Escape(string field) =>
        field.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{field.Replace("\"", "\"\"")}\"" : field;
}
