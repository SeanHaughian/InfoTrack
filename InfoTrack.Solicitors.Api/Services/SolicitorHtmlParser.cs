using System.Net;
using System.Text.RegularExpressions;
using InfoTrack.Solicitors.Api.Models;

namespace InfoTrack.Solicitors.Api.Services;

public sealed class SolicitorHtmlParser : ISolicitorHtmlParser
{
    private const string ResultMarker = "<div class=\"result-item";

    public IReadOnlyList<SolicitorResult> Parse(
        string html,
        string location)
    {
        return ExtractBlocks(html)
            .Select(block => ParseResult(block, location))
            .Where(result => result is not null)
            .Cast<SolicitorResult>()
            .ToList();
    }

    private static SolicitorResult? ParseResult(
        string block,
        string location)
    {
        var name = Extract(
            block,
            """<span[^>]*class=["'][^"']*\bh2\b[^"']*["'][^>]*>\s*([^<]+)""");

        if (name is null)
        {
            return null;
        }

        var address = Extract(
            block,
            """<address[^>]*>(.*?)</address>""",
            """<(?:div|p)[^>]*class=["'][^"']*(?:address|addr|location)[^"']*["'][^>]*>(.*?)</(?:div|p)>""");

        var phone = Extract(
            block,
            """href=["']tel:([^"'>]+)["']""",
            """(\+?\d[\d ()-]{6,}\d)""");

        var website = Extract(
            block,
            """<a[^>]+href=["'](https?://[^"']+)["'][^>]*>.*?\bWebsite\b""");

        var reviewText = Extract(
            block,
            """\((\d{1,6})\)""");

        int? reviewCount = int.TryParse(reviewText, out var count)
            ? count
            : null;

        return new SolicitorResult(
            name,
            location,
            address,
            phone,
            website,
            reviewCount);
    }

    private static IEnumerable<string> ExtractBlocks(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<string>();
        }

        var firstResult = html.IndexOf(
            ResultMarker,
            StringComparison.OrdinalIgnoreCase);

        if (firstResult < 0)
        {
            return Array.Empty<string>();
        }

        return html[firstResult..]
            .Split(
                ResultMarker,
                StringSplitOptions.RemoveEmptyEntries)
            .Select(part => ResultMarker + part);
    }

    private static string? Extract(
        string input,
        params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var match = Regex.Match(
                input,
                pattern,
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

            if (match.Success)
            {
                return CleanText(match.Groups[1].Value);
            }
        }

        return null;
    }

    private static string CleanText(string value)
    {
        var withoutTags = Regex.Replace(value, "<[^>]+>", " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);

        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }
}