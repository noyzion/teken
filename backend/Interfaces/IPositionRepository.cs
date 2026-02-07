using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Interfaces;

/// <summary>
/// Position repository interface - Interface Segregation Principle
/// </summary>
public interface IPositionRepository : IRepository<Position>
{
}
