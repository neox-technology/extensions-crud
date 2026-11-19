namespace Neox.Extensions.Crud.Shared;

public sealed record FilterTextMatch : FilterExpression
{
    public FilterTextMatch(string property, FilterTextFunction function, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        ArgumentNullException.ThrowIfNull(value);

        if (!Enum.IsDefined(function))
        {
            throw new ArgumentOutOfRangeException(nameof(function));
        }

        Function = function;
        Property = property;
        Value = value;
    }

    public FilterTextFunction Function { get; }

    public string Property { get; }

    public string Value { get; }
}
