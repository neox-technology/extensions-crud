using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Neox.Extensions.Crud.EntityFrameworkCore.Extensions;
using Neox.Extensions.Crud.EntityFrameworkCore.Services;
using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.EntityFrameworkCore.Tests;

public sealed class EfCrudServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEfCrudService_ResolvesKeyedService()
    {
        var services = new ServiceCollection();

        services.AddDbContext<TestDbContext>(options => options.UseSqlite("Data Source=:memory:"));
        services.AddEfCrudService<OrderEntity, TestDbContext>("legal", db => db.Orders);

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredKeyedService<ICrudService<OrderEntity>>("legal");

        Assert.IsType<EfCrudService<OrderEntity, TestDbContext>>(resolved);
    }
}
