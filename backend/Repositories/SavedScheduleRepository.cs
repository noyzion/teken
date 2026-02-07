using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Repositories;

public class SavedScheduleRepository : JsonFileRepository<SavedSchedule>, ISavedScheduleRepository
{
    public SavedScheduleRepository() : base(
        Path.Combine(AppContext.BaseDirectory, "Data", "savedSchedules.json"))
    {
    }

    protected override SavedSchedule? GetEntityById(List<SavedSchedule> data, string id)
    {
        return data.FirstOrDefault(s => s.Id == id);
    }

    protected override int FindIndexById(List<SavedSchedule> data, string id)
    {
        return data.FindIndex(s => s.Id == id);
    }

    public async Task<SavedSchedule?> GetByShareCodeAsync(string shareCode)
    {
        var all = await GetAllAsync();
        return all.FirstOrDefault(s => s.IsShared && s.ShareCode == shareCode);
    }

    public async Task<List<SavedSchedule>> GetSharedSchedulesAsync()
    {
        var all = await GetAllAsync();
        return all.Where(s => s.IsShared).ToList();
    }
}
