using Google.OrTools.Sat;

using ShiftPlanner.Core.Domain;
using ShiftPlanner.Core.Validation;

namespace ShiftPlanner.Scheduling;

/// <summary>
/// 把排班問題轉成 CP-SAT 模型。
/// 變數：x[e,d,s] ∈ {0,1}，只為「可上該班且未請假」的組合建立。
/// 硬限制與 validator 的規則一一對應，但為獨立實作；產生後仍會再跑一次 validator。
/// </summary>
internal sealed class ScheduleModel
{
    public const long StrongPreferenceWeight = 1000;
    public const long NormalPreferenceWeight = 100;
    public const long BaselineChangeWeight = 500;
    public const long WeekendSpreadWeight = 50;
    public const long NightSpreadWeight = 20;
    public const long DeferredRestWeight = 30;
    public const long ShiftCost = 1;

    private readonly GenerationRequest _request;
    private readonly Dictionary<(int Employee, DateOnly Date, ShiftType Shift), BoolVar> _x = [];
    private readonly HashSet<(int Employee, DateOnly Date, ShiftType Shift)> _context;
    private readonly HashSet<(int Employee, DateOnly Date, ShiftType Shift)> _following;
    private readonly LinearExprBuilder _objective = LinearExpr.NewBuilder();
    private readonly bool _elastic;
    private readonly List<DemandSlack> _slacks = [];

    /// <param name="elastic">
    /// 診斷模式：人數與技能需求加上 slack，夜班差距改為不限制，目標只剩「最小化補不滿的人數」，
    /// 用來找出無解時是哪些格子排不滿。
    /// </param>
    public ScheduleModel(GenerationRequest request, bool elastic = false)
    {
        _request = request;
        _elastic = elastic;
        _context = request.Context.Select(a => (a.EmployeeId, a.Date, a.Shift)).ToHashSet();
        _following = (request.Following?.Assignments ?? []).Select(a => (a.EmployeeId, a.Date, a.Shift)).ToHashSet();
        Build();
    }

    public CpModel Model { get; } = new();

    public IReadOnlyList<DemandSlack> Slacks => _slacks;

    private DateRange Horizon => _request.Horizon;

    /// <summary>班次確定到哪一天；之後的日子是未知的，可以用來補足休息日。</summary>
    private DateOnly KnownUntil => _request.Following?.KnownUntil ?? Horizon.End;

    public IReadOnlyList<Assignment> ExtractAssignments(CpSolver solver) =>
        _x.Where(kv => solver.BooleanValue(kv.Value))
          .Select(kv => new Assignment(kv.Key.Employee, kv.Key.Date, kv.Key.Shift))
          .OrderBy(a => a.Date).ThenBy(a => a.Shift).ThenBy(a => a.EmployeeId)
          .ToList();

    private void Build()
    {
        CreateVariables();
        AddOneShiftPerDay();
        AddRestPeriods();
        AddWeeklyLimits();
        AddConsecutiveDays();
        AddDemand();
        if (!_elastic)
        {
            AddFairness();
            AddPreferences();
            AddBaselineDeviation();
        }

        Model.Minimize(_objective);
    }

    private void CreateVariables()
    {
        var leaves = _request.Leaves.ToLookup(l => l.EmployeeId);
        foreach (var employee in _request.Employees)
        {
            foreach (var date in Horizon.Days())
            {
                if (leaves[employee.Id].Any(l => l.Contains(date)))
                {
                    continue;
                }

                foreach (var shift in DisplayNames.AllShifts.Where(employee.CanWork))
                {
                    var x = Model.NewBoolVar($"x_{employee.Id}_{date:MMdd}_{shift}");
                    _x[(employee.Id, date, shift)] = x;
                    if (!_elastic)
                    {
                        _objective.AddTerm(x, ShiftCost); // 需求是「最低」人數，避免無謂地多排
                    }
                }
            }
        }
    }

    private void AddOneShiftPerDay()
    {
        foreach (var group in _x.GroupBy(kv => (kv.Key.Employee, kv.Key.Date)))
        {
            Model.AddAtMostOne(group.Select(kv => (ILiteral)kv.Value));
        }
    }

    /// <summary>找出休息不足的「前一班 → 後一班」組合，禁止同時出現；前後月份的班次以常數代入。</summary>
    private void AddRestPeriods()
    {
        var conflicts = RestConflicts.For(_request.Catalog);
        foreach (var employee in _request.Employees)
        {
            foreach (var date in Horizon.Days())
            {
                foreach (var (offset, prevShift, nextShift) in conflicts)
                {
                    var followingDate = date.AddDays(offset);
                    if (followingDate > Horizon.End
                        && _following.Contains((employee.Id, followingDate, nextShift))
                        && _x.TryGetValue((employee.Id, date, prevShift), out var beforeFollowing))
                    {
                        Model.AddLinearConstraint(beforeFollowing, 0, 0);
                    }

                    if (!_x.TryGetValue((employee.Id, date, nextShift), out var next))
                    {
                        continue;
                    }

                    var prevDate = date.AddDays(-offset);
                    if (prevDate < Horizon.Start)
                    {
                        if (_context.Contains((employee.Id, prevDate, prevShift)))
                        {
                            Model.AddLinearConstraint(next, 0, 0);
                        }
                    }
                    else if (_x.TryGetValue((employee.Id, prevDate, prevShift), out var prev))
                    {
                        Model.AddBoolOr([prev.Not(), next.Not()]);
                    }
                }
            }
        }
    }

