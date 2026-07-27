using InfoTrack.Solicitors.Api.Models;
using InfoTrack.Solicitors.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Xml.Linq;
using System.Text.RegularExpressions;


namespace InfoTrack.Solicitors.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SolicitorsController : ControllerBase
{
    private readonly ISolicitorScraper _scraper;
    private readonly IMemoryCache _cache;

    public SolicitorsController(ISolicitorScraper scraper, IMemoryCache cache)
    {
        _scraper = scraper;
        _cache = cache;
    }

    // Export current filtered results as JSON file
    [HttpGet("export/json")]
    public async Task<IActionResult> ExportJson([
        FromQuery] string? locations,
        [FromQuery] string? sourceName = null,
        [FromQuery] string? sourceUrl = null)
    {
        // Reuse same cache key construction as other endpoints
        var requested = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(locations))
        {
            requested = locations.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        }

        var cacheKey = requested == null || requested.Length == 0
            ? "__all_locations__"
            : string.Join("|", requested.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(sourceName) || !string.IsNullOrWhiteSpace(sourceUrl))
        {
            var src = !string.IsNullOrWhiteSpace(sourceName) ? sourceName : sourceUrl;
            cacheKey = $"{cacheKey}:source={src}";
        }

        var version = _cache.Get<string>("solicitors:version") ?? string.Empty;
        var versionedKey = string.IsNullOrEmpty(version) ? cacheKey : $"{version}:{cacheKey}";

