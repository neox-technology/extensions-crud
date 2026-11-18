using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.EntityFrameworkCore.Tests;

public sealed class ArticleEntity : IEntity
{
    public Guid CategoryId { get; set; }

    public Guid Id { get; set; }

    public DateTime? PublishedAt { get; set; }

    public string Slug { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];
}
