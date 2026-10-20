namespace ShiftPlanner.Core.Domain;

/// <summary>班別。數值會存進資料庫，不可任意更動順序。</summary>
public enum ShiftType
{
    Early = 0,
    Late = 1,
    Night = 2,
}

/// <summary>員工可上班別的組合（可複選）。</summary>
[Flags]
public enum ShiftTypes
{
    None = 0,
    Early = 1,
    Late = 2,
    Night = 4,
    All = Early | Late | Night,
}

[Flags]
public enum Skills
{
    None = 0,
    Chinese = 1,
    English = 2,
    Japanese = 4,
}

public enum ContractType
{
    FullTime = 0,
    PartTime = 1,
}

/// <summary>決定套用哪一組人力需求。週末、國定假日、補假為 Holiday；補班日為 Weekday。</summary>
public enum DayType
{
    Weekday = 0,
    Holiday = 1,
}

public enum PreferencePriority
{
    Normal = 0,
    Strong = 1,
}
