namespace ShiftScheduler.API.Models;

/// <summary>
/// Represents a soldier
/// </summary>
public class Soldier
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsCommander { get; set; } = false;
    public SoldierConstraints Constraints { get; set; } = new();
}

/// <summary>
/// Constraints for a soldier
/// </summary>
public class SoldierConstraints
{
    public List<string>? ForbiddenPositions { get; set; }
    public List<int>? ForbiddenDaysOfWeek { get; set; } // 0=Sunday, 1=Monday, ..., 6=Saturday - ימים שלמים שהחייל לא נמצא
    public Dictionary<string, List<int>>? ForbiddenHoursByDay { get; set; } // Key: day of week (0-6), Value: list of forbidden hours - שעות אסורות לפי יום
    public Dictionary<string, List<HourRange>>? ForbiddenHourRangesByDay { get; set; } // Key: day of week (0-6), Value: list of forbidden hour ranges - טווחי שעות אסורות לפי יום
}

/// <summary>
/// Represents a range of forbidden hours
/// </summary>
public class HourRange
{
    public int StartHour { get; set; } // 0-23
    public int EndHour { get; set; } // 0-23, can be less than StartHour if crossing midnight
    public int? EndDay { get; set; } // Optional: day of week for end hour if range crosses days (0-6)
}
