using System.Text.Json;
using ShiftScheduler.API.Interfaces;

namespace ShiftScheduler.API.Repositories;

/// <summary>
/// JSON file repository scoped to current user: Data/{userId}/{fileName}.
/// </summary>
public abstract class UserScopedJsonRepository<T> : IRepository<T> where T : class
{
    private readonly IUserContextService _userContext;
    private readonly string _fileName;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _lastPath;
    private List<T>? _cache;

    protected UserScopedJsonRepository(IUserContextService userContext, string fileName)
    {
        _userContext = userContext;
        _fileName = fileName;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    private string GetFilePath()
    {
        var userId = _userContext.GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return string.Empty;
        return Path.Combine(AppContext.BaseDirectory, "Data", userId, _fileName);
    }

    private void EnsureDirectoryExists(string? path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    private async Task<List<T>> LoadDataAsync()
    {
        var path = GetFilePath();
        if (string.IsNullOrEmpty(path)) return new List<T>();

        EnsureDirectoryExists(path);

        if (_cache != null && _lastPath == path)
            return _cache;

        if (!File.Exists(path))
        {
            _cache = new List<T>();
            _lastPath = path;
            return _cache;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            _cache = JsonSerializer.Deserialize<List<T>>(json, _jsonOptions) ?? new List<T>();
            _lastPath = path;
            return _cache;
        }
        catch
        {
            _cache = new List<T>();
            _lastPath = path;
            return _cache;
        }
    }

    private async Task SaveDataAsync(List<T> data)
    {
        var path = GetFilePath();
        if (string.IsNullOrEmpty(path)) return;
        EnsureDirectoryExists(path);
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        await File.WriteAllTextAsync(path, json);
        _cache = data;
        _lastPath = path;
    }

    public async Task<List<T>> GetAllAsync() => await LoadDataAsync();

    public async Task<T?> GetByIdAsync(string id)
    {
        var data = await LoadDataAsync();
        return GetEntityById(data, id);
    }

    protected abstract T? GetEntityById(List<T> data, string id);
    protected abstract int FindIndexById(List<T> data, string id);

    public async Task<T> CreateAsync(T entity)
    {
        var data = await LoadDataAsync();
        data.Add(entity);
        await SaveDataAsync(data);
        return entity;
    }

    public async Task<T?> UpdateAsync(string id, T entity)
    {
        var data = await LoadDataAsync();
        var index = FindIndexById(data, id);
        if (index == -1) return null;
        data[index] = entity;
        await SaveDataAsync(data);
        return entity;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var data = await LoadDataAsync();
        var index = FindIndexById(data, id);
        if (index == -1) return false;
        data.RemoveAt(index);
        await SaveDataAsync(data);
        return true;
    }

    public Task SaveAsync() => Task.CompletedTask;
}
