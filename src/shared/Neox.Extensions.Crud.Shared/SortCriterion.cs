namespace Neox.Extensions.Crud.Shared;

public sealed record SortCriterion
{
    public SortCriterion(string property, SortDirection direction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(property);

        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        Direction = direction;
        Property = property;
    }

    public SortDirection Direction { get; }

    public string Property { get; }
}
