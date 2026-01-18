using System.Text.Json;
using ShiftScheduler.API.Interfaces;

namespace ShiftScheduler.API.Repositories;

/// <summary>
/// Generic JSON file repository implementation
/// Single Responsibility: Handles file-based persistence
/// </summary>
public class JsonFileRepository<T> : IRepository<T> where T : class
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private List<T>? _cache;

    public JsonFileRepository(string filePath)
    {
        _filePath = filePath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        EnsureDirectoryExists();
    }

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private async Task<List<T>> LoadDataAsync()
    {
        if (_cache != null)
            return _cache;

        if (!File.Exists(_filePath))
        {
            _cache = new List<T>();
            return _cache;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            _cache = JsonSerializer.Deserialize<List<T>>(json, _jsonOptions) ?? new List<T>();
            return _cache;
        }
        catch
        {
            _cache = new List<T>();
            return _cache;
        }
    }

    private async Task SaveDataAsync(List<T> data)
    {
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
        _cache = data;
    }

    public async Task<List<T>> GetAllAsync()
    {
        return await LoadDataAsync();
    }

    public async Task<T?> GetByIdAsync(string id)
    {
        var data = await LoadDataAsync();
        return GetEntityById(data, id);
    }

    protected virtual T? GetEntityById(List<T> data, string id)
    {
        // This will be overridden in derived classes
        return data.FirstOrDefault();
    }

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
        
        if (index == -1)
            return null;

        data[index] = entity;
        await SaveDataAsync(data);
        return entity;
    }

    protected virtual int FindIndexById(List<T> data, string id)
    {
        // This will be overridden in derived classes
        return -1;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var data = await LoadDataAsync();
        var index = FindIndexById(data, id);
        
        if (index == -1)
            return false;

        data.RemoveAt(index);
        await SaveDataAsync(data);
        return true;
    }

    public async Task SaveAsync()
    {
        var data = await LoadDataAsync();
        await SaveDataAsync(data);
    }
}
