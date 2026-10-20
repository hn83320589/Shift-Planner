using System.Text;

using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Export;
using ShiftPlanner.Core.Reporting;

using static ShiftPlanner.Core.Tests.TestData;

namespace ShiftPlanner.Core.Tests.Export;

public class MonthlyReportCsvTests
{
    private static MonthlyReport SampleReport() => MonthlyReportBuilder.Build(
        November,
        [new Employee(1, "王小明", ContractType.FullTime, 40, Skills.Chinese, ShiftTypes.All),
         new Employee(2, "Lin, \"Amy\"", ContractType.PartTime, 24, Skills.English, ShiftTypes.Early)],
        [A(1, "2026-11-07", ShiftType.Night), A(1, "2026-11-09"), A(2, "2026-11-10")],
        [new OffPreference(1, D("2026-11-07"), PreferencePriority.Strong), new OffPreference(1, D("2026-11-20"), PreferencePriority.Normal)]);

    [Fact]
    public void 以UTF8_BOM開頭_Excel開啟中文不亂碼()
    {
        var bytes = MonthlyReportCsv.Write(SampleReport());

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.StartsWith("員工編號,姓名,合約類型", Encoding.UTF8.GetString(bytes[3..]));
    }

    [Fact]
    public void 每一列數字與月報物件格式化結果一致()
    {
        var report = SampleReport();

        var lines = Encoding.UTF8.GetString(MonthlyReportCsv.Write(report)[3..]).Split("\r\n");

        var row = report.Rows[0];
        Assert.Equal(
            $"1,王小明,全職,2,{ReportFormat.Hours(row.TotalHours)},1,1,0/1,1/1,{ReportFormat.Percent(row.PreferenceSatisfaction)}",
            lines[1]);
        Assert.Equal("1,王小明,全職,2,16,1,1,0/1,1/1,50.0", lines[1]);
    }

    [Fact]
    public void 含逗號與引號的欄位依RFC4180加引號()
    {
        var lines = Encoding.UTF8.GetString(MonthlyReportCsv.Write(SampleReport())[3..]).Split("\r\n");

        Assert.StartsWith("2,\"Lin, \"\"Amy\"\"\",兼職,1,8,0,0,,,", lines[2]);
        Assert.Equal("", lines[^1]); // 以 CRLF 結尾
    }
}
