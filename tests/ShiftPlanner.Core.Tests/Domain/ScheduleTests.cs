using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;

using static ShiftPlanner.Core.Tests.TestData;

namespace ShiftPlanner.Core.Tests.Domain;

public class ScheduleTests
{
    private static readonly ValidationResult Clean = new([], []);
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void 有硬性違規時不可發布_仍維持草稿()
    {
        var schedule = new Schedule(2026, 11);
        var violation = new Violation(RuleCode.RestPeriod, 1, D("2026-11-11"), ShiftType.Early, "休息不足");

        var ex = Assert.Throws<SchedulePublishException>(() => schedule.Publish(new ValidationResult([violation], []), Now));

        Assert.Single(ex.Validation.Violations);
        Assert.Equal(ScheduleStatus.Draft, schedule.Status);
    }

    [Fact]
    public void 只有人力缺口時也不可發布()
    {
        var schedule = new Schedule(2026, 11);
        var gap = new CoverageGap(D("2026-11-11"), ShiftType.Early, null, 4, 3, "人數不足");

        Assert.Throws<SchedulePublishException>(() => schedule.Publish(new ValidationResult([], [gap]), Now));
    }

    [Fact]
    public void 驗證通過即可發布_記錄發布時間與版本1()
    {
        var schedule = new Schedule(2026, 11);

        schedule.Publish(Clean, Now);

        Assert.Equal((ScheduleStatus.Published, 1, Now), (schedule.Status, schedule.Revision, schedule.PublishedAt));
    }

    [Fact]
    public void 發布後新增刪除或整批取代都被拒絕()
    {
        var existing = A(1, "2026-11-10");
        var schedule = new Schedule(2026, 11);
        schedule.Add(existing);
        schedule.Publish(Clean, Now);

        Assert.Throws<ScheduleLockedException>(() => schedule.Add(A(2, "2026-11-10")));
        Assert.Throws<ScheduleLockedException>(() => schedule.Remove(existing));
        Assert.Throws<ScheduleLockedException>(() => schedule.ReplaceAll([]));
        Assert.Throws<ScheduleLockedException>(() => schedule.Publish(Clean, Now));
        Assert.Equal([existing], schedule.Assignments);
    }

    [Fact]
    public void 已發布班表套用臨時重排_版本加1()
    {
        var removed = A(1, "2026-11-16");
        var added = A(2, "2026-11-16");
        var schedule = new Schedule(2026, 11);
        schedule.Add(removed);
        schedule.Publish(Clean, Now);

        schedule.ApplyRevision([removed], [added], Clean);

        Assert.Equal(2, schedule.Revision);
        Assert.Equal([added], schedule.Assignments);
    }

    [Fact]
    public void 草稿不能走臨時重排()
    {
        var schedule = new Schedule(2026, 11);

        Assert.Throws<InvalidOperationException>(() => schedule.ApplyRevision([], [], Clean));
    }

    [Fact]
    public void 不屬於該月的班次不可加入()
    {
        var schedule = new Schedule(2026, 11);

        Assert.Throws<ArgumentException>(() => schedule.Add(A(1, "2026-12-01")));
    }
}
