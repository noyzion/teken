using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;
using ShiftScheduler.API.Repositories;

namespace ShiftScheduler.API.Services;

/// <summary>
/// Settings service implementation
/// Single Responsibility: Manages scheduler settings
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ISettingsRepository _repository;

    public SettingsService(ISettingsRepository repository)
    {
        _repository = repository;
    }

    public async Task<SchedulerSettings> GetSettingsAsync()
    {
        return await _repository.LoadSettingsAsync();
    }

    public async Task<SchedulerSettings> UpdateSettingsAsync(SchedulerSettings settings)
    {
        if (settings.ShiftHours < 0.5)
            throw new ArgumentException("מספר השעות חייב להיות גדול מ-0", nameof(settings));

        await _repository.SaveSettingsAsync(settings);
        return settings;
    }
}
