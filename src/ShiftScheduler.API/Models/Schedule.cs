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