    private void AddWeeklyLimits()
    {
        foreach (var monday in Horizon.WeekStarts())
        {
            var unknownDays = Math.Max(0, monday.AddDays(6).DayNumber - KnownUntil.DayNumber);
            var knownDays = Enumerable.Range(0, 7 - unknownDays).Select(monday.AddDays).ToList();
            var requiredOff = Math.Max(0, WeeklyDaysOffRule.RequiredDaysOff - unknownDays);

            foreach (var employee in _request.Employees)
            {
                var worked = LinearExpr.NewBuilder();
                var minutes = LinearExpr.NewBuilder();
                foreach (var date in knownDays)
                {
                    worked.Add(WorkOn(employee.Id, date));
                    foreach (var shift in DisplayNames.AllShifts)
                    {
                        minutes.Add(ShiftOn(employee.Id, date, shift) * MinutesOf(shift));
                    }
                }

                Model.AddLinearConstraint(worked, 0, knownDays.Count - requiredOff);
                Model.AddLinearConstraint(minutes, 0, employee.WeeklyHourLimit * 60L);

                if (requiredOff < WeeklyDaysOffRule.RequiredDaysOff && !_elastic)
                {
                    DiscourageDeferredRest(worked, knownDays.Count, $"{employee.Id}_{monday:MMdd}");
                }
            }
        }
    }

    /// <summary>
    /// 月底不完整的週只要求休 max(0, 2 − 未知天數) 天，剩下的休假會被推給下個月；
    /// 太多人同時這樣做，下個月第一天就可能排不出人。每推一天給下個月就加一點成本。
    /// </summary>
    private void DiscourageDeferredRest(LinearExpr worked, int knownDays, string name)
    {
        var deferred = Model.NewIntVar(0, WeeklyDaysOffRule.RequiredDaysOff, $"deferred_rest_{name}");
        // deferred ≥ 2 − (已知天數 − 上班天數)
        Model.AddLinearConstraint(
            LinearExpr.NewBuilder().Add(worked).AddTerm(deferred, -1),
            long.MinValue,
            knownDays - WeeklyDaysOffRule.RequiredDaysOff);
        _objective.AddTerm(deferred, DeferredRestWeight);
    }

    private void AddConsecutiveDays()
    {
        const int window = ConsecutiveDaysRule.MaxConsecutiveDays + 1;
        for (var start = Horizon.Start.AddDays(1 - window); start <= Horizon.End && start.AddDays(window - 1) <= KnownUntil; start = start.AddDays(1))
        {
            foreach (var employee in _request.Employees)
            {
                var worked = LinearExpr.NewBuilder();
                for (var i = 0; i < window; i++)
                {
                    worked.Add(WorkOn(employee.Id, start.AddDays(i)));
                }

                Model.AddLinearConstraint(worked, 0, ConsecutiveDaysRule.MaxConsecutiveDays);
            }
        }
    }

    private void AddDemand()
    {
        var employees = _request.Employees.ToDictionary(e => e.Id);
        foreach (var date in Horizon.Days())
        {
            foreach (var shift in DisplayNames.AllShifts)
            {
                var slot = _x.Where(kv => kv.Key.Date == date && kv.Key.Shift == shift).ToList();
                AddAtLeast(SumOf(slot.Select(kv => kv.Value)), _request.Calendar.GetMinStaff(date, shift), date, shift, null);

                foreach (var requirement in _request.Calendar.GetSkillRequirements(date, shift))
                {
                    var skilled = slot.Where(kv => employees[kv.Key.Employee].HasSkill(requirement.Skill)).Select(kv => kv.Value);
                    AddAtLeast(SumOf(skilled), requirement.MinCount, date, shift, requirement.Skill);
                }
            }
        }
    }

    private void AddAtLeast(LinearExpr staffed, int required, DateOnly date, ShiftType shift, Skills? skill)
    {
        if (!_elastic || required == 0)
        {
            Model.AddLinearConstraint(staffed, required, long.MaxValue);
            return;
        }

        var slack = Model.NewIntVar(0, required, $"slack_{date:MMdd}_{shift}_{skill}");
        Model.AddLinearConstraint(LinearExpr.NewBuilder().Add(staffed).Add(slack), required, long.MaxValue);
        _objective.Add(slack);
        _slacks.Add(new DemandSlack(date, shift, skill, required, slack));
    }

