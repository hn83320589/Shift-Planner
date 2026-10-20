using System.Globalization;
using System.Text.Json;

namespace ShiftPlanner.Core.Calendar;

/// <summary>
/// 解析國定假日 JSON：{ "year": 2026, "days": [ { "date": "2026-01-01", "name": "...", "isHoliday": true } ] }
/// </summary>
public static class HolidayJsonParser
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<Holiday> Parse(string json)
    {
        HolidayFile? file;
        try
        {
            file = JsonSerializer.Deserialize<HolidayFile>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"國定假日 JSON 格式錯誤：{ex.Message}", ex);
        }

        if (file?.Days is null)
        {
            throw new FormatException("國定假日 JSON 缺少 days 陣列");
        }

        var holidays = file.Days.Select((d, i) => ToHoliday(d, i)).ToList();
        var duplicate = holidays.GroupBy(h => h.Date).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new FormatException($"國定假日 JSON 有重複日期：{duplicate.Key:yyyy-MM-dd}");
        }

        return holidays;
    }

    private static Holiday ToHoliday(HolidayDay day, int index)
    {
        if (!DateOnly.TryParseExact(day.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new FormatException($"國定假日 JSON 第 {index + 1} 筆的日期「{day.Date}」不是 yyyy-MM-dd 格式");
        }

        if (string.IsNullOrWhiteSpace(day.Name))
        {
            throw new FormatException($"國定假日 JSON 第 {index + 1} 筆（{day.Date}）缺少名稱");
        }

        return new Holiday(date, day.Name, day.IsHoliday ?? true);
    }

    private sealed record HolidayFile(int Year, List<HolidayDay>? Days);

    private sealed record HolidayDay(string? Date, string? Name, bool? IsHoliday);
}
