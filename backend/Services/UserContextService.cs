using System.IO;
using System.Security.Claims;
using ShiftScheduler.API.Interfaces;

namespace ShiftScheduler.API.Services;

public class UserContextService : IUserContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetCurrentUserId()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("sub");
        return userId;
    }

    public void EnsureUserDataDirectory()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return;
        var path = Path.Combine(AppContext.BaseDirectory, "Data", userId);
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
