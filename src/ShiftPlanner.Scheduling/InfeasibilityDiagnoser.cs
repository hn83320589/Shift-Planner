using Google.OrTools.Sat;

using ShiftPlanner.Core.Domain;

namespace ShiftPlanner.Scheduling;

/// <summary>
/// 靜態預檢沒發現問題、但 CP-SAT 判定無解時使用：把需求放寬成 slack，
/// 求「最少要補不滿幾人」，列出補不滿的格子。
/// </summary>
internal static class InfeasibilityDiagnoser
{
    public static IReadOnlyList<InfeasibilityReason> Diagnose(GenerationRequest request, Func<CpSolver> createSolver)
    {
        var model = new ScheduleModel(request, elastic: true);
        var solver = createSolver();
        var status = solver.Solve(model.Model);

        if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible))
        {
            return
            [
                new InfeasibilityReason(null, null, 0, 0,
                    "即使不考慮人力需求，仍無法同時滿足休息、連續上班與工時限制；請檢查前月班表與請假設定"),
            ];
        }

        var reasons = model.Slacks
            .Select(s => (Slack: s, Shortfall: (int)solver.Value(s.Shortfall)))
            .Where(x => x.Shortfall > 0)
            .Select(x => ToReason(x.Slack, x.Slack.Required - x.Shortfall))
            .ToList();

        if (reasons.Count == 0)
        {
            reasons.Add(new InfeasibilityReason(null, null, request.MaxNightSpread ?? 0, 0,
                $"人力需求都排得滿，但無法同時讓全職員工的夜班數差距 ≤ {request.MaxNightSpread}"));
        }

        return reasons;
    }

    private static InfeasibilityReason ToReason(DemandSlack slack, int achievable)
    {
        var slot = $"{slack.Date.ToLabel()} {slack.Shift.ToDisplayName()}";
        var message = slack.Skill is { } skill
            ? $"{slot}需要 {slack.Required} 位會{skill.ToDisplayName()}的人員，在休息、連續上班與工時限制下最多只能排 {achievable} 位"
            : $"{slot}需要 {slack.Required} 人，在休息、連續上班與工時限制下最多只能排 {achievable} 人";
        return new InfeasibilityReason(slack.Date, slack.Shift, slack.Required, achievable, message);
    }
}
