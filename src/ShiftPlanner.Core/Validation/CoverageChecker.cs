using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Core.Validation;

/// <summary>檢查每天每班的人數與技能是否達到需求。</summary>
public sealed class CoverageChecker
{
    public IEnumerable<CoverageGap> Check(ScheduleSnapshot snapshot)
    {
        var staffed = snapshot.Assignments
            .ToLookup(a => (a.Date, a.Shift), a => a.EmployeeId);

        foreach (var date in snapshot.Horizon.Days())
        {
            foreach (var shift in DisplayNames.AllShifts)
            {
                var people = staffed[(date, shift)].Distinct().Select(snapshot.GetEmployee).ToList();
                var slot = $"{date.ToLabel()} {shift.ToDisplayName()}";

                var required = snapshot.Calendar.GetMinStaff(date, shift);
                if (people.Count < required)
                {
                    yield return new CoverageGap(date, shift, null, required, people.Count,
                        $"{slot} 需要 {required} 人，目前只有 {people.Count} 人");
                }

                foreach (var requirement in snapshot.Calendar.GetSkillRequirements(date, shift))
                {
                    var count = people.Count(e => e.HasSkill(requirement.Skill));
                    if (count < requirement.MinCount)
                    {
                        yield return new CoverageGap(date, shift, requirement.Skill, requirement.MinCount, count,
                            $"{slot} 需要 {requirement.MinCount} 位會{requirement.Skill.ToDisplayName()}的人員，目前只有 {count} 位");
                    }
                }
            }
        }
    }
}
