using System.Text.Json;
using InfoTrack.Solicitors.Api.Models;

namespace InfoTrack.Solicitors.Api.Services;

public sealed class InMemoryLocationStore : ILocationStore
{
    private readonly object _syncRoot = new();
    private readonly List<LocationEntity> _locationsCache = new();
    private int _nextLocationId = 1;
    private readonly string _storageFilePath;
    private readonly ILogger<InMemoryLocationStore>? _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    private static readonly string[] DefaultLocationNames = new[] { "London", "Birmingham", "Leeds", "Manchester", "Sheffield", "Bradford", "Liverpool", "Bristol" };

    public InMemoryLocationStore(IHostEnvironment hostEnvironment, ILogger<InMemoryLocationStore> logger)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dataDirectory = Path.Combine(appData ?? hostEnvironment.ContentRootPath ?? ".", "InfoTrack.Solicitors");
        try { Directory.CreateDirectory(dataDirectory); }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not create data directory {Dir}, falling back to content root.", dataDirectory);
            dataDirectory = Path.Combine(hostEnvironment.ContentRootPath ?? ".", "data");
            Directory.CreateDirectory(dataDirectory);
        }

        _storageFilePath = Path.Combine(dataDirectory, "locations.json");

        if (File.Exists(_storageFilePath)) LoadFromStorage();
        else InitializeDefaults();
    }

    private void LoadFromStorage()
    {
        try
        {
            var json = File.ReadAllText(_storageFilePath);
            var items = JsonSerializer.Deserialize<List<LocationEntity>>(json, _jsonOptions);
            if (items?.Count > 0)
            {
                _locationsCache.AddRange(items);
                _nextLocationId = _locationsCache.Max(x => x.Id) + 1;
            }
        }
        catch (Exception ex) { _logger?.LogWarning(ex, "Failed to load locations from {Path}", _storageFilePath); }
    }

    private void InitializeDefaults()
    {
        foreach (var name in DefaultLocationNames) _locationsCache.Add(new LocationEntity { Id = _nextLocationId++, Name = name, Checked = true });
        PersistToFile();
    }

    public Task<IReadOnlyList<LocationEntity>> GetAllAsync(CancellationToken _) =>
        Task.FromResult((IReadOnlyList<LocationEntity>)CreateSnapshot());

    public Task<LocationEntity> AddAsync(string name, CancellationToken _)
    {
        lock (_syncRoot)
        {
            var e = new LocationEntity { Id = _nextLocationId++, Name = name.Trim(), Checked = true };
            _locationsCache.Add(e);
            PersistToFile();
            return Task.FromResult(e);
        }
    }

    public Task UpdateAsync(int id, bool isChecked, string? name = null, CancellationToken _ = default)
    {
        lock (_syncRoot)
        {
            var existing = _locationsCache.FirstOrDefault(x => x.Id == id);
            if (existing != null) { existing.Checked = isChecked; if (!string.IsNullOrWhiteSpace(name)) existing.Name = name.Trim(); PersistToFile(); }
            return Task.CompletedTask;
        }
    }

    public Task DeleteAsync(int id, CancellationToken _ = default)
    {
        lock (_syncRoot)
        {
            _locationsCache.RemoveAll(x => x.Id == id);
            PersistToFile();
            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyList<LocationEntity>> RestoreDefaultsAsync(CancellationToken _ = default)
    {
        lock (_syncRoot)
        {
            _locationsCache.Clear();
            foreach (var name in DefaultLocationNames) _locationsCache.Add(new LocationEntity { Id = _nextLocationId++, Name = name, Checked = true });
            PersistToFile();
            return Task.FromResult((IReadOnlyList<LocationEntity>)CreateSnapshot());
        }
    }

    private List<LocationEntity> CreateSnapshot() => _locationsCache.Select(x => new LocationEntity { Id = x.Id, Name = x.Name, Checked = x.Checked }).ToList();

    private void PersistToFile()
    {
        try
        {
            var json = JsonSerializer.Serialize(CreateSnapshot(), _jsonOptions);
            var tmp = _storageFilePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _storageFilePath, overwrite: true);
        }
        catch (Exception ex) { _logger?.LogWarning(ex, "Failed to persist locations to {Path}", _storageFilePath); }
    }
}
