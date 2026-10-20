namespace ShiftPlanner.Core.Validation;

public interface IHardRule
{
    IEnumerable<Violation> Check(ScheduleSnapshot snapshot);
}
