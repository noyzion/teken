using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Repositories;

/// <summary>
/// Position repository - user-scoped (Data/{userId}/positions.json).
/// </summary>
public class PositionRepository : UserScopedJsonRepository<Position>, IPositionRepository
{
    public PositionRepository(IUserContextService userContext)
        : base(userContext, "positions.json")
    {
    }

    protected override Position? GetEntityById(List<Position> data, string id)
        => data.FirstOrDefault(p => p.Id == id);

    protected override int FindIndexById(List<Position> data, string id)
        => data.FindIndex(p => p.Id == id);
}
