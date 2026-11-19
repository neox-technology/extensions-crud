using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Neox.Extensions.Crud.EntityFrameworkCore.Services;
using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.EntityFrameworkCore.Tests;

public sealed class ArticleCrudServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContext _context;
    private readonly DbContextOptions<TestDbContext> _options;
    private readonly EfCrudService<ArticleEntity, TestDbContext> _service;

    public ArticleCrudServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new TestDbContext(_options);
        _context.Database.EnsureCreated();
        _service = new EfCrudService<ArticleEntity, TestDbContext>(_context, context => context.Articles);
    }

    [Fact]
    public async Task CreateAsync_DuplicateSlug_ThrowsAndKeepsSingleRow()
    {
        await _service.CreateAsync(new ArticleEntity { Slug = "post" });

        var exception = await Assert.ThrowsAsync<UniqueConstraintViolationException>(
            () => _service.CreateAsync(new ArticleEntity { Slug = "post" }));

        Assert.Contains("Slug", exception.ConstraintName, StringComparison.Ordinal);

        await using var other = OpenContext();

        Assert.Equal(1, await other.Articles.CountAsync());
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ListAsync_ContainsFilter_ReturnsEntitiesThatIncludeTheValue()
    {
        await _service.CreateAsync(new ArticleEntity { Slug = "news-post", Tags = ["news", "dotnet"] });
        await _service.CreateAsync(new ArticleEntity { Slug = "other", Tags = ["other"] });

        var listed = await _service.ListAsync(new PageRequest
        {
            Filters = [new FilterCriterion(nameof(ArticleEntity.Tags), FilterOperator.Contains, "news")],
        });

        Assert.Equal(1, listed.TotalCount);
        var item = Assert.Single(listed.Items);
        Assert.Equal("news-post", item.Slug);
    }

    [Fact]
    public async Task ListAsync_EqualFilter_ReturnsMatchesAndFilteredCount()
    {
        var categoryId = Guid.NewGuid();
        await _service.CreateAsync(new ArticleEntity { CategoryId = categoryId, Slug = "one" });
        await _service.CreateAsync(new ArticleEntity { CategoryId = Guid.NewGuid(), Slug = "two" });
        await _service.CreateAsync(new ArticleEntity { CategoryId = categoryId, Slug = "three" });

        var listed = await _service.ListAsync(new PageRequest(1, 1)
        {
            Filters = [new FilterCriterion(nameof(ArticleEntity.CategoryId), FilterOperator.Equal, categoryId)],
        });

        Assert.Equal(2, listed.TotalCount);
        var item = Assert.Single(listed.Items);
        Assert.Equal(categoryId, item.CategoryId);
    }

    [Fact]
    public async Task ListAsync_IncompatibleValue_Throws()
    {
        await Assert.ThrowsAsync<InvalidCrudQueryException>(() => _service.ListAsync(new PageRequest
        {
            Filters = [new FilterCriterion(nameof(ArticleEntity.Slug), FilterOperator.Equal, 1)],
        }));
    }

    [Fact]
    public async Task ListAsync_LessThanOrEqualFilter_ExcludesLaterDates()
    {
        var cutoff = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        await _service.CreateAsync(new ArticleEntity { PublishedAt = cutoff.AddDays(-1), Slug = "old" });
        await _service.CreateAsync(new ArticleEntity { PublishedAt = cutoff, Slug = "edge" });
        await _service.CreateAsync(new ArticleEntity { PublishedAt = cutoff.AddDays(1), Slug = "later" });

        var listed = await _service.ListAsync(new PageRequest
        {
            Filters = [new FilterCriterion(nameof(ArticleEntity.PublishedAt), FilterOperator.LessThanOrEqual, cutoff)],
        });

        Assert.Equal(2, listed.TotalCount);
        Assert.DoesNotContain(listed.Items, item => item.Slug == "later");
    }

    [Fact]
    public async Task ListAsync_SortDescending_AppliesOrderBeforePaging()
    {
        var early = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await _service.CreateAsync(new ArticleEntity { PublishedAt = early, Slug = "early" });
        await _service.CreateAsync(new ArticleEntity { PublishedAt = early.AddDays(1), Slug = "middle" });
        await _service.CreateAsync(new ArticleEntity { PublishedAt = early.AddDays(2), Slug = "late" });

        var sorts = new[] { new SortCriterion(nameof(ArticleEntity.PublishedAt), SortDirection.Descending) };

        var first = await _service.ListAsync(new PageRequest(1, 1) { Sorts = sorts });
        var second = await _service.ListAsync(new PageRequest(2, 1) { Sorts = sorts });

        Assert.Equal(3, first.TotalCount);
        Assert.Equal("late", Assert.Single(first.Items).Slug);
        Assert.Equal("middle", Assert.Single(second.Items).Slug);
    }

    [Fact]
    public async Task ListAsync_UnknownProperty_Throws()
    {
        await Assert.ThrowsAsync<InvalidCrudQueryException>(() => _service.ListAsync(new PageRequest
        {
            Filters = [new FilterCriterion("Missing", FilterOperator.Equal, "x")],
        }));
    }

    [Fact]
    public async Task ListAsync_WithoutSort_IsStable()
    {
        await _service.CreateAsync(new ArticleEntity { Slug = "one" });
        await _service.CreateAsync(new ArticleEntity { Slug = "two" });

        var first = await _service.ListAsync(new PageRequest());
        var second = await _service.ListAsync(new PageRequest());

        Assert.Equal(first.Items.Select(item => item.Id), second.Items.Select(item => item.Id));
        Assert.Equal(first.Items.OrderBy(item => item.Id).Select(item => item.Id), first.Items.Select(item => item.Id));
    }

    private TestDbContext OpenContext()
    {
        return new TestDbContext(_options);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateSlug_ThrowsAndKeepsStoredSlug()
    {
        await _service.CreateAsync(new ArticleEntity { Slug = "taken" });
        var second = await _service.CreateAsync(new ArticleEntity { Slug = "free" });

        second.Slug = "taken";

        var exception = await Assert.ThrowsAsync<UniqueConstraintViolationException>(
            () => _service.UpdateAsync(second));

        Assert.Contains("Slug", exception.ConstraintName, StringComparison.Ordinal);

        await using var other = OpenContext();
        var stored = await other.Articles.SingleAsync(article => article.Id == second.Id);

        Assert.Equal("free", stored.Slug);
    }
}
