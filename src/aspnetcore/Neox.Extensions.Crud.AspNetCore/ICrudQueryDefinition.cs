namespace Neox.Extensions.Crud.AspNetCore;

public interface ICrudQueryDefinition<TModel>
{
    static abstract CrudQuerySchema Schema { get; }
}
