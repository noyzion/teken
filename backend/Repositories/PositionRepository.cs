using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Repositories;

/// <summary>
/// Position repository implementation
/// Single Responsibility: Manages Position entities
/// </summary>
public class PositionRepository : JsonFileRepository<Position>, IPositionRepository
{
    public PositionRepository() : base(Path.Combine(AppContext.BaseDirectory, "Data", "positions.json"))
    {
    }

    protected override Position? GetEntityById(List<Position> data, string id)
    {
        return data.FirstOrDefault(p => p.Id == id);
    }

    protected override int FindIndexById(List<Position> data, string id)
    {
        return data.FindIndex(p => p.Id == id);
    }
}
