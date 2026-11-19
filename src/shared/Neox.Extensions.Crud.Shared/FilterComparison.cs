namespace Neox.Extensions.Crud.Shared;

public sealed record FilterComparison : FilterExpression
{
    public FilterComparison(string property, FilterOperator @operator, object? value)
    {
        ArgumentNullException.ThrowIfNull(property);

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
