using Neox.Extensions.Crud.AspNetCore;

namespace Neox.Extensions.Crud.AspNetCore.Tests;

public sealed class ArticleListQuery : ICrudQueryDefinition<ArticleModel>
{
    public static CrudQuerySchema Schema { get; } = Create();

    public static CrudQuerySchema Create(int maxDepth = CrudQuerySchema.DefaultMaxDepth, int maxNodes = CrudQuerySchema.DefaultMaxNodes)
    {
        return new CrudQuerySchema
        {
            MaxDepth = maxDepth,
            MaxNodes = maxNodes,
            Properties = new Dictionary<string, CrudProperty>(StringComparer.Ordinal)
            {
                ["Archived"] = new("Archived", CrudScalarType.Boolean, canFilter: true, canSort: false),
                ["CategoryId"] = new("CategoryId", CrudScalarType.Guid, canFilter: true, canSort: false),
                ["Hidden"] = new("Secret", CrudScalarType.String, canFilter: false, canSort: false),
                ["PublishedAt"] = new("PublishedAt", CrudScalarType.DateTimeOffset, canFilter: true, canSort: true, nullable: true),
                ["Rank"] = new("Rank", CrudScalarType.Int32, canFilter: true, canSort: false),
                ["Score"] = new("Score", CrudScalarType.Decimal, canFilter: true, canSort: false),
                ["TagIds"] = new(
                    "TagItems",
                    new CrudCollection(new Dictionary<string, CrudProperty>(StringComparer.Ordinal)
                    {
                        ["Id"] = new("Id", CrudScalarType.Guid, canFilter: true, canSort: false),
                    }),
                    canFilter: true),
                ["Tags"] = new("Tags", new CrudCollection(CrudScalarType.String), canFilter: true),
                ["Title"] = new("Slug", CrudScalarType.String, canFilter: true, canSort: true),
            },
        };
    }
}