        var fullList = await _cache.GetOrCreateAsync(versionedKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            IReadOnlyList<SolicitorResult> all;
            if (!string.IsNullOrWhiteSpace(sourceUrl) || string.Equals(sourceName, "CLC", StringComparison.OrdinalIgnoreCase))
            {
                var url = !string.IsNullOrWhiteSpace(sourceUrl)
                    ? sourceUrl
                    : "https://www.clc-uk.org/wp-content/uploads/2026/06/List-of-CLC-regulated-practices-as-of-04.06.2026.xlsx";

                var scrapedFromXlsx = await ScrapeFromXlsxUrlAsync(url, HttpContext.RequestAborted);
                all = scrapedFromXlsx;
            }
            else
            {
                var scraped = await _scraper.ScrapeAsync(requested, HttpContext.RequestAborted);
                all = scraped;
            }

            // Apply any global deletions
            var deleted = _cache.Get<HashSet<string>>("solicitors:deleted");
            var listAll = all.ToList();
            if (deleted != null && deleted.Count > 0)
            {
                listAll = listAll.Where(s => !deleted.Contains(MakeUniqueKey(s.Name, s.Location, s.Website))).ToList();
            }

            return listAll;
        });

        var json = JsonSerializer.Serialize(fullList, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        var fname = $"solicitors-{DateTime.UtcNow:yyyy-MM-ddTHH-mm-ssZ}.json";
        return File(bytes, "application/json", fname);
    }

    // Export current filtered results as CSV (served as download). Produces a simple CSV which Excel can open.
    [HttpGet("export/excel")]
    public async Task<IActionResult> ExportExcel([
        FromQuery] string? locations,
        [FromQuery] string? sourceName = null,
        [FromQuery] string? sourceUrl = null)
    {
        // Reuse same cache retrieval logic
        var requested = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(locations))
        {
            requested = locations.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        }

        var cacheKey = requested == null || requested.Length == 0
            ? "__all_locations__"
            : string.Join("|", requested.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(sourceName) || !string.IsNullOrWhiteSpace(sourceUrl))
        {
            var src = !string.IsNullOrWhiteSpace(sourceName) ? sourceName : sourceUrl;
            cacheKey = $"{cacheKey}:source={src}";
        }

        var version = _cache.Get<string>("solicitors:version") ?? string.Empty;
        var versionedKey = string.IsNullOrEmpty(version) ? cacheKey : $"{version}:{cacheKey}";

        var fullList = await _cache.GetOrCreateAsync(versionedKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            IReadOnlyList<SolicitorResult> all;
            if (!string.IsNullOrWhiteSpace(sourceUrl) || string.Equals(sourceName, "CLC", StringComparison.OrdinalIgnoreCase))
            {
                var url = !string.IsNullOrWhiteSpace(sourceUrl)
                    ? sourceUrl
                    : "https://www.clc-uk.org/wp-content/uploads/2026/06/List-of-CLC-regulated-practices-as-of-04.06.2026.xlsx";

                var scrapedFromXlsx = await ScrapeFromXlsxUrlAsync(url, HttpContext.RequestAborted);
                all = scrapedFromXlsx;
            }
            else
            {
                var scraped = await _scraper.ScrapeAsync(requested, HttpContext.RequestAborted);
                all = scraped;
            }

            // Apply any global deletions
            var deleted = _cache.Get<HashSet<string>>("solicitors:deleted");
            var listAll = all.ToList();
            if (deleted != null && deleted.Count > 0)
            {
                listAll = listAll.Where(s => !deleted.Contains(MakeUniqueKey(s.Name, s.Location, s.Website))).ToList();
            }

            return listAll;
        });

        var sb = new StringBuilder();
        sb.AppendLine("Name,Location,Address,Website,Phone,ReviewCount");
        foreach (var s in fullList)
        {
            var name = EscapeCsv(s.Name);
            var loc = EscapeCsv(s.Location);
            var addr = EscapeCsv(s.Address ?? string.Empty);
            var web = EscapeCsv(s.Website ?? string.Empty);
            var phone = EscapeCsv(s.Phone ?? string.Empty);
            var rev = s.ReviewCount?.ToString() ?? string.Empty;
            sb.AppendLine($"{name},{loc},{addr},{web},{phone},{rev}");
        }

        var csv = sb.ToString();
        var bytes = Encoding.UTF8.GetBytes(csv);
        var fname = $"solicitors-{DateTime.UtcNow:yyyy-MM-ddTHH-mm-ssZ}.csv";
        return File(bytes, "text/csv", fname);
    }

    private static string EscapeCsv(string s)
    {
        if (s == null) return string.Empty;
        if (s.Contains('"')) s = s.Replace("\"", "\"\"");
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
        {
            return '"' + s + '"';
        }
        return s;
    }

    // Minimal XLSX (Office Open XML) parser that reads the first worksheet and shared strings
    // without adding external NuGet dependencies. Heuristic mapping of columns is used to
    // locate name and location columns.
    private static async Task<List<SolicitorResult>> ScrapeFromXlsxUrlAsync(string url, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        using var resp = await client.GetAsync(url, cancellationToken);
        resp.EnsureSuccessStatusCode();

        await using var ms = new MemoryStream();
        await resp.Content.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;

        using var archive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);

        // Load shared strings if present
        var shared = new List<string>();
        var sstEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sstEntry != null)
        {
            using var sstStream = sstEntry.Open();
            var sstDoc = XDocument.Load(sstStream);
            XNamespace ns = sstDoc.Root?.Name.Namespace ?? "";
            foreach (var si in sstDoc.Descendants(ns + "si"))
            {
                var texts = si.Descendants(ns + "t").Select(t => t.Value ?? string.Empty);
                shared.Add(string.Concat(texts));
            }
        }

        // Find first worksheet entry (fallback to sheet1.xml)
        var sheetEntry = archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
        if (sheetEntry == null)
        {
            return new List<SolicitorResult>();
        }

        using var sheetStream = sheetEntry.Open();
        var sheetDoc = XDocument.Load(sheetStream);
        XNamespace sx = sheetDoc.Root?.Name.Namespace ?? "";

        // Parse rows into list of dictionaries keyed by column letter (A, B, C...)
        var rows = new List<Dictionary<string, string>>();
        foreach (var row in sheetDoc.Descendants(sx + "row"))
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in row.Elements(sx + "c"))
            {
                var r = (string?)c.Attribute("r") ?? string.Empty; // e.g. A1
                var col = Regex.Replace(r, "\\d", string.Empty); // drop digits
                var t = (string?)c.Attribute("t");
                var v = c.Element(sx + "v")?.Value ?? string.Empty;
                string value;
                if (string.Equals(t, "s", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(v, out var idx) && idx >= 0 && idx < shared.Count)
                        value = shared[idx];
                    else
                        value = string.Empty;
                }
                else
                {
                    value = v;
                }

                dict[col] = value?.Trim() ?? string.Empty;
            }

            if (dict.Count > 0) rows.Add(dict);
        }

        if (rows.Count == 0) return new List<SolicitorResult>();

        // First row assumed header; find name/location columns by header heuristics
        var header = rows[0];
        // Default heuristics
        string nameCol = header.Keys.FirstOrDefault(k => Regex.IsMatch(header[k], "(practice|firm|name)", RegexOptions.IgnoreCase)) ?? header.Keys.First();
        string locationCol = header.Keys.FirstOrDefault(k => Regex.IsMatch(header[k], "(town|location|city|county|address|locality|postcode)", RegexOptions.IgnoreCase)) ?? header.Keys.Skip(1).FirstOrDefault() ?? nameCol;

        // Additional columns we may populate when available
        string? addressCol = header.Keys.FirstOrDefault(k => Regex.IsMatch(header[k], "(address|address_1|address1)", RegexOptions.IgnoreCase));
        string? websiteCol = header.Keys.FirstOrDefault(k => Regex.IsMatch(header[k], "(website|website_url|url|site)", RegexOptions.IgnoreCase));

        // If this appears to be the CLC provided sheet, use strict mappings: Location=City, Address=Address_1, Link=Website_URL
        // Detect by URL presence of clc-uk.org in the uploaded file path (heuristic)
        if (!string.IsNullOrWhiteSpace(sheetEntry?.Name) && sheetEntry.FullName.IndexOf("clc-uk.org", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // unlikely to trigger because entries are inside zip, so also check the original request URL via known header names
        }
        // Try to detect CLC by inspecting header values directly
        var headerValuesCombined = string.Join("|", header.Values).ToLowerInvariant();
        if (headerValuesCombined.Contains("city") && headerValuesCombined.Contains("address_1") && headerValuesCombined.Contains("website_url"))
        {
            locationCol = header.Keys.FirstOrDefault(k => string.Equals(header[k], "City", StringComparison.OrdinalIgnoreCase)) ?? locationCol;
            addressCol = header.Keys.FirstOrDefault(k => string.Equals(header[k], "Address_1", StringComparison.OrdinalIgnoreCase)) ?? addressCol;
            websiteCol = header.Keys.FirstOrDefault(k => string.Equals(header[k], "Website_URL", StringComparison.OrdinalIgnoreCase)) ?? websiteCol;
        }

        var results = new List<SolicitorResult>();

        // Iterate data rows after header
        for (int i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            r.TryGetValue(nameCol, out var name);
            r.TryGetValue(locationCol, out var location);

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(location)) continue;

            name = name ?? string.Empty;
            location = location ?? string.Empty;

            string? address = null;
            if (!string.IsNullOrWhiteSpace(addressCol)) r.TryGetValue(addressCol, out address);
            string? website = null;
            if (!string.IsNullOrWhiteSpace(websiteCol)) r.TryGetValue(websiteCol, out website);

            address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
            website = string.IsNullOrWhiteSpace(website) ? null : website.Trim();

            // Normalize website if it looks like a domain but lacks scheme
            if (website != null && !website.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !website.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (website.Contains("www.") || website.Contains("."))
                {
                    website = "https://" + website;
                }
            }

            results.Add(new SolicitorResult(
                Name: name.Trim(),
                Location: location.Trim(),
                Address: address,
                Phone: null,
                Website: website,
                ReviewCount: null));
        }

        return results;
    }

    // Helper: get the full list of solicitors for the supplied query parameters.
    // This consolidates caching and source selection so controller actions stay simple.
    private async Task<List<SolicitorResult>> GetFullListAsync(string? locations, string? sourceName, string? sourceUrl, CancellationToken cancellationToken)
    {
        var requested = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(locations))
        {
            requested = locations.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        }

        var cacheKey = requested == null || requested.Length == 0
            ? "__all_locations__"
            : string.Join("|", requested.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(sourceName) || !string.IsNullOrWhiteSpace(sourceUrl))
        {
            var src = !string.IsNullOrWhiteSpace(sourceName) ? sourceName : sourceUrl;
            cacheKey = $"{cacheKey}:source={src}";
        }

        var version = _cache.Get<string>("solicitors:version") ?? string.Empty;
        var versionedKey = string.IsNullOrEmpty(version) ? cacheKey : $"{version}:{cacheKey}";

        var fullList = await _cache.GetOrCreateAsync(versionedKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            IReadOnlyList<SolicitorResult> all;
            if (!string.IsNullOrWhiteSpace(sourceUrl) || string.Equals(sourceName, "CLC", StringComparison.OrdinalIgnoreCase))
            {
                var url = !string.IsNullOrWhiteSpace(sourceUrl)
                    ? sourceUrl
                    : "https://www.clc-uk.org/wp-content/uploads/2026/06/List-of-CLC-regulated-practices-as-of-04.06.2026.xlsx";

                all = await ScrapeFromXlsxUrlAsync(url, cancellationToken);
            }
            else
            {
                all = await _scraper.ScrapeAsync(requested, cancellationToken);
            }

            // Apply any global deletions
            var deleted = _cache.Get<HashSet<string>>("solicitors:deleted");
            var listAll = all.ToList();
            if (deleted != null && deleted.Count > 0)
            {
                listAll = listAll.Where(s => !deleted.Contains(MakeUniqueKey(s.Name, s.Location, s.Website))).ToList();
            }

            return listAll;
        });

        return fullList.ToList();
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<SolicitorResult>>> Get([
        FromQuery] string? locations,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sourceName = null,
        [FromQuery] string? sourceUrl = null)
    {
        var currentPage = Math.Max(1, page);
        var currentPageSize = Math.Clamp(pageSize, 1, 500);

        var fullList = await GetFullListAsync(locations, sourceName, sourceUrl, HttpContext.RequestAborted);

        var total = fullList.Count;
        var items = fullList.Skip((currentPage - 1) * currentPageSize).Take(currentPageSize).ToArray();

        return Ok(new PagedResult<SolicitorResult>(items, total, currentPage, currentPageSize, false));
    }

    // Delete a specific solicitor from the in-memory results cache for the supplied locations.
    [HttpGet("insights")]
    public async Task<ActionResult<IEnumerable<SolicitorResult>>> GetInsights(
        [FromQuery] int top = 10,
        [FromQuery] string? locations = null,
        [FromQuery] string? sourceName = null,
        [FromQuery] string? sourceUrl = null)
    {
        var topCount = Math.Clamp(top, 1, 100);
        var fullList = await GetFullListAsync(locations, sourceName, sourceUrl, HttpContext.RequestAborted);

        var insights = fullList.Where(s => s.ReviewCount.HasValue && s.ReviewCount.Value > 0)
            .OrderByDescending(s => s.ReviewCount)
            .ThenBy(s => s.Name)
            .Take(topCount)
            .ToArray();

        return Ok(insights);
    }

    [HttpDelete]
    public ActionResult Delete([FromBody] DeleteSolicitorRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Location)) return BadRequest();
        var unique = MakeUniqueKey(request.Name, request.Location, request.Website);

        // maintain a global deleted set so deletions apply across cached queries
        var deletedKey = "solicitors:deleted";
        var deleted = _cache.Get<HashSet<string>>(deletedKey) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        deleted.Add(unique);
        _cache.Set(deletedKey, deleted, TimeSpan.FromHours(1));

        // Bump cache version so callers will refresh
        _cache.Set("solicitors:version", Guid.NewGuid().ToString());

        return NoContent();
    }

    private static string MakeUniqueKey(string name, string location, string? website)
    {
        return $"{name?.Trim().ToLowerInvariant()}|{location?.Trim().ToLowerInvariant()}|{website?.Trim().ToLowerInvariant() ?? string.Empty}";
    }
}
