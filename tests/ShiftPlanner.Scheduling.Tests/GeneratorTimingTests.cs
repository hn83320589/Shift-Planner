using ShiftPlanner.Core.Calendar;
using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Scheduling.Tests;

public class GeneratorTimingTests
{
    [Fact]
    public void 求解耗時由注入的TimeProvider計算()
    {
        var clock = new SteppingTimeProvider(TimeSpan.FromSeconds(1.5));
        var request = new GenerationRequest
        {
            Horizon = new DateRange(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 2)),
            Employees = [new Employee(1, "員工1", ContractType.FullTime, 40, Skills.Chinese, ShiftTypes.All)],
            Calendar = new StaffingCalendar([], [
                new(DayType.Weekday, ShiftType.Early, 1), new(DayType.Weekday, ShiftType.Late, 0), new(DayType.Weekday, ShiftType.Night, 0),
                new(DayType.Holiday, ShiftType.Early, 0), new(DayType.Holiday, ShiftType.Late, 0), new(DayType.Holiday, ShiftType.Night, 0),
            ], []),
        };

        var result = new ShiftScheduleGenerator(clock).Generate(request);

        Assert.True(result.Succeeded);
        Assert.Equal(TimeSpan.FromSeconds(1.5), result.Elapsed); // 開始與結束各讀一次時間戳記
    }

    /// <summary>每次讀取時間戳記就前進固定時間，用來確認計時走的是注入的時鐘。</summary>
    private sealed class SteppingTimeProvider(TimeSpan step) : TimeProvider
    {
        private long _calls;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _calls++ * step.Ticks;
    }
}
