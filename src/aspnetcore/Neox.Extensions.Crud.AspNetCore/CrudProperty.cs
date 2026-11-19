namespace Neox.Extensions.Crud.AspNetCore;

public sealed class CrudProperty
{
    public CrudProperty(string entityPath, CrudScalarType type, bool canFilter, bool canSort, bool nullable = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityPath);

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        CanFilter = canFilter;
        CanSort = canSort;
        Collection = null;
        EntityPath = entityPath;
        Nullable = nullable;
        Type = type;
    }

    public CrudProperty(string entityPath, CrudCollection collection, bool canFilter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityPath);
        ArgumentNullException.ThrowIfNull(collection);

        CanFilter = canFilter;
        CanSort = false;
        Collection = collection;
        EntityPath = entityPath;
        Nullable = false;
        Type = null;
    }

    public bool CanFilter { get; }

    public bool CanSort { get; }

    public CrudCollection? Collection { get; }

    public string EntityPath { get; }

    public bool Nullable { get; }

    public CrudScalarType? Type { get; }
}
