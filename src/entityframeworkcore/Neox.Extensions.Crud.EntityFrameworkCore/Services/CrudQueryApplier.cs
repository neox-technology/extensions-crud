using System.Linq.Expressions;
using System.Reflection;

using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.EntityFrameworkCore.Services;

internal static class CrudQueryApplier
{
    public static IQueryable<TEntity> ApplyFilters<TEntity>(
        IQueryable<TEntity> source,
        IReadOnlyList<FilterCriterion> filters,
        FilterExpression? filter)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(filters);

        foreach (var criterion in filters)
        {
            source = ApplyFilter(source, criterion);
        }

        if (filter is null)
        {
            return source;
        }

        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        var body = Build(filter, parameter, typeof(TEntity));
        var lambda = Expression.Lambda<Func<TEntity, bool>>(body, parameter);

        return source.Where(lambda);
    }

    public static IQueryable<TEntity> ApplySorts<TEntity>(IQueryable<TEntity> source, IReadOnlyList<SortCriterion> sorts)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sorts);

        if (sorts.Count == 0)
        {
            return source.OrderBy(entity => entity.Id);
        }

        IOrderedQueryable<TEntity>? ordered = null;

        foreach (var sort in sorts)
        {
            var property = RequireProperty<TEntity>(sort.Property);

            if (ElementType(property.PropertyType) is not null)
            {
                throw new InvalidCrudQueryException($"Property '{sort.Property}' cannot be used for sorting.");
            }

            ordered = OrderByProperty(
                ordered is null ? source : ordered,
                property,
                sort.Direction == SortDirection.Descending,
                thenBy: ordered is not null);
        }

        if (!sorts.Any(sort => string.Equals(sort.Property, nameof(IEntity.Id), StringComparison.Ordinal)))
        {
            ordered = ordered!.ThenBy(entity => entity.Id);
        }

        return ordered!;
    }

    private static IQueryable<TEntity> ApplyFilter<TEntity>(IQueryable<TEntity> source, FilterCriterion filter)
    {
        var property = RequireProperty<TEntity>(filter.Property);
        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        var access = Expression.Property(parameter, property);

        var body = filter.Operator switch
        {
            FilterOperator.Contains => Contains(access, filter.Value, filter.Property),
            FilterOperator.Equal => Compare(Expression.Equal, access, filter.Value, filter.Property),
            FilterOperator.GreaterThan => Compare(Expression.GreaterThan, access, filter.Value, filter.Property),
            FilterOperator.GreaterThanOrEqual => Compare(Expression.GreaterThanOrEqual, access, filter.Value, filter.Property),
            FilterOperator.LessThan => Compare(Expression.LessThan, access, filter.Value, filter.Property),
            FilterOperator.LessThanOrEqual => Compare(Expression.LessThanOrEqual, access, filter.Value, filter.Property),
            FilterOperator.NotEqual => Compare(Expression.NotEqual, access, filter.Value, filter.Property),
            _ => throw new InvalidCrudQueryException($"Filter operator '{filter.Operator}' is not supported."),
        };

        var lambda = Expression.Lambda<Func<TEntity, bool>>(body, parameter);

        return source.Where(lambda);
    }

    private static Expression Build(FilterExpression filter, ParameterExpression parameter, Type currentType)
    {
        return filter switch
        {
            FilterAny any => BuildAny(any, parameter, currentType),
            FilterComparison comparison => BuildComparison(comparison, parameter, currentType),
            FilterGroup group => BuildGroup(group, parameter, currentType),
            FilterNot not => Expression.Not(Build(not.Operand, parameter, currentType)),
            FilterTextMatch text => BuildText(text, parameter, currentType),
            _ => throw new InvalidCrudQueryException($"Filter '{filter.GetType().Name}' is not supported."),
        };
    }

    private static Expression BuildAny(FilterAny any, ParameterExpression parameter, Type currentType)
    {
        var property = RequireProperty(currentType, any.Property);
        var elementType = ElementType(property.PropertyType);

        if (elementType is null)
        {
            throw new InvalidCrudQueryException($"Property '{any.Property}' is not a collection.");
        }

        var element = Expression.Parameter(elementType, "element");
        var body = Build(any.Predicate, element, elementType);
        var predicate = Expression.Lambda(body, element);
        var anyMethod = typeof(Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == nameof(Enumerable.Any) && method.GetParameters().Length == 2)
            .MakeGenericMethod(elementType);

        return Expression.Call(anyMethod, Expression.Property(parameter, property), predicate);
    }

    private static Expression BuildComparison(FilterComparison comparison, ParameterExpression parameter, Type currentType)
    {
        Expression access;

        if (comparison.Property.Length == 0)
        {
            if (!IsScalar(currentType))
            {
                throw new InvalidCrudQueryException("Property name is required.");
            }

            access = parameter;
        }
        else
        {
            var property = RequireProperty(currentType, comparison.Property);

            access = Expression.Property(parameter, property);
        }

        return comparison.Operator switch
        {
            FilterOperator.Contains => Contains(access, comparison.Value, comparison.Property),
            FilterOperator.Equal => Compare(Expression.Equal, access, comparison.Value, comparison.Property),
            FilterOperator.GreaterThan => Compare(Expression.GreaterThan, access, comparison.Value, comparison.Property),
            FilterOperator.GreaterThanOrEqual => Compare(Expression.GreaterThanOrEqual, access, comparison.Value, comparison.Property),
            FilterOperator.LessThan => Compare(Expression.LessThan, access, comparison.Value, comparison.Property),
            FilterOperator.LessThanOrEqual => Compare(Expression.LessThanOrEqual, access, comparison.Value, comparison.Property),
            FilterOperator.NotEqual => Compare(Expression.NotEqual, access, comparison.Value, comparison.Property),
            _ => throw new InvalidCrudQueryException($"Filter operator '{comparison.Operator}' is not supported."),
        };
    }

    private static Expression BuildGroup(FilterGroup group, ParameterExpression parameter, Type currentType)
    {
        if (group.Operands.Count < 2)
        {
            throw new InvalidCrudQueryException("A filter group requires at least two operands.");
        }

        var combine = group.Kind == FilterGroupKind.And
            ? new Func<Expression, Expression, Expression>(Expression.AndAlso)
            : Expression.OrElse;

        Expression? combined = null;

        foreach (var operand in group.Operands)
        {
            var next = Build(operand, parameter, currentType);

            combined = combined is null ? next : combine(combined, next);
        }

        return combined!;
    }

    private static Expression BuildText(FilterTextMatch text, ParameterExpression parameter, Type currentType)
    {
        var property = RequireProperty(currentType, text.Property);
        var underlying = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (underlying != typeof(string))
        {
            throw new InvalidCrudQueryException($"Property '{text.Property}' is not a string.");
        }

        if (!Enum.IsDefined(text.Function))
        {
            throw new InvalidCrudQueryException($"Text function '{text.Function}' is not supported.");
        }

        var methodName = text.Function switch
        {
            FilterTextFunction.Contains => nameof(string.Contains),
            FilterTextFunction.EndsWith => nameof(string.EndsWith),
            FilterTextFunction.StartsWith => nameof(string.StartsWith),
            _ => throw new InvalidCrudQueryException($"Text function '{text.Function}' is not supported."),
        };

        var method = typeof(string).GetMethod(methodName, [typeof(string)])
            ?? throw new InvalidCrudQueryException($"Text function '{text.Function}' is not supported.");
        var access = Expression.Property(parameter, property);

        return Expression.Call(access, method, Expression.Constant(text.Value));
    }

    private static Expression Compare(
        Func<Expression, Expression, BinaryExpression> comparison,
        Expression access,
        object? value,
        string propertyName)
    {
        return comparison(access, Constant(access.Type, value, propertyName));
    }

    private static Expression Constant(Type targetType, object? value, string propertyName)
    {
        var underlying = Nullable.GetUnderlyingType(targetType);
        var effective = underlying ?? targetType;

        if (value is null)
        {
            if (underlying is null && effective.IsValueType)
            {
                throw new InvalidCrudQueryException($"Property '{propertyName}' cannot be compared to null.");
            }

            return Expression.Constant(null, targetType);
        }

        if (!effective.IsInstanceOfType(value))
        {
            throw new InvalidCrudQueryException(
                $"Value of type '{value.GetType().Name}' is not compatible with property '{propertyName}' of type '{targetType.Name}'.");
        }

        if (underlying is null)
        {
            return Expression.Constant(value, targetType);
        }

        var nullableValue = Activator.CreateInstance(targetType, value);

        return Expression.Constant(nullableValue, targetType);
    }

    private static Expression Contains(Expression access, object? value, string propertyName)
    {
        var elementType = ElementType(access.Type);

        if (elementType is null)
        {
            throw new InvalidCrudQueryException($"Property '{propertyName}' is not a collection.");
        }

        if (value is null || !elementType.IsInstanceOfType(value))
        {
            var valueType = value is null ? "null" : value.GetType().Name;

            throw new InvalidCrudQueryException(
                $"Value of type '{valueType}' is not compatible with property '{propertyName}' of type '{elementType.Name}'.");
        }

        var contains = typeof(Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == nameof(Enumerable.Contains) && method.GetParameters().Length == 2)
            .MakeGenericMethod(elementType);

        return Expression.Call(contains, access, Expression.Constant(value, elementType));
    }

    private static Type? ElementType(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        foreach (var interfaceType in type.GetInterfaces().Append(type))
        {
            if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return interfaceType.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static bool IsScalar(Type type)
    {
        var effective = Nullable.GetUnderlyingType(type) ?? type;

        return effective == typeof(string)
            || effective == typeof(Guid)
            || effective == typeof(DateTime)
            || effective == typeof(DateTimeOffset)
            || effective == typeof(decimal)
            || effective.IsPrimitive
            || effective.IsEnum;
    }

    private static IOrderedQueryable<TEntity> OrderByProperty<TEntity>(
        IQueryable<TEntity> source,
        PropertyInfo property,
        bool descending,
        bool thenBy)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        var access = Expression.Property(parameter, property);
        var lambda = Expression.Lambda(access, parameter);
        var methodName = thenBy
            ? descending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy)
            : descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy);

        var method = typeof(Queryable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(candidate => candidate.Name == methodName && candidate.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(TEntity), property.PropertyType);

        return (IOrderedQueryable<TEntity>)method.Invoke(null, [source, lambda])!;
    }

    private static PropertyInfo RequireProperty(Type type, string propertyName)
    {
        var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

        if (property?.GetMethod is null)
        {
            throw new InvalidCrudQueryException($"Property '{propertyName}' was not found on '{type.Name}'.");
        }

        return property;
    }

    private static PropertyInfo RequireProperty<TEntity>(string propertyName)
    {
        return RequireProperty(typeof(TEntity), propertyName);
    }
}
