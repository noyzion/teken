namespace ShiftScheduler.API.Interfaces;

/// <summary>
/// Provides current user id from JWT (HttpContext).
/// </summary>
public interface IUserContextService
{
    string? GetCurrentUserId();
    void EnsureUserDataDirectory();
}
