using Microsoft.EntityFrameworkCore;

namespace Neox.Extensions.Crud.AspNetCore.Tests;

public sealed class ArticleDbContext : DbContext
{
    public ArticleDbContext(DbContextOptions<ArticleDbContext> options)
        : base(options)
    {
    }

    public DbSet<QueryArticle> Articles => Set<QueryArticle>();
}
