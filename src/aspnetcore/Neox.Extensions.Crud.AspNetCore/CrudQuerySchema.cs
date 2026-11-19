namespace Neox.Extensions.Crud.AspNetCore;

public sealed class CrudQuerySchema
{
    public const int DefaultMaxDepth = 8;

    public const int DefaultMaxNodes = 32;

    public int MaxDepth { get; init; } = DefaultMaxDepth;

    public int MaxNodes { get; init; } = DefaultMaxNodes;

    public required IReadOnlyDictionary<string, CrudProperty> Properties { get; init; }
}
