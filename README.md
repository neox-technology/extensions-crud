# Neox.Extensions.Crud

Contrats CRUD génériques, traduction des requêtes OData pour ASP.NET Core, et implémentation EF Core, avec isolation de schéma optionnelle.

## Installation

Prérequis : [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet restore Neox.Extensions.Crud.slnx
dotnet build Neox.Extensions.Crud.slnx
```

## Utilisation

Référencer `Neox.Extensions.Crud.Shared` pour `IEntity` et `ICrudService<TEntity>`. Référencer `Neox.Extensions.Crud.EntityFrameworkCore` pour enregistrer un `DbSet` et, si besoin, isoler un schéma SQL.

```csharp
builder.Services.AddEfCrudService<OrderEntity, AppDbContext>(db => db.Orders);
```

```csharp
modelBuilder.IsolateSchema("orders");
```

`ListAsync` filtre et trie à partir de `PageRequest`. Plusieurs filtres se combinent en ET. Sans tri, l'ordre est `Id` ascendant.

```csharp
var page = new PageRequest
{
    Filters =
    [
        new FilterCriterion(nameof(Article.CategoryId), FilterOperator.Equal, categoryId),
    ],
    Sorts =
    [
        new SortCriterion(nameof(Article.PublishedAt), SortDirection.Descending),
    ],
};

var listed = await articles.ListAsync(page);
```

`PageRequest.Filter` porte un arbre (`and`, `or`, `not`, fonctions de chaîne, `any`). Les critères de `Filters` restent combinés en ET avec cet arbre. `EfCrudService` traduit le tout en SQL.

Référencer `Neox.Extensions.Crud.AspNetCore` pour lier `$filter`, `$orderby`, `$top`, `$skip` et `$count` dans une minimal API. Le contrat HTTP n'est pas l'entité : le schéma déclare le nom exposé, le chemin sur l'entité, et les propriétés filtrables ou triables. Les littéraux suivent le parseur OData 9 : chaîne entre quotes, Guid nu, entier, décimal suffixé `M`, date ISO-8601, `true`, `false`, `null`.

```csharp
public sealed record ArticleModel(string Title, DateTimeOffset? PublishedAt);

public sealed class ArticleListQuery : ICrudQueryDefinition<ArticleModel>
{
    public static CrudQuerySchema Schema { get; } = new()
    {
        Properties = new Dictionary<string, CrudProperty>
        {
            ["Title"] = new("Slug", CrudScalarType.String, canFilter: true, canSort: true),
            ["PublishedAt"] = new("PublishedAt", CrudScalarType.DateTimeOffset, canFilter: true, canSort: true, nullable: true),
        },
    };
}

builder.Services.AddCrudQueryHandling();
app.UseExceptionHandler();

app.MapGet("/articles", async (
    [CrudQueryDefinition(typeof(ArticleListQuery))] CrudQuery<ArticleModel> query,
    ICrudService<ArticleEntity> articles,
    CancellationToken cancellationToken) =>
    Results.Ok(await articles.ListAsync(query.Page, cancellationToken)));
```

`GET /articles?$filter=Title eq 'x'&$orderby=PublishedAt desc,Title asc&$top=20&$skip=0&$count=true` appelle `ListAsync` avec l'égalité sur `Slug`, le tri, la page 1 et `TotalCount`. Sans `$top`, la taille est `PageRequest.DefaultPageSize`. `$top` est borné à `PageRequest.MaxPageSize`. `$skip` doit être un multiple de cette taille. `$expand`, `$select` et `$apply` répondent 400, comme une propriété hors liste ou une expression trop profonde. `AddCrudQueryHandling` écrit alors un `ProblemDetails`.

`CreateAsync` et `UpdateAsync` lèvent `UniqueConstraintViolationException` quand un index unique est violé. `ConstraintName` porte le nom de l'index ou de la colonne. Une propriété inconnue ou un type incompatible lève `InvalidCrudQueryException`.

## Contribuer

Les questions et les changements passent par les issues et les pull requests du dépôt. Décrire le changement dans la pull request.

## Liens

- [Spécifications](specs/README.md)
- [Sources](src/)
- [Workflows](.github/workflows/)
- [Solution](Neox.Extensions.Crud.slnx)
