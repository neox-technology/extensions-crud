# Neox.Extensions.Crud

Contrats CRUD génériques et implémentation EF Core, avec isolation de schéma optionnelle.

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

`CreateAsync` et `UpdateAsync` lèvent `UniqueConstraintViolationException` quand un index unique est violé. `ConstraintName` porte le nom de l'index ou de la colonne. Une propriété inconnue ou un type incompatible lève `InvalidCrudQueryException`.

## Contribuer

Les questions et les changements passent par les issues et les pull requests du dépôt. Décrire le changement dans la pull request.

## Liens

- [Spécifications](specs/README.md)
- [Sources](src/)
- [Workflows](.github/workflows/)
- [Solution](Neox.Extensions.Crud.slnx)
