using InfoTrack.Solicitors.Api.Models;

namespace InfoTrack.Solicitors.Api.Services;

public interface ISavedSearchStore
{
    Task<IReadOnlyList<SavedSearchResult>> GetAllAsync();
    Task<SavedSearchResult?> GetByIdAsync(int id);
    Task<SavedSearchResult> SaveAsync(string name, string[] locations, SolicitorResult[] results);
    Task<bool> DeleteAsync(int id);
}
