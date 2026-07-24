using System.Collections.Concurrent;
using System.Text.Json;
using InfoTrack.Solicitors.Api.Models;

namespace InfoTrack.Solicitors.Api.Services;

public sealed class InMemorySavedSearchStore : ISavedSearchStore
{
    private readonly ConcurrentDictionary<int, SavedSearchResult> _store = new();
    private int _nextId = 1;
    private readonly string _filePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public InMemorySavedSearchStore()
    {
        _filePath = Path.Combine(Path.GetTempPath(), "InfoTrack_SavedSearches.json");
        LoadFromFile();
    }

    private void LoadFromFile()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var data = JsonSerializer.Deserialize<SavedSearchData>(json);
                if (data != null)
                {
                    foreach (var search in data.Searches)
                    {
                        _store[search.Id] = search;
                    }
                    _nextId = data.NextId;
                }
            }
        }
        catch
        {
            // If loading fails, start fresh
        }
    }

    private async Task SaveToFileAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var data = new SavedSearchData
            {
                NextId = _nextId,
                Searches = _store.Values.ToList()
            };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch
        {
            // Silent fail - persistence is a nice-to-have
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public Task<IReadOnlyList<SavedSearchResult>> GetAllAsync()
    {
        var results = _store.Values.OrderByDescending(s => s.Timestamp).ToList();
        return Task.FromResult<IReadOnlyList<SavedSearchResult>>(results);
    }

    public Task<SavedSearchResult?> GetByIdAsync(int id)
    {
        _store.TryGetValue(id, out var result);
        return Task.FromResult(result);
    }

    public async Task<SavedSearchResult> SaveAsync(string name, string[] locations, SolicitorResult[] results)
    {
        var id = Interlocked.Increment(ref _nextId);
        var savedSearch = new SavedSearchResult(
            id,
            name,
            DateTime.UtcNow,
            locations,
            results);

        _store[id] = savedSearch;
        await SaveToFileAsync();
        return savedSearch;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var deleted = _store.TryRemove(id, out _);
        if (deleted)
        {
            await SaveToFileAsync();
        }
        return deleted;
    }

    private sealed class SavedSearchData
    {
        public int NextId { get; set; }
        public List<SavedSearchResult> Searches { get; set; } = new();
    }
}
