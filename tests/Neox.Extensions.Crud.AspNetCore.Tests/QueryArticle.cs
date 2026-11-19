using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.AspNetCore.Tests;

public sealed class QueryArticle : IEntity
{
    public Guid Id { get; set; }

    public DateTime? PublishedAt { get; set; }

    public string Slug { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];
}
