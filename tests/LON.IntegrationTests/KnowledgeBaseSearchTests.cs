using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// Regression guard for P6.42: KnowledgeBaseController.SearchRequest was a
/// positional record and failed model binding on {"query":"..."} bodies with
/// 400 "The request field is required". Converted to an init-only record so
/// System.Text.Json can populate it through ordinary property setters.
///
/// Vector store is disabled in the test factory (EnableVectorStore=false), so
/// these assertions intentionally focus on the model-binding contract only —
/// the endpoint must not reject a well-formed body with 400.
/// </summary>
public class KnowledgeBaseSearchTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public KnowledgeBaseSearchTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Search_WithLowercaseQuery_BindsAndDoesNotReturn400()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync(
            "/api/KnowledgeBase/search",
            new { query = "customs procedure", topK = 3 });

        resp.StatusCode.Should().NotBe(HttpStatusCode.BadRequest,
            "the positional-record binder bug fixed in P6.42 must not regress");
    }

    [Fact]
    public async Task Search_WithPascalCaseQuery_BindsAndDoesNotReturn400()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync(
            "/api/KnowledgeBase/search",
            new { Query = "tariff", TopK = 5, MinSimilarity = 0.6 });

        resp.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_WithEmptyQuery_Returns400WithControllerValidationMessage()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync(
            "/api/KnowledgeBase/search",
            new { query = "" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("Query не може да биде празен",
            "empty query must hit the controller-level validation, not the JSON binder");
    }
}
