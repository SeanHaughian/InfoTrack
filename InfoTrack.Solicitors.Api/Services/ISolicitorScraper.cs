using InfoTrack.Solicitors.Api.Models;

namespace InfoTrack.Solicitors.Api.Services;

public interface ISolicitorScraper
{
    Task<IReadOnlyList<SolicitorResult>> ScrapeAsync(
        IEnumerable<string> locations,
        CancellationToken cancellationToken = default);
}