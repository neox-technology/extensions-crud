using Microsoft.EntityFrameworkCore;

namespace Neox.Extensions.Crud.EntityFrameworkCore.Tests;

public sealed class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    public DbSet<ArticleEntity> Articles => Set<ArticleEntity>();

    public DbSet<NoteEntity> Notes => Set<NoteEntity>();

    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ArticleEntity>()
            .HasIndex(article => article.Slug)
            .IsUnique();

        modelBuilder.Entity<NoteEntity>()
            .OwnsMany(note => note.Tags);
    }
}
