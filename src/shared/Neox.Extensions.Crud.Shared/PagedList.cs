namespace Neox.Extensions.Crud.Shared;

public sealed record PagedList<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
