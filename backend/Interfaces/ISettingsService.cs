using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Interfaces;

/// <summary>
/// Settings service interface - Single Responsibility Principle
/// </summary>
public interface ISettingsService
{
    Task<SchedulerSettings> GetSettingsAsync();
    Task<SchedulerSettings> UpdateSettingsAsync(SchedulerSettings settings);
}
