using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Interfaces;

public interface ISettingsRepository
{
    Task<SchedulerSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(SchedulerSettings settings);
}
