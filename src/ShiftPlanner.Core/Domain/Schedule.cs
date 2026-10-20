using ShiftPlanner.Core.Validation;

namespace ShiftPlanner.Core.Domain;

public enum ScheduleStatus
{
    Draft = 0,
    Published = 1,
}

/// <summary>發布後仍嘗試直接修改班表時拋出。</summary>
public sealed class ScheduleLockedException(string message) : InvalidOperationException(message);

/// <summary>驗證未通過卻嘗試發布時拋出。</summary>
public sealed class SchedulePublishException(string message, ValidationResult validation) : InvalidOperationException(message)
{
    public ValidationResult Validation { get; } = validation;
}

/// <summary>
/// 某月班表。草稿可自由修改；發布後只能透過臨時重排（ApplyRevision）產生新版本。
/// </summary>
public sealed class Schedule
{
    private readonly List<Assignment> _assignments = [];

    public Schedule(int year, int month)
    {
        _ = DateRange.ForMonth(year, month); // 年月不合法時立即拋出
        Year = year;
        Month = month;
        Status = ScheduleStatus.Draft;
    }

    public int Id { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public ScheduleStatus Status { get; private set; }

    /// <summary>草稿為 0；發布為 1；每次套用臨時重排 +1。</summary>
    public int Revision { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public IReadOnlyList<Assignment> Assignments => _assignments;

    public DateRange Period => DateRange.ForMonth(Year, Month);

    public bool IsPublished => Status == ScheduleStatus.Published;

    public void Add(Assignment assignment)
    {
        EnsureDraft();
        EnsureInPeriod(assignment);
        if (_assignments.Contains(assignment))
        {
            throw new InvalidOperationException($"班次已存在：{assignment}");
        }

        _assignments.Add(assignment);
    }

    public void Remove(Assignment assignment)
    {
        EnsureDraft();
        if (!_assignments.Remove(assignment))
        {
            throw new InvalidOperationException($"找不到要刪除的班次：{assignment}");
        }
    }

    /// <summary>以產生器的結果整批取代草稿內容。</summary>
    public void ReplaceAll(IEnumerable<Assignment> assignments)
    {
        EnsureDraft();
        var list = assignments.ToList();
        list.ForEach(EnsureInPeriod);
        _assignments.Clear();
        _assignments.AddRange(list.Distinct());
    }

    public void Publish(ValidationResult validation, DateTimeOffset now)
    {
        EnsureDraft();
        if (!validation.CanPublish)
        {
            throw new SchedulePublishException(
                $"班表仍有 {validation.Violations.Count} 個硬性違規、{validation.Gaps.Count} 個人力缺口，無法發布",
                validation);
        }

        Status = ScheduleStatus.Published;
        PublishedAt = now;
        Revision = 1;
    }

    /// <summary>已發布班表套用經主管確認的臨時重排結果，版本 +1。</summary>
    public void ApplyRevision(IEnumerable<Assignment> removed, IEnumerable<Assignment> added, ValidationResult validationAfter)
    {
        if (!IsPublished)
        {
            throw new InvalidOperationException("只有已發布的班表需要臨時重排，草稿請直接修改");
        }

        if (!validationAfter.CanPublish)
        {
            throw new SchedulePublishException("重排後的班表仍有違規或缺口，無法套用", validationAfter);
        }

        foreach (var assignment in removed)
        {
            if (!_assignments.Remove(assignment))
            {
                throw new InvalidOperationException($"重排要移除的班次不存在：{assignment}");
            }
        }

        foreach (var assignment in added)
        {
            EnsureInPeriod(assignment);
            _assignments.Add(assignment);
        }

        Revision++;
    }

    private void EnsureDraft()
    {
        if (IsPublished)
        {
            throw new ScheduleLockedException($"{Year}/{Month} 班表已發布，不可直接修改；請使用臨時重排");
        }
    }

    private void EnsureInPeriod(Assignment assignment)
    {
        if (!Period.Contains(assignment.Date))
        {
            throw new ArgumentException($"班次 {assignment} 不在 {Year}/{Month} 範圍內");
        }
    }
}
