using InfoTrack.Solicitors.Api.Models;

namespace InfoTrack.Solicitors.Api.Services;

public interface ILocationStore
{
    Task<IReadOnlyList<LocationEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<LocationEntity> AddAsync(string name, CancellationToken cancellationToken = default);
    Task UpdateAsync(int id, bool isChecked, string? name = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LocationEntity>> RestoreDefaultsAsync(CancellationToken cancellationToken = default);
}
