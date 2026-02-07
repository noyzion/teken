namespace ShiftScheduler.API.Models;

/// <summary>
/// User for authentication - stored in Data/users.json (global, not per-user).
/// </summary>
public class User
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
