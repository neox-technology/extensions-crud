using Microsoft.Extensions.DependencyInjection;

namespace Neox.Extensions.Crud.AspNetCore;

public static class CrudQueryServiceCollectionExtensions
{
    public static IServiceCollection AddCrudQueryHandling(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails();
        services.AddExceptionHandler<InvalidCrudQueryExceptionHandler>();

        return services;
    }
}
