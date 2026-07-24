using InfoTrack.Solicitors.Api.Models;
using InfoTrack.Solicitors.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace InfoTrack.Solicitors.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LocationsController : ControllerBase
{
    private readonly ILocationStore _store;
    private readonly IMemoryCache _cache;

    public LocationsController(ILocationStore store, IMemoryCache cache)
    {
        _store = store;
        _cache = cache;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LocationEntity>>> Get([FromQuery] string? sourceName = null, [FromQuery] string? sourceUrl = null)
    {
        // If a custom source is provided, attempt to extract locations directly from it (e.g., CLC xlsx)
        if (!string.IsNullOrWhiteSpace(sourceName) || !string.IsNullOrWhiteSpace(sourceUrl))
        {
            var url = !string.IsNullOrWhiteSpace(sourceUrl) ? sourceUrl : null;
            if (string.Equals(sourceName, "CLC", StringComparison.OrdinalIgnoreCase) && url == null)
            {
                url = "https://www.clc-uk.org/wp-content/uploads/2026/06/List-of-CLC-regulated-practices-as-of-04.06.2026.xlsx";
            }

            if (!string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    var names = await XlsxLocationExtractor.ExtractLocationsFromXlsxUrlAsync(url, CancellationToken.None);
                    var entities = names.Select((n, i) => new LocationEntity { Id = i + 1, Name = n, Checked = true }).ToList();
                    return Ok(entities);
                }
                catch
                {
                    // fallback to stored locations on any failure
                }
            }
        }

        var list = await _store.GetAllAsync();
        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<LocationEntity>> Post([FromBody] LocationEntity create)
    {
        if (string.IsNullOrWhiteSpace(create?.Name)) return BadRequest();

        var entity = await _store.AddAsync(create.Name);

        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Put(int id, [FromBody] LocationEntity update)
    {
        await _store.UpdateAsync(id, update.Checked, update.Name);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        await _store.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("restore")]
    public async Task<ActionResult<IEnumerable<LocationEntity>>> RestoreDefaults([FromQuery] string? sourceName = null, [FromQuery] string? sourceUrl = null)
    {
        // If a source is provided, attempt to seed locations from it and persist to store
        if (!string.IsNullOrWhiteSpace(sourceName) || !string.IsNullOrWhiteSpace(sourceUrl))
        {
            var url = !string.IsNullOrWhiteSpace(sourceUrl) ? sourceUrl : null;
            if (string.Equals(sourceName, "CLC", StringComparison.OrdinalIgnoreCase) && url == null)
            {
                url = "https://www.clc-uk.org/wp-content/uploads/2026/06/List-of-CLC-regulated-practices-as-of-04.06.2026.xlsx";
            }

            if (!string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    var names = await XlsxLocationExtractor.ExtractLocationsFromXlsxUrlAsync(url, CancellationToken.None);

                    // Replace current locations with extracted ones
                    var current = await _store.GetAllAsync();
                    foreach (var c in current) await _store.DeleteAsync(c.Id);
                    var seeded = new List<LocationEntity>();
                    foreach (var n in names)
                    {
                        var e = await _store.AddAsync(n);
                        seeded.Add(e);
                    }

                    // Clear any global deletion blacklist and bump the solicitors cache version so clients see refreshed results
                    _cache.Remove("solicitors:deleted");
                    _cache.Set("solicitors:version", Guid.NewGuid().ToString());

                    return Ok(seeded);
                }
                catch
                {
                    // fallback to normal restore on failure
                }
            }
        }

        var seededDefault = await _store.RestoreDefaultsAsync();

        // Clear any global deletion blacklist and bump the solicitors cache version so clients see refreshed results
        _cache.Remove("solicitors:deleted");
        _cache.Set("solicitors:version", Guid.NewGuid().ToString());

        return Ok(seededDefault);
    }
}