    /// <summary>全職員工之間的夜班數與週末班數盡量平均；夜班差距可設為硬限制。</summary>
    private void AddFairness()
    {
        var period = _request.FairnessPeriod ?? Horizon;
        var fullTime = _request.Employees.Where(e => e.IsFullTime).ToList();

        var nights = fullTime.Where(e => e.CanWork(ShiftType.Night))
            .Select(e => CountShifts(e.Id, period, (d, s) => s == ShiftType.Night))
            .ToList();
        var nightSpread = Spread(nights, "night");
        if (nightSpread is not null)
        {
            _objective.AddTerm(nightSpread, NightSpreadWeight);
            if (_request.MaxNightSpread is int max)
            {
                Model.AddLinearConstraint(nightSpread, 0, max);
            }
        }

        var weekends = fullTime.Select(e => CountShifts(e.Id, period, (d, _) => d.IsWeekend())).ToList();
        var weekendSpread = Spread(weekends, "weekend");
        if (weekendSpread is not null)
        {
            _objective.AddTerm(weekendSpread, WeekendSpreadWeight);
        }
    }

    private void AddPreferences()
    {
        foreach (var preference in _request.Preferences.Where(p => Horizon.Contains(p.Date)))
        {
            var weight = preference.Priority == PreferencePriority.Strong ? StrongPreferenceWeight : NormalPreferenceWeight;
            _objective.AddTerm(WorkOn(preference.EmployeeId, preference.Date), weight);
        }
    }

    /// <summary>重排時，每個與原班表不同的格子都計入成本，並以原班表當作求解起點。</summary>
    private void AddBaselineDeviation()
    {
        if (_request.Baseline.Count == 0)
        {
            return;
        }

        var baseline = _request.Baseline.Select(a => (a.EmployeeId, a.Date, a.Shift)).ToHashSet();
        foreach (var (key, x) in _x)
        {
            var wasAssigned = baseline.Contains(key);
            Model.AddHint(x, wasAssigned);
            if (wasAssigned)
            {
                _objective.Add(BaselineChangeWeight);
                _objective.AddTerm(x, -BaselineChangeWeight);
            }
            else
            {
                _objective.AddTerm(x, BaselineChangeWeight);
            }
        }
    }

    private IntVar? Spread(IReadOnlyList<LinearExpr> counts, string name)
    {
        if (counts.Count < 2)
        {
            return null;
        }

        var upper = Horizon.DayCount + 31;
        var max = Model.NewIntVar(0, upper, $"{name}_max");
        var min = Model.NewIntVar(0, upper, $"{name}_min");
        var spread = Model.NewIntVar(0, upper, $"{name}_spread");
        Model.AddMaxEquality(max, counts);
        Model.AddMinEquality(min, counts);
        Model.AddLinearConstraint(LinearExpr.NewBuilder().Add(spread).Add(min).AddTerm(max, -1), 0, 0);
        return spread;
    }

    private LinearExpr CountShifts(int employeeId, DateRange period, Func<DateOnly, ShiftType, bool> predicate)
    {
        var count = LinearExpr.NewBuilder();
        foreach (var date in period.Days())
        {
            foreach (var shift in DisplayNames.AllShifts.Where(s => predicate(date, s)))
            {
                count.Add(ShiftOn(employeeId, date, shift));
            }
        }

        return count;
    }

    /// <summary>某人某天是否上班（0/1）。Horizon 之前、之後用前後上下文的常數。</summary>
    private LinearExpr WorkOn(int employeeId, DateOnly date)
    {
        var expr = LinearExpr.NewBuilder();
        foreach (var shift in DisplayNames.AllShifts)
        {
            expr.Add(ShiftOn(employeeId, date, shift));
        }

        return expr;
    }

    private LinearExpr ShiftOn(int employeeId, DateOnly date, ShiftType shift)
    {
        if (date < Horizon.Start)
        {
            return LinearExpr.Constant(_context.Contains((employeeId, date, shift)) ? 1 : 0);
        }

        if (date > Horizon.End)
        {
            return LinearExpr.Constant(_following.Contains((employeeId, date, shift)) ? 1 : 0);
        }

        return _x.TryGetValue((employeeId, date, shift), out var x) ? x : LinearExpr.Constant(0);
    }

    private long MinutesOf(ShiftType shift) => (long)_request.Catalog[shift].Duration.TotalMinutes;

    private static LinearExpr SumOf(IEnumerable<BoolVar> vars)
    {
        var sum = LinearExpr.NewBuilder();
        sum.AddSum(vars);
        return sum;
    }
}

/// <summary>診斷模式下，某格需求補不滿的人數。</summary>
internal sealed record DemandSlack(DateOnly Date, ShiftType Shift, Skills? Skill, int Required, IntVar Shortfall);
