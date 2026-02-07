using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Repositories;

/// <summary>
/// Soldier repository - user-scoped (Data/{userId}/soldiers.json).
/// </summary>
public class SoldierRepository : UserScopedJsonRepository<Soldier>, ISoldierRepository
{
    public SoldierRepository(IUserContextService userContext)
        : base(userContext, "soldiers.json")
    {
    }

    protected override Soldier? GetEntityById(List<Soldier> data, string id)
        => data.FirstOrDefault(s => s.Id == id);

    protected override int FindIndexById(List<Soldier> data, string id)
        => data.FindIndex(s => s.Id == id);
}
