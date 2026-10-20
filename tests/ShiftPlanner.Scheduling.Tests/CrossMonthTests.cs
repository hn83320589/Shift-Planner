using ShiftPlanner.Core.Calendar;
using ShiftPlanner.Core.Domain;
using ShiftPlanner.Data.Seed;

namespace ShiftPlanner.Scheduling.Tests;

/// <summary>下個月已發布時，產生與重排都要把下個月開頭當成固定的限制。</summary>
public class CrossMonthTests(GeneratedNovemberFixture fixture) : IClassFixture<GeneratedNovemberFixture>
{
    private static readonly DateOnly Nov30 = new(2026, 11, 30);
    private readonly ShiftScheduleGenerator _generator = new(TimeProvider.System);

    [Fact]
    public void 十二月已發布後重排十一月底_十二月仍然零違規()
    {
        Assert.True(fixture.DecemberResult.Succeeded);
        var december = new FollowingContext(SampleScenario.December.End, fixture.DecemberResult.Assignments);
        var rescheduler = new Rescheduler(_generator);

        // 月底有班的人逐一請 11/28～11/30 的假：有解時兩個月都必須零違規，無解時必須說明原因。
        // 班表由 CP-SAT 產生、細節每次可能不同，所以不假設每種情況都有解，但至少要有幾種情況成功。
        var succeeded = 0;
        var employees = fixture.NovemberResult.Assignments
            .Where(a => a.Date >= new DateOnly(2026, 11, 28)).Select(a => a.EmployeeId).Distinct().Order().Take(6).ToList();
        foreach (var employeeId in employees)
        {
            var leave = new Leave(employeeId, new DateOnly(2026, 11, 28), Nov30);
            var result = rescheduler.Reschedule(new RescheduleRequest
            {
                Month = SampleScenario.November,
                Employees = SampleData.Employees,
                Calendar = fixture.NovemberRequest.Calendar,
                Published = fixture.NovemberResult.Assignments,
                FromDate = leave.Start,
                PreviousMonth = fixture.OctoberResult.Assignments,
                NextMonth = december,
                Leaves = [leave],
                Preferences = SampleData.NovemberPreferences,
            });

            if (!result.Succeeded)
            {
                Assert.NotEmpty(result.Reasons);
                continue;
            }

            succeeded++;
            var decemberValidation = SampleScenario.Validate(SampleScenario.DecemberRequest(result.Schedule), fixture.DecemberResult.Assignments);
            Assert.True(decemberValidation.Violations.Count == 0,
                $"員工 #{employeeId} 請假後 12 月出現違規：{string.Join("；", decemberValidation.Violations.Select(v => v.Message))}");
            var novemberValidation = SampleScenario.Validate(
                fixture.NovemberRequest with { Leaves = [leave], Following = december }, result.Schedule);
            Assert.Empty(novemberValidation.Violations);
        }

        Assert.True(succeeded >= 3, $"只有 {succeeded} 種請假情況有可行的重排");
    }

    [Fact]
    public void 下個月第一天上早班的人_月底不能排夜班_預檢指出原因()
    {
        var request = OneEmployeeOnNov30(ShiftType.Night);

        var result = _generator.Generate(request);

        Assert.Equal(GenerationStatus.Infeasible, result.Status);
        var reason = Assert.Single(result.Reasons);
        Assert.Equal((1, 0), (reason.Required, reason.Available));
        Assert.Contains("11/30 夜班需要 1 人", reason.Message);
    }

    [Fact]
    public void 下個月第一天上早班的人_月底排早班休息足夠可以產生()
    {
        var result = _generator.Generate(OneEmployeeOnNov30(ShiftType.Early));

        Assert.True(result.Succeeded);
        Assert.Equal([new Assignment(1, Nov30, ShiftType.Early)], result.Assignments);
    }

    private static GenerationRequest OneEmployeeOnNov30(ShiftType demanded) => new()
    {
        Horizon = new DateRange(Nov30, Nov30),
        Employees = [new Employee(1, "員工1", ContractType.FullTime, 40, Skills.Chinese, ShiftTypes.All)],
        Calendar = new StaffingCalendar([],
            [
                .. from dayType in Enum.GetValues<DayType>()
                   from shift in DisplayNames.AllShifts
                   select new StaffingRequirement(dayType, shift, shift == demanded ? 1 : 0),
            ],
            []),
        Following = new FollowingContext(new DateOnly(2026, 12, 31), [new Assignment(1, new DateOnly(2026, 12, 1), ShiftType.Early)]),
    };
}
