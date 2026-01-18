using System.Text.Json;
using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Repositories;

/// <summary>
/// Settings repository implementation
/// Single Responsibility: Manages SchedulerSettings
/// </summary>
public class SettingsRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public SettingsRepository()
    {
        _filePath = Path.Combine(AppContext.BaseDirectory, "Data", "settings.json");
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        EnsureDirectoryExists();
    }

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<SchedulerSettings> LoadSettingsAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new SchedulerSettings { ShiftHours = 8 };
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<SchedulerSettings>(json, _jsonOptions) 
                ?? new SchedulerSettings { ShiftHours = 8 };
        }
        catch
        {
            return new SchedulerSettings { ShiftHours = 8 };
        }
    }

    public async Task SaveSettingsAsync(SchedulerSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
