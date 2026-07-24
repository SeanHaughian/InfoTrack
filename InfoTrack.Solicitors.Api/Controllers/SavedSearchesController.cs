using InfoTrack.Solicitors.Api.Models;
using InfoTrack.Solicitors.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace InfoTrack.Solicitors.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SavedSearchesController : ControllerBase
{
    private readonly ISavedSearchStore _store;

    public SavedSearchesController(ISavedSearchStore store)
    {
        _store = store;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SavedSearchResult>>> GetAll()
    {
        var results = await _store.GetAllAsync();
        return Ok(results);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SavedSearchResult>> GetById(int id)
    {
        var result = await _store.GetByIdAsync(id);
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<SavedSearchResult>> Save([FromBody] SaveSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required");
        }

        if (request.Locations == null || request.Locations.Length == 0)
        {
            return BadRequest("Locations are required");
        }

        if (request.Results == null)
        {
            return BadRequest("Results are required");
        }

        var saved = await _store.SaveAsync(request.Name, request.Locations, request.Results);
        return CreatedAtAction(nameof(GetById), new { id = saved.Id }, saved);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var deleted = await _store.DeleteAsync(id);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpPost("compare")]
    public async Task<ActionResult<ComparisonResult>> Compare([FromBody] CompareRequest request)
    {
        var search1 = await _store.GetByIdAsync(request.Id1);
        var search2 = await _store.GetByIdAsync(request.Id2);

        if (search1 == null || search2 == null)
        {
            return NotFound("One or both saved searches not found");
        }

        // Validate locations match
        var locations1 = search1.Locations.OrderBy(l => l).ToArray();
        var locations2 = search2.Locations.OrderBy(l => l).ToArray();

        if (!locations1.SequenceEqual(locations2, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                error = "Cannot compare searches with different locations",
                search1Locations = search1.Locations,
                search2Locations = search2.Locations
            });
        }

        // Perform comparison - using Phone as primary key (most stable identifier)
        // Fall back to Name+Location+Address if phone is missing
        // Use GroupBy to handle potential duplicates
        string GetKey(SolicitorResult r) => 
            !string.IsNullOrWhiteSpace(r.Phone) 
                ? $"PHONE:{r.Phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "")}"
                : $"NAME:{r.Name}|{r.Location}|{r.Address ?? ""}";

        var results1Dict = search1.Results
            .GroupBy(r => GetKey(r), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var results2Dict = search2.Results
            .GroupBy(r => GetKey(r), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var added = search2.Results
            .GroupBy(r => GetKey(r), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Where(r => !results1Dict.ContainsKey(GetKey(r)))
            .ToArray();

        var removed = search1.Results
            .GroupBy(r => GetKey(r), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Where(r => !results2Dict.ContainsKey(GetKey(r)))
            .ToArray();

        var unchanged = search1.Results
            .GroupBy(r => GetKey(r), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Where(r => results2Dict.ContainsKey(GetKey(r)))
            .ToArray();

        return Ok(new ComparisonResult(
            search1,
            search2,
            added,
            removed,
            unchanged
        ));
    }
}

public sealed record SaveSearchRequest(
    string Name,
    string[] Locations,
    SolicitorResult[] Results);

public sealed record CompareRequest(int Id1, int Id2);

public sealed record ComparisonResult(
    SavedSearchResult Search1,
    SavedSearchResult Search2,
    SolicitorResult[] Added,
    SolicitorResult[] Removed,
    SolicitorResult[] Unchanged);
