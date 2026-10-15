using Microsoft.EntityFrameworkCore;

namespace Neox.Extensions.Crud.EntityFrameworkCore.Tests;

public sealed class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
}
