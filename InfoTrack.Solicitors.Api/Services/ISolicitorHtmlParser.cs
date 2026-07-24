using InfoTrack.Solicitors.Api.Models;

namespace InfoTrack.Solicitors.Api.Services
{
    public interface ISolicitorHtmlParser
    {
        IReadOnlyList<SolicitorResult> Parse(
            string html,
            string location);
    }
}