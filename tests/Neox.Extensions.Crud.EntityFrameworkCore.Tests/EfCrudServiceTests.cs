using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Neox.Extensions.Crud.EntityFrameworkCore.Services;
using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.EntityFrameworkCore.Tests;

public sealed class EfCrudServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContext _context;
    private readonly EfCrudService<OrderEntity, TestDbContext> _service;

    public EfCrudServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new TestDbContext(options);
        _context.Database.EnsureCreated();
        _service = new EfCrudService<OrderEntity, TestDbContext>(_context, context => context.Orders);
    }

    [Fact]
    public async Task CreateAsync_AssignsIdWhenEmpty()
    {
        var created = await _service.CreateAsync(new OrderEntity { Name = "soup" });

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("soup", created.Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntity()
    {
        var created = await _service.CreateAsync(new OrderEntity { Name = "soup" });

        await _service.DeleteAsync(created.Id);

        Assert.Null(await _service.RetrieveAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenMissing()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteAsync(Guid.NewGuid()));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ListAsync_ReturnsPage()
    {
        await _service.CreateAsync(new OrderEntity { Name = "soup" });
        await _service.CreateAsync(new OrderEntity { Name = "bread" });

        var listed = await _service.ListAsync(new PageRequest());

        Assert.Equal(2, listed.TotalCount);
        Assert.Equal(2, listed.Items.Count);
        Assert.Equal(1, listed.Page);
        Assert.Equal(PageRequest.DefaultPageSize, listed.PageSize);
    }

    [Fact]
    public async Task ListAsync_SecondPageReturnsRemainder()
    {
        var first = await _service.CreateAsync(new OrderEntity { Name = "soup" });
        var second = await _service.CreateAsync(new OrderEntity { Name = "bread" });
        var ordered = new[] { first, second }.OrderBy(entity => entity.Id).ToArray();

        var listed = await _service.ListAsync(new PageRequest(2, 1));

        Assert.Equal(2, listed.TotalCount);
        var item = Assert.Single(listed.Items);
        Assert.Equal(ordered[1].Id, item.Id);
    }

    [Fact]
    public void PageRequest_ThrowsWhenPageIsBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PageRequest(0, 1));
    }

    [Fact]
    public void PageRequest_ThrowsWhenPageSizeExceedsMax()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PageRequest(1, PageRequest.MaxPageSize + 1));
    }

    [Fact]
    public async Task RetrieveAsync_ReturnsEntity()
    {
        var created = await _service.CreateAsync(new OrderEntity { Name = "soup" });

        var retrieved = await _service.RetrieveAsync(created.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved.Id);
    }

    [Fact]
    public async Task RetrieveAsync_ReturnsNullWhenMissing()
    {
        var retrieved = await _service.RetrieveAsync(Guid.NewGuid());

        Assert.Null(retrieved);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var created = await _service.CreateAsync(new OrderEntity { Name = "soup" });

        created.Name = "stew";
        await _service.UpdateAsync(created);

        var retrieved = await _service.RetrieveAsync(created.Id);

        Assert.NotNull(retrieved);
        Assert.Equal("stew", retrieved.Name);
    }
}
