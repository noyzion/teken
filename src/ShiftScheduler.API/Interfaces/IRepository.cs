namespace ShiftScheduler.API.Interfaces;

/// <summary>
/// Generic repository interface following Repository Pattern
/// </summary>
public interface IRepository<T> where T : class
{
    Task<List<T>> GetAllAsync();
    Task<T?> GetByIdAsync(string id);
    Task<T> CreateAsync(T entity);
    Task<T?> UpdateAsync(string id, T entity);
    Task<bool> DeleteAsync(string id);
    Task SaveAsync();
}
