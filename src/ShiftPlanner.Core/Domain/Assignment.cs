namespace ShiftPlanner.Core.Domain;

/// <summary>某人在某日上某班。Date 為班次開始日（夜班歸屬開始那天）。</summary>
public sealed record Assignment(int EmployeeId, DateOnly Date, ShiftType Shift);
