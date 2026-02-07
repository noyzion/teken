using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Interfaces;

/// <summary>
/// Scheduler service interface - Single Responsibility Principle
/// </summary>
public interface ISchedulerService
{
    Task<List<DaySchedule>> GenerateScheduleAsync(ScheduleConfig config);
    Task<ScheduleValidationReport> GetScheduleValidationReportAsync(List<DaySchedule> schedule);
    /// <summary>בדיקת אילוצים קשיחים (סעיף 1) – אם יש הפרות, הרשימה לא תקינה.</summary>
    Task<ScheduleHardConstraintsValidationResult> ValidateScheduleHardConstraintsAsync(List<DaySchedule> schedule);
}
