namespace InfoTrack.Solicitors.Api.Models;

public sealed record SavedSearchResult(
    int Id,
    string Name,
    DateTime Timestamp,
    string[] Locations,
    SolicitorResult[] Results);
