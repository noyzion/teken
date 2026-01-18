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
}
