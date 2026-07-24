using Microsoft.Extensions.Caching.Memory;

namespace InfoTrack.Solicitors.Api.Services;

public sealed class CacheWarmupService : BackgroundService
{
    private readonly ILogger<CacheWarmupService> _logger;
    private readonly IMemoryCache _cache;
    private readonly ISolicitorScraper _scraper;

    // Default locations to warm cache for. Keep in sync with frontend defaults if needed.
    private static readonly string[] DefaultLocations = new[]
    {
        "London",
        "Birmingham",
        "Leeds",
        "Manchester",
        "Sheffield",
        "Bradford",
        "Liverpool",
        "Bristol",
    };

    public CacheWarmupService(
        ILogger<CacheWarmupService> logger,
        IMemoryCache cache,
        ISolicitorScraper scraper)
    {
        _logger = logger;
        _cache = cache;
        _scraper = scraper;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Cache warmup starting for default locations.");

            var key = "__all_locations__";

            // If already cached, skip
            if (!_cache.TryGetValue(key, out _))
            {
                var results = await _scraper.ScrapeAsync(DefaultLocations, stoppingToken);

                // cache for 10 minutes
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                };

                _cache.Set(key, results.ToList(), options);

                _logger.LogInformation("Cache warmup completed: {Count} items cached.", results.Count);
            }
            else
            {
                _logger.LogInformation("Cache warmup skipped: cache already populated.");
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // ignore
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache warmup failed.");
        }
    }
}
