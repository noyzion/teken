namespace ShiftScheduler.API.Models;

/// <summary>
/// Represents a schedule configuration
/// </summary>
public class ScheduleConfig
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? StartHour { get; set; } // שעת התחלה (0-23), null = 00:00 - מגביל טווח בלבד
    public int? EndHour { get; set; } // שעת סיום (0-23), null = 23:59 - מגביל טווח בלבד
    public int? FirstShiftStartHour { get; set; } // שעת התחלה של משמרת ראשונה (0-23), null = 00:00. למשל 6 = משמרות 6-9, 9-12, 12-15...
    /// <summary>בקשות לשיבוץ ספציפי: לשים חייל במשמרת בתאריך/שעה מסוימת.</summary>
    public List<ShiftRequest>? ShiftRequests { get; set; }
}

/// <summary>
/// בקשת שיבוץ: חייל X במשמרת בתאריך Y (אופציונלית: שעת התחלה של המשמרת).
/// </summary>
public class ShiftRequest
{
    public string SoldierId { get; set; } = string.Empty;
    /// <summary>תאריך format yyyy-MM-dd</summary>
    public string Date { get; set; } = string.Empty;
    /// <summary>שעת התחלה של המשמרת (0-23). null = כל משמרת באותו תאריך.</summary>
    public int? StartHour { get; set; }
}

/// <summary>
/// Represents a shift assignment
/// </summary>
public class ShiftAssignment
{
    public string PositionId { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public string SoldierId { get; set; } = string.Empty;
    public string SoldierName { get; set; } = string.Empty;
    /// <summary>שיבוץ בכפייה (fallback relaxGap) – לצורך שקיפות בדוח.</summary>
    public bool IsForced { get; set; }
}

/// <summary>
/// Represents a day schedule with shifts
/// </summary>
public class DaySchedule
{
    public string Date { get; set; } = string.Empty;
    public int ShiftNumber { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public List<ShiftAssignment> Assignments { get; set; } = new();
}

/// <summary>
/// Global settings for the scheduler
/// </summary>
public class SchedulerSettings
{
    public double ShiftHours { get; set; } = 8;
}

/// <summary>
/// דוח בדיקות סופיות לפני סגירה (סעיף 7 במפרט).
/// </summary>
public class ScheduleValidationReport
{
    public List<SoldierScheduleStats> PerSoldierStats { get; set; } = new();
    public List<string> SoldiersWithFewestGuards { get; set; } = new();
    public List<string> SoldiersWithMostGuards { get; set; } = new();
    public List<string> SoldiersWithFewestNights { get; set; } = new();
    public List<string> SoldiersWithMostNights { get; set; } = new();
    public List<string> SoldiersWithShortestGap { get; set; } = new();
    public double? ShortestGapHours { get; set; }
    public List<string> SoldiersWithLongestGap { get; set; } = new();
    public double? LongestGapHours { get; set; }
    /// <summary>שיבוצים בכפייה (fallback relaxGap) – לשקיפות.</summary>
    public List<ForcedAssignmentInfo> ForcedAssignments { get; set; } = new();
    /// <summary>האם כל האילוצים הקשיחים מתקיימים (לאחר בדיקה).</summary>
    public bool? HardConstraintsValid { get; set; }
    /// <summary>רשימת הפרות אילוצים (אם נבדק).</summary>
    public List<string> HardConstraintViolations { get; set; } = new();
}

public class ForcedAssignmentInfo
{
    public string Date { get; set; } = string.Empty;
    public int ShiftNumber { get; set; }
    public string PositionName { get; set; } = string.Empty;
    public string SoldierName { get; set; } = string.Empty;
}

public class SoldierScheduleStats
{
    public string SoldierId { get; set; } = string.Empty;
    public string SoldierName { get; set; } = string.Empty;
    /// <summary>סה"כ משמרות (שמירות + כוננויות).</summary>
    public int TotalShifts { get; set; }
    /// <summary>שמירות (לא כוננות).</summary>
    public int GuardCount { get; set; }
    /// <summary>שמירות לילה (00–3, 3–6, 6–9).</summary>
    public int NightGuardCount { get; set; }
    /// <summary>רווח ממוצע בין שמירות (שעות).</summary>
    public double? AverageGapHours { get; set; }
    /// <summary>רווח מינימלי בין שמירות (שעות).</summary>
    public double? MinGapHours { get; set; }
    /// <summary>רווח מקסימלי בין שמירות (שעות).</summary>
    public double? MaxGapHours { get; set; }
}

/// <summary>
/// תוצאת בדיקת אילוצים קשיחים (סעיף 1) – אם יש הפרות, הרשימה לא תקינה.
/// </summary>
public class ScheduleHardConstraintsValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Violations { get; set; } = new();
}
