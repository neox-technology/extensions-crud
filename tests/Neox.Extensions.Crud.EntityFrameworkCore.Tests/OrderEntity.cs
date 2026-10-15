using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.EntityFrameworkCore.Tests;

public sealed class OrderEntity : IEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
