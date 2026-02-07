using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Interfaces;

public interface ICurrentScheduleRepository
{
    Task<List<DaySchedule>> GetAsync();
    Task SaveAsync(List<DaySchedule> schedule);
}
