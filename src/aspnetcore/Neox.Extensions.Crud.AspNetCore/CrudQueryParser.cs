using System.Globalization;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;

using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.AspNetCore;

public static class CrudQueryParser
{
    private static readonly HashSet<string> SupportedOptions = new(StringComparer.Ordinal)
    {
        "$count",
        "$filter",
        "$orderby",
        "$skip",
        "$top",
    };

    public static CrudQueryResult Parse(IQueryCollection query, CrudQuerySchema schema)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(schema);

        if (schema.Properties is null)
        {
            throw new InvalidOperationException($"{nameof(schema.Properties)} is required.");
        }

        if (schema.MaxDepth < 1)
        {
            throw new InvalidOperationException($"{nameof(schema.MaxDepth)} must be at least 1.");
        }

        if (schema.MaxNodes < 1)
        {
            throw new InvalidOperationException($"{nameof(schema.MaxNodes)} must be at least 1.");
        }

        foreach (var key in query.Keys)
        {
            if (key.StartsWith('$') && !SupportedOptions.Contains(key))
            {
                throw new InvalidCrudQueryException($"Query option '{key}' is not supported.");
            }

            if (query[key].Count > 1)
            {
                throw new InvalidCrudQueryException($"Query option '{key}' is specified more than once.");
            }
        }

        var count = ReadCount(query);
        var pageSize = ReadPageSize(query);
        var skip = ReadSkip(query);

        if (skip % pageSize != 0)
        {
            throw new InvalidCrudQueryException($"$skip must be a multiple of the page size ({pageSize}).");
        }

        var visitor = new Visitor(schema);
        var filter = visitor.ReadFilter(ReadOptional(query, "$filter"));
        var sorts = visitor.ReadOrderBy(ReadOptional(query, "$orderby"));
        var page = new PageRequest(skip / pageSize + 1, pageSize)
        {
            Filter = filter,
            Sorts = sorts,
        };

