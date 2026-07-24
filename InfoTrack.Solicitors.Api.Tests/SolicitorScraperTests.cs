using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InfoTrack.Solicitors.Api.Services;
using InfoTrack.Solicitors.Api.Models;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoTrack.Solicitors.Api.Tests;

public class SolicitorScraperTests
{
    [Fact]
    public async Task ScrapeAsync_DeduplicatesByOffice()
    {
        var locations = new[] { "Townsville", "Other" };

        // Use a parser that returns duplicate office entries (same Name+Location+Address)
        var fakeParser = new DuplicateResultParser("Office One", "Townsville", "1 Road, Townsville");

        var html = "<html></html>"; // content isn't used by the fake parser
        var responses = new Dictionary<string, string>
        {
            { BuildUrlFor("Townsville"), html },
            { BuildUrlFor("Other"), html }
        };

        using var httpClient = new HttpClient(new FakeHandler(responses));
        var scraper = new SolicitorScraper(httpClient, fakeParser, new NullLogger<SolicitorScraper>());

        var results = (await scraper.ScrapeAsync(locations)).ToList();

        Assert.Single(results);
        var r = results[0];
        Assert.Equal("Office One", r.Name);
        Assert.Equal("1 Road, Townsville", r.Address);
    }

    [Fact]
    public async Task ScrapeAsync_ContinuesWhenHttpFails()
    {
        var locations = new[] { "Bad", "Good" };

        var goodHtml = "...<div class=\"result-item\">" +
                       "<span class=\"h2\">Good Office</span>" +
                       "<address>9 Example Rd</address>" +
                       "</div>";

        var responses = new Dictionary<string, string>
        {
            { BuildUrlFor("Good"), goodHtml }
        };

        using var httpClient = new HttpClient(new ThrowingHandler(BuildUrlFor("Bad"), responses));
        var scraper = new SolicitorScraper(httpClient, new SolicitorHtmlParser(), new NullLogger<SolicitorScraper>());

        var results = (await scraper.ScrapeAsync(locations)).ToList();

        Assert.Single(results);
        Assert.Equal("Good Office", results[0].Name);
    }

    class DuplicateResultParser : ISolicitorHtmlParser
    {
        private readonly SolicitorResult[] _results;

        public DuplicateResultParser(string name, string location, string address)
        {
            _results = new[]
            {
                new SolicitorResult(name, location, address, null, null, null),
                new SolicitorResult(name, location, address, null, null, null)
            };
        }

        public IReadOnlyList<SolicitorResult> Parse(string html, string location)
        {
            // Return duplicates regardless of input
            return _results;
        }
    }

    static string BuildUrlFor(string location)
    {
        var slug = location.ToLowerInvariant().Replace(" ", "-");
        return $"https://www.solicitors.com/{Uri.EscapeDataString(slug)}-solicitors.html";
    }

    class FakeHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _responses;

        public FakeHandler(Dictionary<string, string> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.TryGetValue(request.RequestUri.ToString(), out var body))
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "text/html")
                };
                return Task.FromResult(resp);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    class ThrowingHandler : HttpMessageHandler
    {
        private readonly string _throwFor;
        private readonly Dictionary<string, string> _responses;

        public ThrowingHandler(string throwFor, Dictionary<string, string> responses)
        {
            _throwFor = throwFor;
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (string.Equals(request.RequestUri.ToString(), _throwFor, StringComparison.OrdinalIgnoreCase))
            {
                throw new HttpRequestException("Simulated failure");
            }

            if (_responses.TryGetValue(request.RequestUri.ToString(), out var body))
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "text/html")
                };
                return Task.FromResult(resp);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    // Minimal no-op logger for constructor parameter
    class NullLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }

        private class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
