using System.Text.Json;
using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Repositories;

public class CurrentScheduleRepository : ICurrentScheduleRepository
{
    private readonly IUserContextService _userContext;
    private readonly JsonSerializerOptions _jsonOptions;

    public CurrentScheduleRepository(IUserContextService userContext)
    {
        _userContext = userContext;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    private string GetFilePath()
    {
        var userId = _userContext.GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return string.Empty;
        return Path.Combine(AppContext.BaseDirectory, "Data", userId, "currentSchedule.json");
    }

    private void EnsureDirectoryExists(string? path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    public async Task<List<DaySchedule>> GetAsync()
    {
        var path = GetFilePath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return new List<DaySchedule>();

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var list = JsonSerializer.Deserialize<List<DaySchedule>>(json, _jsonOptions);
            return list ?? new List<DaySchedule>();
        }
        catch
        {
            return new List<DaySchedule>();
        }
    }

    public async Task SaveAsync(List<DaySchedule> schedule)
    {
        var path = GetFilePath();
        if (string.IsNullOrEmpty(path)) return;
        EnsureDirectoryExists(path);
        var json = JsonSerializer.Serialize(schedule, _jsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

}
