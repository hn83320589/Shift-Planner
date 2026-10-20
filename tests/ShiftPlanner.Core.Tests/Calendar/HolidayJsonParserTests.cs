using ShiftPlanner.Core.Calendar;

using static ShiftPlanner.Core.Tests.TestData;

namespace ShiftPlanner.Core.Tests.Calendar;

public class HolidayJsonParserTests
{
    [Fact]
    public void 匯入2026年國定假日資料()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "holidays-2026.json"));

        var holidays = HolidayJsonParser.Parse(json);

        Assert.Contains(holidays, h => h.Date == D("2026-10-10") && h.Name == "國慶日");
        Assert.All(holidays, h => Assert.Equal(2026, h.Date.Year));
        Assert.DoesNotContain(holidays, h => h.Date.Month == 11); // 11 月沒有國定假日
    }

    [Fact]
    public void 未填isHoliday時預設為放假()
    {
        var holidays = HolidayJsonParser.Parse("""{ "days": [ { "date": "2026-01-01", "name": "元旦" } ] }""");

        Assert.True(Assert.Single(holidays).IsHoliday);
    }

    [Theory]
    [InlineData("""{ "days": [ { "date": "2026/01/01", "name": "元旦" } ] }""", "yyyy-MM-dd")]
    [InlineData("""{ "days": [ { "date": "2026-01-01" } ] }""", "缺少名稱")]
    [InlineData("""{ "days": [ { "date": "2026-01-01", "name": "a" }, { "date": "2026-01-01", "name": "b" } ] }""", "重複日期")]
    [InlineData("""{ "year": 2026 }""", "缺少 days")]
    [InlineData("not json", "格式錯誤")]
    public void 格式錯誤時拋出可辨識的訊息(string json, string expectedMessage)
    {
        var ex = Assert.Throws<FormatException>(() => HolidayJsonParser.Parse(json));

        Assert.Contains(expectedMessage, ex.Message);
    }
}
