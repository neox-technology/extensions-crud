using System.Reflection;

using Microsoft.AspNetCore.Http;

using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.AspNetCore;

public sealed class CrudQuery<TModel>
{
    public CrudQuery(PageRequest page, bool count)
    {
        ArgumentNullException.ThrowIfNull(page);

        Count = count;
        Page = page;
    }

    public bool Count { get; }

    public PageRequest Page { get; }

    public static ValueTask<CrudQuery<TModel>?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameter);

        var attribute = parameter.GetCustomAttribute<CrudQueryDefinitionAttribute>()
            ?? throw new InvalidOperationException(
                $"Parameter '{parameter.Name}' requires CrudQueryDefinitionAttribute.");

        var schema = CrudQuerySchemas.Require<TModel>(attribute.DefinitionType);
        var parsed = CrudQueryParser.Parse(context.Request.Query, schema);

        return ValueTask.FromResult<CrudQuery<TModel>?>(new CrudQuery<TModel>(parsed.Page, parsed.Count));
    }
}
