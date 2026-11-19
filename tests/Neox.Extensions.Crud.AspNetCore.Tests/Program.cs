using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Neox.Extensions.Crud.AspNetCore;
using Neox.Extensions.Crud.EntityFrameworkCore.Extensions;
using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.AspNetCore.Tests;

public sealed class Program
{
    public static void Main(string[] args)
    {
        Create(args).Run();
    }

    public static WebApplication Create(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Services.AddCrudQueryHandling();
        builder.Services.AddDbContext<ArticleDbContext>(options => options.UseSqlite(ArticleDatabase.Connection));
        builder.Services.AddEfCrudService<QueryArticle, ArticleDbContext>(db => db.Articles);

        var app = builder.Build();

        Seed(app.Services);
        app.UseExceptionHandler();
        app.MapGet("/articles", ListAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(
        [CrudQueryDefinition(typeof(ArticleListQuery))] CrudQuery<ArticleModel> query,
        ICrudService<QueryArticle> articles,
        CancellationToken cancellationToken)
    {
        var listed = await articles.ListAsync(query.Page, cancellationToken);

        return Results.Ok(listed);
    }

    private static void Seed(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleDbContext>();

        db.Database.EnsureCreated();

        if (db.Articles.Any())
        {
            return;
        }

        db.Articles.Add(new QueryArticle
        {
            PublishedAt = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),
            Slug = "x",
        });
        db.Articles.Add(new QueryArticle
        {
            PublishedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Slug = "y",
        });
        db.SaveChanges();
    }
}

internal static class ArticleDatabase
{
    public static SqliteConnection Connection { get; } = Open();

    private static SqliteConnection Open()
    {
        var connection = new SqliteConnection("Data Source=:memory:");

        connection.Open();

        return connection;
    }
}
