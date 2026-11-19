using System.Data.Common;

using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Primitives;

using Neox.Extensions.Crud.AspNetCore;
using Neox.Extensions.Crud.EntityFrameworkCore.Services;

namespace Neox.Extensions.Crud.AspNetCore.Tests;

public sealed class ParsedQuerySqlTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestCapture _capture = new();
    private readonly ArticleDbContext _context;
    private readonly EfCrudService<QueryArticle, ArticleDbContext> _service;

    public ParsedQuerySqlTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ArticleDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_capture)
            .Options;

        _context = new ArticleDbContext(options);
        _context.Database.EnsureCreated();
        _service = new EfCrudService<QueryArticle, ArticleDbContext>(_context, context => context.Articles);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ListAsync_ParsedOrContainsAndAny_RunsAsSql()
    {
        _context.Articles.Add(new QueryArticle { Slug = "news-post", Tags = ["dotnet"] });
        _context.Articles.Add(new QueryArticle { Slug = "weekly", Tags = ["news"] });
        _context.Articles.Add(new QueryArticle { Slug = "plain", Tags = ["other"] });
        _context.SaveChanges();

        var parsed = CrudQueryParser.Parse(
            new QueryCollection(new Dictionary<string, StringValues>
            {
                ["$filter"] = "contains(Title,'news') or Tags/any(t: t eq 'news')",
            }),
            ArticleListQuery.Schema);

        _capture.Commands.Clear();

        var listed = await _service.ListAsync(parsed.Page);

        Assert.Equal(2, listed.TotalCount);
        Assert.Equal(["news-post", "weekly"], listed.Items.Select(item => item.Slug).OrderBy(slug => slug));
        Assert.Contains(_capture.Commands, command => command.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TestCapture : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