        return new CrudQueryResult(page, count);
    }

    private static bool ReadCount(IQueryCollection query)
    {
        var value = ReadOptional(query, "$count");

        if (value is null)
        {
            return false;
        }

        if (value == "true")
        {
            return true;
        }

        if (value == "false")
        {
            return false;
        }

        throw new InvalidCrudQueryException("$count must be true or false.");
    }

    private static string? ReadOptional(IQueryCollection query, string key)
    {
        if (!query.TryGetValue(key, out StringValues values) || values.Count == 0)
        {
            return null;
        }

        var text = values[0];

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidCrudQueryException($"Query option '{key}' is empty.");
        }

        return text;
    }

    private static int ReadPageSize(IQueryCollection query)
    {
        var text = ReadOptional(query, "$top");

        if (text is null)
        {
            return PageRequest.DefaultPageSize;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var top) || top < 1)
        {
            throw new InvalidCrudQueryException("$top must be an integer greater than or equal to 1.");
        }

        return Math.Min(top, PageRequest.MaxPageSize);
    }

    private static int ReadSkip(IQueryCollection query)
    {
        var text = ReadOptional(query, "$skip");

        if (text is null)
        {
            return 0;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var skip) || skip < 0)
        {
            throw new InvalidCrudQueryException("$skip must be an integer greater than or equal to 0.");
        }

        return skip;
    }

    private sealed class Visitor
    {
        private readonly CrudQuerySchema _schema;
        private int _nodes;

        public Visitor(CrudQuerySchema schema)
        {
            _schema = schema;
        }

        public FilterExpression? ReadFilter(string? filter)
        {
            if (filter is null)
            {
                return null;
            }

            var model = CreateModel(out var entityType, out var entitySet);
            FilterClause clause;

            try
            {
                var parser = new ODataQueryOptionParser(
                    model,
                    entityType,
                    entitySet,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["$filter"] = filter });
                clause = parser.ParseFilter();
            }
            catch (ODataException exception)
            {
                throw new InvalidCrudQueryException($"$filter '{filter}' is invalid: {exception.Message}", exception);
            }

            return VisitFilter(clause.Expression, RootScope(), depth: 1);
        }

        public IReadOnlyList<SortCriterion> ReadOrderBy(string? orderBy)
        {
            if (orderBy is null)
            {
                return [];
            }

            var model = CreateModel(out var entityType, out var entitySet);
            OrderByClause clause;

            try
            {
                var parser = new ODataQueryOptionParser(
                    model,
                    entityType,
                    entitySet,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["$orderby"] = orderBy });
                clause = parser.ParseOrderBy();
            }
            catch (ODataException exception)
            {
                throw new InvalidCrudQueryException($"$orderby '{orderBy}' is invalid: {exception.Message}", exception);
            }

            var sorts = new List<SortCriterion>();
            var current = clause;

            while (current is not null)
            {
                var property = VisitSort(current.Expression, RootScope(), depth: 1);
                var direction = current.Direction == OrderByDirection.Descending
                    ? SortDirection.Descending
                    : SortDirection.Ascending;

                sorts.Add(new SortCriterion(property, direction));
                current = current.ThenBy;
            }

            return sorts;
        }

        private void Count(int depth)
        {
            _nodes++;

            if (_nodes > _schema.MaxNodes)
            {
                throw new InvalidCrudQueryException($"The query exceeds the maximum number of nodes ({_schema.MaxNodes}).");
            }

            if (depth > _schema.MaxDepth)
            {
                throw new InvalidCrudQueryException($"The query exceeds the maximum depth ({_schema.MaxDepth}).");
            }
        }

        private IEdmModel CreateModel(out IEdmEntityType entityType, out IEdmNavigationSource entitySet)
        {
            var model = new EdmModel();
            var entity = new EdmEntityType("Neox.Extensions.Crud", "Resource");

            foreach (var pair in _schema.Properties)
            {
                AddProperty(model, entity, pair.Key, pair.Value);
            }

            var key = entity.AddStructuralProperty("__key", EdmPrimitiveTypeKind.Guid, isNullable: false);
            entity.AddKeys(key);
            model.AddElement(entity);

            var container = new EdmEntityContainer("Neox.Extensions.Crud", "Container");
            var set = container.AddEntitySet("Resources", entity);
            model.AddElement(container);

            entityType = entity;
            entitySet = set;

            return model;
        }

        private static void AddProperty(EdmModel model, EdmStructuredType type, string name, CrudProperty property)
        {
            if (property.Collection is null)
            {
                type.AddStructuralProperty(name, ToKind(property.Type!.Value), property.Nullable);

                return;
            }

            IEdmTypeReference elementType;

            if (property.Collection.ElementType is CrudScalarType scalar)
            {
                elementType = EdmCoreModel.Instance.GetPrimitive(ToKind(scalar), isNullable: false);
            }
            else
            {
                var complex = new EdmComplexType("Neox.Extensions.Crud", name + "Item");

                foreach (var child in property.Collection.Properties)
                {
                    AddProperty(model, complex, child.Key, child.Value);
                }

                model.AddElement(complex);
                elementType = new EdmComplexTypeReference(complex, isNullable: false);
            }

            var collection = new EdmCollectionTypeReference(new EdmCollectionType(elementType));
            type.AddStructuralProperty(name, collection);
        }

        private Scope RootScope()
        {
            return new Scope(_schema.Properties, null, null);
        }

        private static EdmPrimitiveTypeKind ToKind(CrudScalarType type)
        {
            return type switch
            {
                CrudScalarType.Boolean => EdmPrimitiveTypeKind.Boolean,
                CrudScalarType.DateTimeOffset => EdmPrimitiveTypeKind.DateTimeOffset,
                CrudScalarType.Decimal => EdmPrimitiveTypeKind.Decimal,
                CrudScalarType.Guid => EdmPrimitiveTypeKind.Guid,
                CrudScalarType.Int32 => EdmPrimitiveTypeKind.Int32,
                CrudScalarType.String => EdmPrimitiveTypeKind.String,
                _ => throw new InvalidOperationException($"Scalar type '{type}' is not supported."),
            };
        }

        private FilterExpression VisitAny(AnyNode any, Scope scope, int depth)
        {
            var property = VisitCollection(any.Source, scope, depth + 1);
            var collection = property.Collection!;
            Scope child;

            if (collection.ElementType is CrudScalarType elementType)
            {
                child = new Scope(
                    new Dictionary<string, CrudProperty>(),
                    elementType,
                    CollectionContractName(any.Source));
            }
            else
            {
                child = new Scope(collection.Properties, null, null);
            }

            var body = VisitFilter(any.Body, child, depth + 1);

            return new FilterAny(property.EntityPath, body);
        }

        private FilterExpression VisitComparison(BinaryOperatorNode binary, Scope scope, int depth)
        {
            var left = VisitOperand(binary.Left, scope, depth + 1);
            var right = VisitOperand(binary.Right, scope, depth + 1);
            var swapped = Swap(binary.OperatorKind, left, right);

            if (swapped.Left is not PropertyOperand and not ElementOperand || swapped.Right is not ConstantOperand constant)
            {
                throw new InvalidCrudQueryException($"Operator '{binary.OperatorKind}' is not supported.");
            }

            var value = ScalarValue(constant, swapped.Left);

            if (swapped.Left is ElementOperand)
            {
                return new FilterComparison(string.Empty, swapped.Operator, value);
            }

            var property = (PropertyOperand)swapped.Left;

            return new FilterComparison(property.EntityPath, swapped.Operator, value);
        }

        private CrudProperty VisitCollection(QueryNode node, Scope scope, int depth)
        {
            Count(depth);

            if (node is ConvertNode convert)
            {
                return VisitCollection(convert.Source, scope, depth + 1);
            }

            var (source, name) = node switch
            {
                CollectionPropertyAccessNode access => (access.Source, access.Property.Name),
                CollectionComplexNode complex => (complex.Source, complex.Property.Name),
                _ => ((QueryNode?)null, (string?)null),
            };

            if (name is null)
            {
                throw new InvalidCrudQueryException("The collection expression is not supported.");
            }

            EnsureRange(source);

            var property = RequireProperty(scope, name, "$filter");

            if (property.Collection is null)
            {
                throw new InvalidCrudQueryException($"Property '{name}' is not a collection.");
            }

            return property;
        }

        private FilterExpression VisitFilter(QueryNode node, Scope scope, int depth)
        {
            Count(depth);

            if (node is ConvertNode convert)
            {
                return VisitFilter(convert.Source, scope, depth + 1);
            }

            if (node is AnyNode any)
            {
                return VisitAny(any, scope, depth);
            }

            if (node is UnaryOperatorNode unary)
            {
                if (unary.OperatorKind != UnaryOperatorKind.Not)
                {
                    throw new InvalidCrudQueryException($"Operator '{unary.OperatorKind}' is not supported.");
                }

                return new FilterNot(VisitFilter(unary.Operand, scope, depth + 1));
            }

            if (node is SingleValueFunctionCallNode call)
            {
                return VisitFunction(call, scope, depth);
            }

            if (node is BinaryOperatorNode binary)
            {
                if (binary.OperatorKind is BinaryOperatorKind.And or BinaryOperatorKind.Or)
                {
                    var kind = binary.OperatorKind == BinaryOperatorKind.And ? FilterGroupKind.And : FilterGroupKind.Or;
                    var left = VisitFilter(binary.Left, scope, depth + 1);
                    var right = VisitFilter(binary.Right, scope, depth + 1);

                    return new FilterGroup(kind, [left, right]);
                }

                return VisitComparison(binary, scope, depth);
            }

            throw new InvalidCrudQueryException($"Expression '{node.GetType().Name}' is not supported.");
        }

        private FilterExpression VisitFunction(SingleValueFunctionCallNode call, Scope scope, int depth)
        {
            var function = call.Name switch
            {
                "contains" => FilterTextFunction.Contains,
                "endswith" => FilterTextFunction.EndsWith,
                "startswith" => FilterTextFunction.StartsWith,
                _ => throw new InvalidCrudQueryException($"Function '{call.Name}' is not supported."),
            };

            var parameters = call.Parameters.ToList();

            if (parameters.Count != 2)
            {
                throw new InvalidCrudQueryException($"Function '{call.Name}' is not supported.");
            }

            var first = VisitOperand(parameters[0], scope, depth + 1);
            var second = VisitOperand(parameters[1], scope, depth + 1);
            var property = first as PropertyOperand ?? second as PropertyOperand;
            var constant = first as ConstantOperand ?? second as ConstantOperand;

            if (property is null || constant is null || property.Type != CrudScalarType.String || constant.Value is not string text)
            {
                throw new InvalidCrudQueryException($"Function '{call.Name}' is not supported on '{property?.ContractName ?? call.Name}'.");
            }

            return new FilterTextMatch(property.EntityPath, function, text);
        }

        private Operand VisitOperand(QueryNode node, Scope scope, int depth)
        {
            Count(depth);

            if (node is ConvertNode convert)
            {
                return VisitOperand(convert.Source, scope, depth + 1);
            }

            if (node is ConstantNode constant)
            {
                return new ConstantOperand(constant.Value);
            }

            if (node is NonResourceRangeVariableReferenceNode)
            {
                if (scope.ElementType is not CrudScalarType elementType)
                {
                    throw new InvalidCrudQueryException("Property name is required.");
                }

                return new ElementOperand(elementType, scope.ElementContractName ?? "element");
            }

            if (node is SingleValuePropertyAccessNode access)
            {
                EnsureRange(access.Source);

                var property = RequireProperty(scope, access.Property.Name, "$filter");

                if (property.Type is not CrudScalarType type)
                {
                    throw new InvalidCrudQueryException($"Property '{access.Property.Name}' cannot be used in $filter.");
                }

                return new PropertyOperand(access.Property.Name, property.EntityPath, type, property.Nullable);
            }

            throw new InvalidCrudQueryException($"Expression '{node.GetType().Name}' is not supported.");
        }

        private string VisitSort(QueryNode node, Scope scope, int depth)
        {
            Count(depth);

            if (node is ConvertNode convert)
            {
                return VisitSort(convert.Source, scope, depth + 1);
            }

            if (node is not SingleValuePropertyAccessNode access)
            {
                throw new InvalidCrudQueryException($"Expression '{node.GetType().Name}' is not supported in $orderby.");
            }

            EnsureRange(access.Source);

            var property = RequireProperty(scope, access.Property.Name, "$orderby");

            if (property.Collection is not null || property.Type is null)
            {
                throw new InvalidCrudQueryException($"Property '{access.Property.Name}' cannot be used in $orderby.");
            }

            return property.EntityPath;
        }

        private static string CollectionContractName(QueryNode node)
        {
            if (node is CollectionPropertyAccessNode access)
            {
                return access.Property.Name;
            }

            if (node is CollectionComplexNode complex)
            {
                return complex.Property.Name;
            }

            return "element";
        }

        private static void EnsureRange(QueryNode? node)
        {
            if (node is null || node is ResourceRangeVariableReferenceNode or NonResourceRangeVariableReferenceNode)
            {
                return;
            }

            if (node is ConvertNode convert)
            {
                EnsureRange(convert.Source);

                return;
            }

            throw new InvalidCrudQueryException($"Expression '{node.GetType().Name}' is not supported.");
        }

        private CrudProperty RequireProperty(Scope scope, string name, string clause)
        {
            if (!scope.Properties.TryGetValue(name, out var property))
            {
                throw new InvalidCrudQueryException($"Property '{name}' cannot be used in {clause}.");
            }

            var allowed = clause == "$orderby" ? property.CanSort : property.CanFilter;

            if (!allowed)
            {
                throw new InvalidCrudQueryException($"Property '{name}' cannot be used in {clause}.");
            }

            return property;
        }

        private static object? ScalarValue(ConstantOperand constant, Operand target)
        {
            var type = target switch
            {
                ElementOperand element => element.Type,
                PropertyOperand property => property.Type,
                _ => throw new InvalidCrudQueryException("The comparison value is not supported."),
            };
            var nullable = target is PropertyOperand propertyOperand && propertyOperand.Nullable;
            var name = target switch
            {
                ElementOperand element => element.ContractName,
                PropertyOperand property => property.ContractName,
                _ => "value",
            };

            if (constant.Value is null)
            {
                if (!nullable)
                {
                    throw new InvalidCrudQueryException($"Property '{name}' cannot be compared to null.");
                }

                return null;
            }

            var matches = type switch
            {
                CrudScalarType.Boolean => constant.Value is bool,
                CrudScalarType.DateTimeOffset => constant.Value is DateTimeOffset,
                CrudScalarType.Decimal => constant.Value is decimal,
                CrudScalarType.Guid => constant.Value is Guid,
                CrudScalarType.Int32 => constant.Value is int,
                CrudScalarType.String => constant.Value is string,
                _ => false,
            };

            if (!matches)
            {
                throw new InvalidCrudQueryException(
                    $"Value of type '{constant.Value.GetType().Name}' is not compatible with property '{name}'.");
            }

            return constant.Value;
        }

        private static (FilterOperator Operator, Operand Left, Operand Right) Swap(
            BinaryOperatorKind kind,
            Operand left,
            Operand right)
        {
            var propertyOnRight = right is PropertyOperand or ElementOperand && left is ConstantOperand;

            if (!propertyOnRight)
            {
                return (Map(kind), left, right);
            }

            var swapped = kind switch
            {
                BinaryOperatorKind.GreaterThan => BinaryOperatorKind.LessThan,
                BinaryOperatorKind.GreaterThanOrEqual => BinaryOperatorKind.LessThanOrEqual,
                BinaryOperatorKind.LessThan => BinaryOperatorKind.GreaterThan,
                BinaryOperatorKind.LessThanOrEqual => BinaryOperatorKind.GreaterThanOrEqual,
                _ => kind,
            };

            return (Map(swapped), right, left);
        }

        private static FilterOperator Map(BinaryOperatorKind kind)
        {
            return kind switch
            {
                BinaryOperatorKind.Equal => FilterOperator.Equal,
                BinaryOperatorKind.GreaterThan => FilterOperator.GreaterThan,
                BinaryOperatorKind.GreaterThanOrEqual => FilterOperator.GreaterThanOrEqual,
                BinaryOperatorKind.LessThan => FilterOperator.LessThan,
                BinaryOperatorKind.LessThanOrEqual => FilterOperator.LessThanOrEqual,
                BinaryOperatorKind.NotEqual => FilterOperator.NotEqual,
                _ => throw new InvalidCrudQueryException($"Operator '{kind}' is not supported."),
            };
        }
    }

    private abstract record Operand;

    private sealed record ConstantOperand(object? Value) : Operand;

    private sealed record ElementOperand(CrudScalarType Type, string ContractName) : Operand;

    private sealed record PropertyOperand(string ContractName, string EntityPath, CrudScalarType Type, bool Nullable) : Operand;

    private sealed record Scope(
        IReadOnlyDictionary<string, CrudProperty> Properties,
        CrudScalarType? ElementType,
        string? ElementContractName);
}
