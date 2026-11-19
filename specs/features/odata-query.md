# Requête OData vers ICrudService

`Neox.Extensions.Crud.AspNetCore` traduit `$filter`, `$orderby`, `$top`, `$skip` et `$count` vers `PageRequest`. Le parseur est `Microsoft.OData.Core`. Il n’y a pas de modèle EDM publié ni de contrôleur OData.

Le schéma de l’endpoint nomme les propriétés du contrat, leur chemin sur l’entité, et celles qui peuvent être filtrées ou triées. `CrudQuery<TModel>` se lie dans une minimal API avec `CrudQueryDefinitionAttribute`. `TModel` n’est pas l’entité.

`$filter` accepte `eq`, `ne`, `gt`, `ge`, `lt`, `le`, `and`, `or`, `not`, les parenthèses, `contains`, `startswith`, `endswith`, et `any` sur une collection primitive ou complexe. L’arbre est `PageRequest.Filter`. Les littéraux Guid, entier, décimal, `DateTimeOffset`, booléen et `null` gardent leur type CLR. Les autres options `$` répondent 400.

`$top` absent vaut `PageRequest.DefaultPageSize`. Au-delà de `PageRequest.MaxPageSize`, il est borné. `$skip` doit être un multiple de cette taille, sinon 400. `ListAsync` remplit `TotalCount` pour `$count=true` comme pour `$count=false`.

La profondeur et le nombre de nœuds sont limités par `CrudQuerySchema` (`DefaultMaxDepth` 8, `DefaultMaxNodes` 32). Au-delà, la réponse est un `ProblemDetails` 400 dont le détail nomme l’erreur. `EfCrudService` traduit l’arbre en SQL.
