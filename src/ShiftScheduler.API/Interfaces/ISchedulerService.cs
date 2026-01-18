using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Interfaces;

/// <summary>
/// Scheduler service interface - Single Responsibility Principle
/// </summary>
public interface ISchedulerService
{
    Task<List<DaySchedule>> GenerateScheduleAsync(ScheduleConfig config);
}
