using Microsoft.EntityFrameworkCore;

namespace Neox.Extensions.Crud.EntityFrameworkCore.Tests;

public sealed class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    public DbSet<ArticleEntity> Articles => Set<ArticleEntity>();

    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ArticleEntity>()
            .HasIndex(article => article.Slug)
            .IsUnique();
    }
}
