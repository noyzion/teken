using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;
using System.Reflection;

namespace ShiftScheduler.API.Repositories;

/// <summary>
/// Repository for managing soldier groups
/// </summary>
public class SoldierGroupRepository : JsonFileRepository<SoldierGroup>, ISoldierGroupRepository
{
    public SoldierGroupRepository() : base(
        Path.Combine(AppContext.BaseDirectory, "Data", "soldierGroups.json"))
    {
    }

    protected override SoldierGroup? GetEntityById(List<SoldierGroup> data, string id)
    {
        return data.FirstOrDefault(g => g.Id == id);
    }

    protected override int FindIndexById(List<SoldierGroup> data, string id)
    {
        return data.FindIndex(g => g.Id == id);
    }
}
