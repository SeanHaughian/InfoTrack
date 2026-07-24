using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using InfoTrack.Solicitors.Api.Models;

namespace InfoTrack.Solicitors.Api.Services;

internal static class XlsxLocationExtractor
{
    public static async Task<List<string>> ExtractLocationsFromXlsxUrlAsync(string url, CancellationToken cancellationToken = default)
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

        var sheetEntry = archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
        if (sheetEntry == null) return new List<string>();

        using var sheetStream = sheetEntry.Open();
        var sheetDoc = XDocument.Load(sheetStream);
        XNamespace sx = sheetDoc.Root?.Name.Namespace ?? "";

        var rows = new List<Dictionary<string, string>>();
        foreach (var row in sheetDoc.Descendants(sx + "row"))
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in row.Elements(sx + "c"))
            {
                var r = (string?)c.Attribute("r") ?? string.Empty;
                var col = Regex.Replace(r, "\\d", string.Empty);
                var t = (string?)c.Attribute("t");
                var v = c.Element(sx + "v")?.Value ?? string.Empty;
                string value;
                if (string.Equals(t, "s", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(v, out var idx) && idx >= 0 && idx < shared.Count) value = shared[idx]; else value = string.Empty;
                }
                else value = v;

                dict[col] = value?.Trim() ?? string.Empty;
            }
            if (dict.Count > 0) rows.Add(dict);
        }

        if (rows.Count == 0) return new List<string>();

        var header = rows[0];
        // find location column heuristics
        var locationCol = header.Keys.FirstOrDefault(k => Regex.IsMatch(header[k], "(city|town|location|county|locality|address|postcode)", RegexOptions.IgnoreCase)) ?? header.Keys.First();

        // CLC-specific header names
        var headerValuesCombined = string.Join("|", header.Values).ToLowerInvariant();
        if (headerValuesCombined.Contains("city") && headerValuesCombined.Contains("address_1") && headerValuesCombined.Contains("website_url"))
        {
            locationCol = header.Keys.FirstOrDefault(k => string.Equals(header[k], "City", StringComparison.OrdinalIgnoreCase)) ?? locationCol;
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            if (r.TryGetValue(locationCol, out var loc) && !string.IsNullOrWhiteSpace(loc))
            {
                set.Add(loc.Trim());
            }
        }

        var list = set.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        return list;
    }
}
