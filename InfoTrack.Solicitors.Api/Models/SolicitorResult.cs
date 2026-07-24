namespace InfoTrack.Solicitors.Api.Models;

public sealed record SolicitorResult(
    string Name,
    string Location,
    string? Address,
    string? Phone,
    string? Website,
    int? ReviewCount);