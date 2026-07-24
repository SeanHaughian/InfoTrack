using System.Text.RegularExpressions;
using InfoTrack.Solicitors.Api.Models;

namespace InfoTrack.Solicitors.Api.Services;

public sealed class SolicitorScraper : ISolicitorScraper
{
    private readonly HttpClient _httpClient;
    private readonly ISolicitorHtmlParser _parser;
    private readonly ILogger<SolicitorScraper> _logger;

    public SolicitorScraper(
        HttpClient httpClient,
        ISolicitorHtmlParser parser,
        ILogger<SolicitorScraper> logger)
    {
        _httpClient = httpClient;
        _parser = parser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SolicitorResult>> ScrapeAsync(
        IEnumerable<string> locations,
        CancellationToken cancellationToken = default)
    {
        var requestedLocations = locations
            .Where(location => !string.IsNullOrWhiteSpace(location))
            .Select(location => location.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var results = new List<SolicitorResult>();

        foreach (var location in requestedLocations)
        {
            try
            {
                var html = await _httpClient.GetStringAsync(
                    BuildUrl(location),
                    cancellationToken);

                results.AddRange(_parser.Parse(html, location));

                await Task.Delay(50, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Unable to scrape solicitors for {Location}",
                    location);
            }
        }

        // Deduplicate by office identity (Name + Location + Address)
        return DeduplicateByOffice(results);
    }

    private static IReadOnlyList<SolicitorResult> DeduplicateByOffice(List<SolicitorResult> results)
    {
        // Sort results first to ensure deterministic processing order
        // This eliminates non-determinism from varying HTML parse order or HTTP response timing
        var sorted = results
            .OrderBy(r => r.Location, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Address, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduped = new List<SolicitorResult>();

        foreach (var result in sorted)
        {
            // Create a unique key based on Name + Location + Address (no normalization)
            var uniqueKey = $"{result.Name?.Trim() ?? ""}|{result.Location?.Trim() ?? ""}|{result.Address?.Trim() ?? ""}";

            // Only add if we haven't seen this exact combination
            if (seen.Add(uniqueKey))
            {
                deduped.Add(result);
            }
        }

        return deduped;
    }

    private static string BuildUrl(string location)
    {
        var slug = location
            .ToLowerInvariant()
            .Replace(" ", "-");

        // Use the correct host and HTTPS scheme
        return $"https://www.solicitors.com/{Uri.EscapeDataString(slug)}-solicitors.html";
    }
}
