# Filtres, tri et conflit d'unicité

`PageRequest` porte `Filters` et `Sorts`. `ListAsync(PageRequest, CancellationToken)` ne change pas de signature.

Les filtres se combinent en ET. Les opérateurs sont `Equal`, `Contains` (élément d'une collection), `LessThan`, `LessThanOrEqual`, `GreaterThan` et `GreaterThanOrEqual`. Le nom de propriété est celui d'une propriété publique directe, sensible à la casse. La valeur est déjà typée.

`EfCrudService` traduit ces critères en LINQ. EF Core les exécute en SQL. Une propriété inconnue, une comparaison de types incompatibles ou un `Contains` sur une propriété qui n'est pas une collection lève `InvalidCrudQueryException`.

Les tests SQLite utilisent `DateTime` pour la date : ce fournisseur ne traduit pas `DateTimeOffset` dans un `ORDER BY`.

Sans tri, l'ordre est `Id` ascendant. Avec tri, `Id` est ajouté comme dernier critère s'il n'est pas déjà demandé. Le tri est appliqué avant `Skip` / `Take`. `TotalCount` compte les entités filtrées.

`CreateAsync` et `UpdateAsync` lèvent `UniqueConstraintViolationException` quand le fournisseur signale un index ou une contrainte unique. `ConstraintName` contient le nom extrait du message (colonne, index ou contrainte), ou `unique` si seul le code d'erreur est disponible. L'écriture est annulée par le fournisseur.
