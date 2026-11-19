using System.Reflection;

namespace Neox.Extensions.Crud.AspNetCore;

public static class CrudQuerySchemas
{
    public static CrudQuerySchema Require<TModel>(Type definitionType)
    {
        ArgumentNullException.ThrowIfNull(definitionType);

        if (!definitionType.IsAssignableTo(typeof(ICrudQueryDefinition<TModel>)))
        {
            throw new InvalidOperationException(
                $"Type '{definitionType.Name}' does not implement ICrudQueryDefinition<{typeof(TModel).Name}>.");
        }

        var property = definitionType.GetProperty(
            nameof(ICrudQueryDefinition<TModel>.Schema),
            BindingFlags.Public | BindingFlags.Static);

        if (property?.GetValue(null) is not CrudQuerySchema schema)
        {
            throw new InvalidOperationException($"Type '{definitionType.Name}' does not expose a public static Schema.");
        }

        return schema;
    }
}
