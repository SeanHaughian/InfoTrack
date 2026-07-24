using System.Linq;
using InfoTrack.Solicitors.Api.Services;
using Xunit;

namespace InfoTrack.Solicitors.Api.Tests;

public class SolicitorHtmlParserTests
{
    [Fact]
    public void Parse_ReturnsResult_WhenBlockContainsFields()
    {
        var html = "...<div class=\"result-item\">" +
                   "<span class=\"h2\">Example Solicitors</span>" +
                   "<address>123 Main St, Townsville</address>" +
                   "<a href=\"tel:+441234567890\">Call</a>" +
                   "<a href=\"https://example.com\">Website</a>" +
                   "(42)" +
                   "</div>...";

        var parser = new SolicitorHtmlParser();
        var results = parser.Parse(html, "Townsville");

        Assert.Single(results);
        var r = results.First();
        Assert.Equal("Example Solicitors", r.Name);
        Assert.Equal("Townsville", r.Location);
        Assert.Equal("123 Main St, Townsville", r.Address);
        Assert.Equal("+441234567890", r.Phone);
        Assert.Equal("https://example.com", r.Website);
        Assert.Equal(42, r.ReviewCount);
    }

    [Fact]
    public void Parse_ReturnsEmpty_WhenNoResultMarker()
    {
        var parser = new SolicitorHtmlParser();
        var results = parser.Parse("<html></html>", "Nowhere");
        Assert.Empty(results);
    }
}
