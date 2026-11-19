using System.Data.Common;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Neox.Extensions.Crud.EntityFrameworkCore.Services;
using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.EntityFrameworkCore.Tests;

public sealed class FilterExpressionCrudServiceTests : IDisposable
{
    private readonly EfCrudService<ArticleEntity, TestDbContext> _articles;
    private readonly SqliteConnection _connection;
    private readonly TestDbContext _context;
    private readonly SqlCaptureInterceptor _interceptor = new();
    private readonly EfCrudService<NoteEntity, TestDbContext> _notes;

    public FilterExpressionCrudServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_interceptor)
            .Options;

        _context = new TestDbContext(options);
        _context.Database.EnsureCreated();
        _articles = new EfCrudService<ArticleEntity, TestDbContext>(_context, context => context.Articles);
        _notes = new EfCrudService<NoteEntity, TestDbContext>(_context, context => context.Notes);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ListAsync_BoolEqual_ReturnsMatchingNotes()
    {
        await _notes.CreateAsync(new NoteEntity { Archived = true, Title = "old" });
        await _notes.CreateAsync(new NoteEntity { Archived = false, Title = "draft" });
        await _notes.CreateAsync(new NoteEntity { Title = "unset" });

        var listed = await _notes.ListAsync(new PageRequest
        {
            Filter = new FilterComparison(nameof(NoteEntity.Archived), FilterOperator.Equal, true),
        });

        Assert.Equal("old", Assert.Single(listed.Items).Title);
    }

    [Fact]
    public async Task ListAsync_ComplexAny_ReturnsNotesThatContainTheTag()
    {
        var tagId = Guid.NewGuid();
        await _notes.CreateAsync(new NoteEntity
        {
            Tags = [new NoteTagEntity { Id = tagId }],
            Title = "tagged",
        });
        await _notes.CreateAsync(new NoteEntity
        {
            Tags = [new NoteTagEntity { Id = Guid.NewGuid() }],
            Title = "other",
        });

        var listed = await _notes.ListAsync(new PageRequest
        {
            Filter = new FilterAny(
                nameof(NoteEntity.Tags),
                new FilterComparison(nameof(NoteTagEntity.Id), FilterOperator.Equal, tagId)),
        });

        Assert.Equal("tagged", Assert.Single(listed.Items).Title);
    }

    [Fact]
    public async Task ListAsync_DateTimeEqualAndNull_ReturnsMatchingArticles()
    {
        var publishedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        await _articles.CreateAsync(new ArticleEntity { PublishedAt = publishedAt, Slug = "dated" });
        await _articles.CreateAsync(new ArticleEntity { Slug = "draft" });

        var dated = await _articles.ListAsync(new PageRequest
        {
            Filter = new FilterComparison(nameof(ArticleEntity.PublishedAt), FilterOperator.Equal, publishedAt),
        });
        var drafts = await _articles.ListAsync(new PageRequest
        {
            Filter = new FilterComparison(nameof(ArticleEntity.PublishedAt), FilterOperator.Equal, null),
        });

        Assert.Equal("dated", Assert.Single(dated.Items).Slug);
        Assert.Equal("draft", Assert.Single(drafts.Items).Slug);
    }

    [Fact]
    public async Task ListAsync_GuidEqual_ReturnsMatchingCategory()
    {
        var categoryId = Guid.NewGuid();
        await _articles.CreateAsync(new ArticleEntity { CategoryId = categoryId, Slug = "match" });
        await _articles.CreateAsync(new ArticleEntity { CategoryId = Guid.NewGuid(), Slug = "other" });

        var listed = await _articles.ListAsync(new PageRequest
        {
            Filter = new FilterComparison(nameof(ArticleEntity.CategoryId), FilterOperator.Equal, categoryId),
        });

        Assert.Equal("match", Assert.Single(listed.Items).Slug);
    }

    [Fact]
    public async Task ListAsync_IncompatibleValue_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidCrudQueryException>(() => _articles.ListAsync(new PageRequest
        {
            Filter = new FilterComparison(nameof(ArticleEntity.Slug), FilterOperator.Equal, 1),
        }));

        Assert.Contains(nameof(ArticleEntity.Slug), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAsync_NotEqual_ExcludesTheValue()
    {
        await _articles.CreateAsync(new ArticleEntity { Slug = "keep" });
        await _articles.CreateAsync(new ArticleEntity { Slug = "drop" });

        var listed = await _articles.ListAsync(new PageRequest
        {
            Filter = new FilterComparison(nameof(ArticleEntity.Slug), FilterOperator.NotEqual, "drop"),
        });

        Assert.Equal("keep", Assert.Single(listed.Items).Slug);
    }

    [Fact]
    public async Task ListAsync_OrNotContainsAndAny_TranslatesToSql()
    {
        await _articles.CreateAsync(new ArticleEntity { Slug = "news-post", Tags = ["dotnet"] });
        await _articles.CreateAsync(new ArticleEntity { Slug = "weekly", Tags = ["news"] });
        await _articles.CreateAsync(new ArticleEntity { Slug = "hidden", Tags = ["news"] });
        await _articles.CreateAsync(new ArticleEntity { Slug = "plain", Tags = ["other"] });

        _interceptor.Commands.Clear();

        var listed = await _articles.ListAsync(new PageRequest
        {
            Filter = new FilterGroup(
                FilterGroupKind.And,
                [
                    new FilterGroup(
                        FilterGroupKind.Or,
                        [
                            new FilterTextMatch(nameof(ArticleEntity.Slug), FilterTextFunction.Contains, "news"),
                            new FilterAny(
                                nameof(ArticleEntity.Tags),
                                new FilterComparison(string.Empty, FilterOperator.Equal, "news")),
                        ]),
                    new FilterNot(new FilterComparison(nameof(ArticleEntity.Slug), FilterOperator.Equal, "hidden")),
                ]),
        });

        Assert.Equal(2, listed.TotalCount);
        Assert.Equal(["news-post", "weekly"], listed.Items.Select(item => item.Slug).OrderBy(slug => slug));
        Assert.Contains(
            _interceptor.Commands,
            command => command.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ListAsync_StartsWithAndEndsWith_ReturnsMatches()
    {
        await _articles.CreateAsync(new ArticleEntity { Slug = "news-post" });
        await _articles.CreateAsync(new ArticleEntity { Slug = "weekly-news" });
        await _articles.CreateAsync(new ArticleEntity { Slug = "plain" });

        var starts = await _articles.ListAsync(new PageRequest
        {
            Filter = new FilterTextMatch(nameof(ArticleEntity.Slug), FilterTextFunction.StartsWith, "news"),
        });
        var ends = await _articles.ListAsync(new PageRequest
        {
            Filter = new FilterTextMatch(nameof(ArticleEntity.Slug), FilterTextFunction.EndsWith, "news"),
        });

        Assert.Equal("news-post", Assert.Single(starts.Items).Slug);
        Assert.Equal("weekly-news", Assert.Single(ends.Items).Slug);
    }

    [Fact]
    public async Task ListAsync_TextOnNonString_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidCrudQueryException>(() => _articles.ListAsync(new PageRequest
        {
            Filter = new FilterTextMatch(nameof(ArticleEntity.CategoryId), FilterTextFunction.Contains, "x"),
        }));

        Assert.Contains(nameof(ArticleEntity.CategoryId), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAsync_UnknownProperty_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidCrudQueryException>(() => _articles.ListAsync(new PageRequest
        {
            Filter = new FilterComparison("Missing", FilterOperator.Equal, "x"),
        }));

        Assert.Contains("Missing", exception.Message, StringComparison.Ordinal);
    }

    private sealed class SqlCaptureInterceptor : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Commands.Add(command.CommandText);

            return base.ReaderExecuting(command, eventData, result);
        }

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
