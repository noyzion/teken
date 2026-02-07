using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Repositories;

/// <summary>
/// Soldier groups repository - user-scoped (Data/{userId}/soldierGroups.json).
/// </summary>
public class SoldierGroupRepository : UserScopedJsonRepository<SoldierGroup>, ISoldierGroupRepository
{
    public SoldierGroupRepository(IUserContextService userContext)
        : base(userContext, "soldierGroups.json")
    {
    }

    protected override SoldierGroup? GetEntityById(List<SoldierGroup> data, string id)
        => data.FirstOrDefault(g => g.Id == id);

    protected override int FindIndexById(List<SoldierGroup> data, string id)
        => data.FindIndex(g => g.Id == id);
}
