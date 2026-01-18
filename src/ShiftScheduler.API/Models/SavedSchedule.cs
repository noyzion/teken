namespace ShiftScheduler.API.Models;

/// <summary>
/// Represents a saved schedule
/// </summary>
public class SavedSchedule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastModified { get; set; }
    public string CreatedBy { get; set; } = string.Empty; // שם המשתמש שיצר
    public List<DaySchedule> Schedule { get; set; } = new();
    public bool IsShared { get; set; } = false; // האם הלוח משותף
    public string ShareCode { get; set; } = string.Empty; // קוד שיתוף ייחודי
}
