namespace InfoTrack.Solicitors.Api.Models;

public sealed record DeleteSolicitorRequest(
    string Name,
    string Location,
    string? Website,
    string? Locations // comma separated locations used to build cache key
);
