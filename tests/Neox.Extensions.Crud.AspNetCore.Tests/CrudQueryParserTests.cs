using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

using Neox.Extensions.Crud.AspNetCore;
using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.AspNetCore.Tests;

public sealed class CrudQueryParserTests
{
    private static readonly Guid SampleId = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");

    [Fact]
    public void Parse_AnyOnComplexId_MapsTheElementProperty()
    {
        var result = Parse(("$filter", $"TagIds/any(t: t/Id eq {SampleId})"));
        var any = Assert.IsType<FilterAny>(result.Page.Filter);
        var comparison = Assert.IsType<FilterComparison>(any.Predicate);

        Assert.Equal("TagItems", any.Property);
        Assert.Equal("Id", comparison.Property);
        Assert.Equal(FilterOperator.Equal, comparison.Operator);
        Assert.Equal(SampleId, comparison.Value);
    }

    [Fact]
    public void Parse_AnyOnPrimitive_UsesTheElement()
    {
        var result = Parse(("$filter", "Tags/any(t: t eq 'news')"));
        var any = Assert.IsType<FilterAny>(result.Page.Filter);
        var comparison = Assert.IsType<FilterComparison>(any.Predicate);

        Assert.Equal("Tags", any.Property);
        Assert.Equal(string.Empty, comparison.Property);
        Assert.Equal("news", comparison.Value);
    }

    [Fact]
    public void Parse_ComparisonOperators_MapToFilterOperators()
    {
        AssertOperator("Title eq 'x'", FilterOperator.Equal, "x");
        AssertOperator("Title ne 'x'", FilterOperator.NotEqual, "x");
        AssertOperator("Rank gt 3", FilterOperator.GreaterThan, 3);
        AssertOperator("Rank ge 3", FilterOperator.GreaterThanOrEqual, 3);
        AssertOperator("Rank lt 3", FilterOperator.LessThan, 3);
        AssertOperator("Rank le 3", FilterOperator.LessThanOrEqual, 3);
    }

    [Fact]
    public void Parse_CountFalse_IsAccepted()
    {
        var result = Parse(("$count", "false"));

        Assert.False(result.Count);
    }

