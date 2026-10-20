using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Neox.Extensions.Crud.EntityFrameworkCore.Services;
using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.EntityFrameworkCore.Extensions;

public static class EfCrudServiceCollectionExtensions
{
    public static IServiceCollection AddEfCrudService<TEntity, TContext>(
        this IServiceCollection services,
        Expression<Func<TContext, DbSet<TEntity>>> setSelector)
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(setSelector);

        var compiled = setSelector.Compile();

        services.AddScoped<ICrudService<TEntity>>(provider =>
            new EfCrudService<TEntity, TContext>(
                provider.GetRequiredService<TContext>(),
                compiled));

        return services;
    }

    public static IServiceCollection AddEfCrudService<TEntity, TContext>(
        this IServiceCollection services,
        object serviceKey,
        Expression<Func<TContext, DbSet<TEntity>>> setSelector)
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(setSelector);

        var compiled = setSelector.Compile();

        services.AddKeyedScoped<ICrudService<TEntity>>(serviceKey, (provider, _) =>
            new EfCrudService<TEntity, TContext>(
                provider.GetRequiredService<TContext>(),
                compiled));

        return services;
    }
}
