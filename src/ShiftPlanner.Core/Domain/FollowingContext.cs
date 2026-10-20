namespace ShiftPlanner.Core.Domain;

/// <summary>
/// 範圍之後已確定的班次（通常是已發布的下個月），唯讀。
/// KnownUntil 之前沒有班次的日子視為休假；之後則視為未知，可以用來補足休息日。
/// </summary>
public sealed record FollowingContext(DateOnly KnownUntil, IReadOnlyList<Assignment> Assignments);
