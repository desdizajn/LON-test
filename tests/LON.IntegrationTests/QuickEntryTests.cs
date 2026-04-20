using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P5.2.8 — single-line command bar.
///
/// Cases:
///   1. `help` — happy path, returns verb=help + catalogue.
///   2. Empty command → 400.
///   3. Unknown verb → 400 with "Unknown verb".
///   4. `move &lt;batch&gt; bogus-stage` → 400 with "Unknown stage".
/// </summary>
public class QuickEntryTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public QuickEntryTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Help_ReturnsCatalogue()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var resp = await client.PostAsJsonAsync("/api/quickentry/execute", new { command = "help" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ResultResponse>();
        body!.IsSuccess.Should().BeTrue();
        body.Data!.Verb.Should().Be("help");
        body.Data.Outcome.Should().Contain("issue").And.Contain("release").And.Contain("move");
    }

    [Fact]
    public async Task Empty_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var resp = await client.PostAsJsonAsync("/api/quickentry/execute", new { command = "" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnknownVerb_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var resp = await client.PostAsJsonAsync("/api/quickentry/execute", new { command = "fizzbuzz" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<ResultResponse>();
        body!.ErrorMessage.Should().Contain("Unknown verb");
    }

    [Fact]
    public async Task Move_UnknownStage_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var resp = await client.PostAsJsonAsync("/api/quickentry/execute", new { command = "move SOMEBATCH fizz" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<ResultResponse>();
        body!.ErrorMessage.Should().Contain("Unknown stage");
    }

    private static async Task Authenticate(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record ResultData(string Verb, string Outcome, object? Payload);
    private sealed record ResultResponse(bool IsSuccess, ResultData? Data, string? ErrorMessage);
}
