using System.Globalization;

using Google.OrTools.Sat;

using ShiftPlanner.Core.Validation;

namespace ShiftPlanner.Scheduling;

/// <summary>
/// 以 OR-Tools CP-SAT 產生班表。產生後一定以獨立的 validator 再檢查一次。
/// 回報的耗時經由注入的 TimeProvider 計算；CP-SAT 內部的時間上限則由原生程式庫自行計時。
/// </summary>
public sealed class ShiftScheduleGenerator(TimeProvider time)
{
    private readonly ScheduleValidator _validator = new();

    public GenerationResult Generate(GenerationRequest request)
    {
        var started = time.GetTimestamp();
        var precheck = FeasibilityPrecheck.Check(request);
        if (precheck.Count > 0)
        {
            return new GenerationResult(GenerationStatus.Infeasible, [], precheck, time.GetElapsedTime(started));
        }

        var model = new ScheduleModel(request);
        var solver = CreateSolver(request);
        var status = solver.Solve(model.Model);
        switch (status)
        {
            case CpSolverStatus.Optimal:
            case CpSolverStatus.Feasible:
                var assignments = model.ExtractAssignments(solver);
                EnsureValid(request, assignments);
                return new GenerationResult(
                    status == CpSolverStatus.Optimal ? GenerationStatus.Optimal : GenerationStatus.Feasible,
                    assignments, [], time.GetElapsedTime(started));

            case CpSolverStatus.Infeasible:
                var reasons = InfeasibilityDiagnoser.Diagnose(request, () => CreateSolver(request));
                return new GenerationResult(GenerationStatus.Infeasible, [], reasons, time.GetElapsedTime(started));

            case CpSolverStatus.Unknown:
                return new GenerationResult(GenerationStatus.TimedOut, [],
                    [new InfeasibilityReason(null, null, 0, 0, $"{request.TimeLimit.TotalSeconds:0} 秒內找不到可行解，也無法證明無解")],
                    time.GetElapsedTime(started));

            default:
                throw new InvalidOperationException($"CP-SAT 回傳非預期狀態 {status}：{solver.SolutionInfo()}");
        }
    }

    private static CpSolver CreateSolver(GenerationRequest request) => new()
    {
        StringParameters = string.Create(CultureInfo.InvariantCulture,
            $"max_time_in_seconds:{request.TimeLimit.TotalSeconds} num_workers:8 random_seed:42"),
    };

    /// <summary>產生器與 validator 是兩套獨立實作；結果不一致代表程式有錯，直接拋出。</summary>
    private void EnsureValid(GenerationRequest request, IReadOnlyList<Core.Domain.Assignment> assignments)
    {
        var snapshot = new ScheduleSnapshot(
            request.Horizon, request.Employees, assignments, request.Calendar,
            request.Context, request.Leaves, request.Catalog);
        var result = _validator.Validate(snapshot);
        if (!result.CanPublish)
        {
            var details = result.Violations.Select(v => v.Message).Concat(result.Gaps.Select(g => g.Message)).Take(5);
            throw new InvalidOperationException("產生器的結果未通過 validator 檢查（程式錯誤）：" + string.Join("；", details));
        }
    }
}
