namespace Neox.Extensions.Crud.Shared;

public sealed record FilterGroup : FilterExpression
{
    public FilterGroup(FilterGroupKind kind, IReadOnlyList<FilterExpression> operands)
    {
        ArgumentNullException.ThrowIfNull(operands);

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (operands.Count < 2 || operands.Any(operand => operand is null))
        {
            throw new ArgumentException("A filter group requires at least two operands.", nameof(operands));
        }

        Kind = kind;
        Operands = operands.ToArray();
    }

    public FilterGroupKind Kind { get; }

    public IReadOnlyList<FilterExpression> Operands { get; }
}
