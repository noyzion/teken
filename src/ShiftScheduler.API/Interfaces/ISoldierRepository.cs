using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Interfaces;

/// <summary>
/// Soldier repository interface - Interface Segregation Principle
/// </summary>
public interface ISoldierRepository : IRepository<Soldier>
{
}