    [Fact]
    public void Parse_CountInvalid_Throws()
    {
        var exception = Assert.Throws<InvalidCrudQueryException>(() => Parse(("$count", "yes")));

        Assert.Contains("$count", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_DuplicateOption_Throws()
    {
        var exception = Assert.Throws<InvalidCrudQueryException>(() => Parse(("$top", "1"), ("$top", "2")));

        Assert.Contains("$top", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_IncompatibleType_Throws()
    {
        var exception = Assert.Throws<InvalidCrudQueryException>(() => Parse(("$filter", "Rank eq 'x'")));

        Assert.Contains("Rank", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Literals_AreTyped()
    {
        var guid = AssertComparison(Parse(("$filter", $"CategoryId eq {SampleId}")));
        var number = AssertComparison(Parse(("$filter", "Rank eq 3")));
        var score = AssertComparison(Parse(("$filter", "Score eq 1.5M")));
        var published = AssertComparison(Parse(("$filter", "PublishedAt eq 2026-03-01T00:00:00Z")));
        var archived = AssertComparison(Parse(("$filter", "Archived eq true")));
        var missing = AssertComparison(Parse(("$filter", "PublishedAt eq null")));

        Assert.Equal(SampleId, guid.Value);
        Assert.Equal(3, number.Value);
        Assert.Equal(1.5m, score.Value);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), published.Value);
        Assert.Equal(true, archived.Value);
        Assert.Null(missing.Value);
    }

    [Fact]
    public void Parse_NotAndParentheses_PreserveGrouping()
    {
        var negated = Parse(("$filter", "not (Archived eq true)"));
        var not = Assert.IsType<FilterNot>(negated.Page.Filter);
        var archived = Assert.IsType<FilterComparison>(not.Operand);

        Assert.Equal("Archived", archived.Property);

        var grouped = Parse(("$filter", "(Title eq 'a' or Title eq 'b') and Archived eq true"));
        var and = Assert.IsType<FilterGroup>(grouped.Page.Filter);
        var or = Assert.IsType<FilterGroup>(and.Operands[0]);

        Assert.Equal(FilterGroupKind.And, and.Kind);
        Assert.Equal(FilterGroupKind.Or, or.Kind);
        Assert.Equal("Slug", Assert.IsType<FilterComparison>(or.Operands[0]).Property);
        Assert.Equal("Archived", Assert.IsType<FilterComparison>(and.Operands[1]).Property);
    }

    [Fact]
    public void Parse_OrderByAndPage_MapNamesAndClampTop()
    {
        var result = Parse(
            ("$filter", "Title eq 'x'"),
            ("$orderby", "PublishedAt desc,Title asc"),
            ("$top", "150"),
            ("$skip", "100"),
            ("$count", "true"));

        Assert.True(result.Count);
        Assert.Equal(2, result.Page.Page);
        Assert.Equal(PageRequest.MaxPageSize, result.Page.PageSize);
        Assert.Equal("Slug", Assert.IsType<FilterComparison>(result.Page.Filter).Property);
        Assert.Equal("PublishedAt", result.Page.Sorts[0].Property);
        Assert.Equal(SortDirection.Descending, result.Page.Sorts[0].Direction);
        Assert.Equal("Slug", result.Page.Sorts[1].Property);
        Assert.Equal(SortDirection.Ascending, result.Page.Sorts[1].Direction);
    }

    [Fact]
    public void Parse_PropertyOutsideWhitelist_Throws()
    {
        var unknown = Assert.Throws<InvalidCrudQueryException>(() => Parse(("$filter", "Nope eq 'x'")));
        var hidden = Assert.Throws<InvalidCrudQueryException>(() => Parse(("$filter", "Hidden eq 'x'")));
        var sort = Assert.Throws<InvalidCrudQueryException>(() => Parse(("$orderby", "Rank")));

        Assert.Contains("Nope", unknown.Message, StringComparison.Ordinal);
        Assert.Contains("Hidden", hidden.Message, StringComparison.Ordinal);
        Assert.Contains("Rank", sort.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_SkipNotMultipleOfPageSize_Throws()
    {
        var exception = Assert.Throws<InvalidCrudQueryException>(() => Parse(("$top", "150"), ("$skip", "150")));

        Assert.Contains("$skip", exception.Message, StringComparison.Ordinal);
        Assert.Contains(PageRequest.MaxPageSize.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_TextFunctions_MapToTheEntityProperty()
    {
        AssertText("contains(Title,'news')", FilterTextFunction.Contains);
        AssertText("startswith(Title,'news')", FilterTextFunction.StartsWith);
        AssertText("endswith(Title,'news')", FilterTextFunction.EndsWith);
    }

    [Fact]
    public void Parse_TooDeepOrTooManyNodes_Throws()
    {
        var deep = Assert.Throws<InvalidCrudQueryException>(() =>
            Parse(ArticleListQuery.Create(maxDepth: 2), ("$filter", "not (Title eq 'x')")));
        var wide = Assert.Throws<InvalidCrudQueryException>(() =>
            Parse(ArticleListQuery.Create(maxNodes: 2), ("$filter", "Title eq 'x'")));

        Assert.Contains("2", deep.Message, StringComparison.Ordinal);
        Assert.Contains("2", wide.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_UnsupportedOption_Throws()
    {
        AssertOption("$expand");
        AssertOption("$select");
        AssertOption("$apply");
    }

    [Fact]
    public void Parse_WithoutTop_UsesTheDefaultPageSize()
    {
        var result = Parse(("$skip", "20"));

        Assert.Equal(2, result.Page.Page);
        Assert.Equal(PageRequest.DefaultPageSize, result.Page.PageSize);
        Assert.False(result.Count);
        Assert.Null(result.Page.Filter);
    }

    private static void AssertOperator(string filter, FilterOperator expected, object value)
    {
        var comparison = AssertComparison(Parse(("$filter", filter)));

        Assert.Equal(expected, comparison.Operator);
        Assert.Equal(value, comparison.Value);
    }

    private static void AssertOption(string option)
    {
        var exception = Assert.Throws<InvalidCrudQueryException>(() => Parse((option, "value")));

        Assert.Contains(option, exception.Message, StringComparison.Ordinal);
    }

    private static void AssertText(string filter, FilterTextFunction function)
    {
        var match = Assert.IsType<FilterTextMatch>(Parse(("$filter", filter)).Page.Filter);

        Assert.Equal("Slug", match.Property);
        Assert.Equal(function, match.Function);
        Assert.Equal("news", match.Value);
    }

    private static FilterComparison AssertComparison(CrudQueryResult result)
    {
        return Assert.IsType<FilterComparison>(result.Page.Filter);
    }

    private static CrudQueryResult Parse(params (string Key, string Value)[] items)
    {
        return Parse(ArticleListQuery.Schema, items);
    }

    private static CrudQueryResult Parse(CrudQuerySchema schema, params (string Key, string Value)[] items)
    {
        var dictionary = new Dictionary<string, StringValues>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            dictionary[item.Key] = dictionary.TryGetValue(item.Key, out var existing)
                ? StringValues.Concat(existing, item.Value)
                : new StringValues(item.Value);
        }

        return CrudQueryParser.Parse(new QueryCollection(dictionary), schema);
    }
}
