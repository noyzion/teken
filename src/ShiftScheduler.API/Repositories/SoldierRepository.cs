using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Repositories;

/// <summary>
/// Soldier repository implementation
/// Single Responsibility: Manages Soldier entities
/// </summary>
public class SoldierRepository : JsonFileRepository<Soldier>, ISoldierRepository
{
    public SoldierRepository() : base(Path.Combine(AppContext.BaseDirectory, "Data", "soldiers.json"))
    {
    }

    protected override Soldier? GetEntityById(List<Soldier> data, string id)
    {
        return data.FirstOrDefault(s => s.Id == id);
    }

    protected override int FindIndexById(List<Soldier> data, string id)
    {
        return data.FindIndex(s => s.Id == id);
    }
}
