namespace ShiftScheduler.API.Models;

/// <summary>
/// Represents a guard position
/// </summary>
public class Position
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool RequiresCommander { get; set; } = false;
    public bool IsStandby { get; set; } = false; // true = כוננות (יכול להיות רצוף), false = שמירה (צריך רווח)
}
