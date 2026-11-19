namespace Neox.Extensions.Crud.AspNetCore;

public sealed class CrudCollection
{
    public CrudCollection(CrudScalarType elementType)
    {
        if (!Enum.IsDefined(elementType))
        {
            throw new ArgumentOutOfRangeException(nameof(elementType));
        }

        ElementType = elementType;
        Properties = new Dictionary<string, CrudProperty>();
    }

    public CrudCollection(IReadOnlyDictionary<string, CrudProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        if (properties.Count == 0)
        {
            throw new ArgumentException("A complex collection requires at least one property.", nameof(properties));
        }

        ElementType = null;
        Properties = properties;
    }

    public CrudScalarType? ElementType { get; }

    public IReadOnlyDictionary<string, CrudProperty> Properties { get; }
}
