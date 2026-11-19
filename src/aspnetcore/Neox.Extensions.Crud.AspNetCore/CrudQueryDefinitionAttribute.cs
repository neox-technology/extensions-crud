namespace Neox.Extensions.Crud.AspNetCore;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class CrudQueryDefinitionAttribute : Attribute
{
    public CrudQueryDefinitionAttribute(Type definitionType)
    {
        ArgumentNullException.ThrowIfNull(definitionType);

        DefinitionType = definitionType;
    }

    public Type DefinitionType { get; }
}
