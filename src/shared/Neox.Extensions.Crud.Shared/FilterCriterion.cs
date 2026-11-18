namespace Neox.Extensions.Crud.Shared;

public sealed record FilterCriterion
{
    public FilterCriterion(string property, FilterOperator @operator, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(property);

        if (!Enum.IsDefined(@operator))
        {
            throw new ArgumentOutOfRangeException(nameof(@operator));
        }

        Operator = @operator;
        Property = property;
        Value = value;
    }

    public FilterOperator Operator { get; }

    public string Property { get; }

    public object? Value { get; }
}
