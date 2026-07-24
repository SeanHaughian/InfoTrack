namespace InfoTrack.Solicitors.Api.Models;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int? TotalCount,
    int Page,
    int PageSize,
    bool HasMore);
