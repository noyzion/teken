using System.Text.Json;
using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Repositories;

public class UserRepository : IUserRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private List<User>? _cache;

    public UserRepository()
    {
        _filePath = Path.Combine(AppContext.BaseDirectory, "Data", "users.json");
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        EnsureDirectoryExists();
    }

    private void EnsureDirectoryExists()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    private async Task<List<User>> LoadAsync()
    {
        if (_cache != null) return _cache;
        if (!File.Exists(_filePath))
        {
            _cache = new List<User>();
            return _cache;
        }
        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            _cache = JsonSerializer.Deserialize<List<User>>(json, _jsonOptions) ?? new List<User>();
            return _cache;
        }
        catch
        {
            _cache = new List<User>();
            return _cache;
        }
    }

    private async Task SaveAsync(List<User> data)
    {
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
        _cache = data;
    }

    public async Task<List<User>> GetAllAsync() => await LoadAsync();

    public async Task<User?> GetByIdAsync(string id)
    {
        var data = await LoadAsync();
        return data.FirstOrDefault(u => u.Id == id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var data = await LoadAsync();
        return data.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<User> CreateAsync(User user)
    {
        var data = await LoadAsync();
        data.Add(user);
        await SaveAsync(data);
        return user;
    }
}
