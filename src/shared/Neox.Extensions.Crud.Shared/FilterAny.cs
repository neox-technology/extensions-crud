namespace Neox.Extensions.Crud.Shared;

public sealed record FilterAny : FilterExpression
{
    public FilterAny(string property, FilterExpression predicate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        ArgumentNullException.ThrowIfNull(predicate);

        Predicate = predicate;
        Property = property;
    }

    public FilterExpression Predicate { get; }

    public string Property { get; }
}
