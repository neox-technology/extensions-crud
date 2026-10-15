using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Neox.Extensions.Crud.EntityFrameworkCore.Services;

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
    public async Task ListAsync_ReturnsAll()
    {
        await _service.CreateAsync(new OrderEntity { Name = "soup" });
        await _service.CreateAsync(new OrderEntity { Name = "bread" });

        var listed = await _service.ListAsync();

        Assert.Equal(2, listed.Count);
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
