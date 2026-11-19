using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;

namespace Neox.Extensions.Crud.AspNetCore.Tests;

public sealed class ArticlesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ArticlesEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_FilterSortPageAndCount_ReturnsTheMatchingPage()
    {
        var response = await _client.GetAsync(
            "/articles?$filter=Title eq 'x'&$orderby=PublishedAt desc,Title asc&$top=20&$skip=0&$count=true");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(20, body.GetProperty("pageSize").GetInt32());
        Assert.Equal("x", body.GetProperty("items")[0].GetProperty("slug").GetString());
    }

    [Fact]
    public async Task Get_InvalidFilter_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/articles?$filter=Nope eq 'x'");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Nope", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_UnsupportedOption_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/articles?$expand=Tags");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("$expand", body, StringComparison.Ordinal);
    }
}
