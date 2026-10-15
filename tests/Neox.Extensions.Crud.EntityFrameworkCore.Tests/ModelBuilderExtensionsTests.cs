using Microsoft.EntityFrameworkCore;
using Neox.Extensions.Crud.EntityFrameworkCore.Extensions;

namespace Neox.Extensions.Crud.EntityFrameworkCore.Tests;

public sealed class ModelBuilderExtensionsTests
{
    [Fact]
    public void IsolateSchema_SetsDefaultSchema()
    {
        var modelBuilder = new ModelBuilder();

        modelBuilder.IsolateSchema("orders");

        Assert.Equal("orders", modelBuilder.Model.GetDefaultSchema());
    }
}
