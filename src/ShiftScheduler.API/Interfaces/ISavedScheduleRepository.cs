using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Interfaces;

public interface ISavedScheduleRepository : IRepository<SavedSchedule>
{
    Task<SavedSchedule?> GetByShareCodeAsync(string shareCode);
    Task<List<SavedSchedule>> GetSharedSchedulesAsync();
}
