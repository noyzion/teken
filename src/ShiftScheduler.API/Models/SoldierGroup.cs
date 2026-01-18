namespace ShiftScheduler.API.Models;

/// <summary>
/// Represents a group of soldiers that cannot guard simultaneously
/// </summary>
public class SoldierGroup
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> SoldierIds { get; set; } = new();
}
