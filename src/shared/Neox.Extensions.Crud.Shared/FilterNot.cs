namespace Neox.Extensions.Crud.Shared;

public sealed record FilterNot : FilterExpression
{
    public FilterNot(FilterExpression operand)
    {
        ArgumentNullException.ThrowIfNull(operand);

        Operand = operand;
    }

    public FilterExpression Operand { get; }
}
