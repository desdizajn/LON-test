using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P5.1.5 — target entity schemas + registry.
///   1. GET /targets returns all 5 schemas.
///   2. GET /targets/{name} returns field list for Receipts with required flags.
///   3. GET /targets/unknown -> 404.
/// </summary>
public class ImportTargetTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public ImportTargetTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ListTargets_ReturnsAllKnownSchemas()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var result = await client.GetFromJsonAsync<ResultResponse<List<TargetRow>>>("/api/import/targets");
        result!.IsSuccess.Should().BeTrue();
        result.Data!.Select(t => t.TargetName).Should().BeEquivalentTo(
            new[] { "BOMs", "CustomsDeclarations", "Items", "Partners", "Receipts" });
    }

    [Fact]
    public async Task GetTarget_Receipts_DescribesHeaderAndRowFields()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var result = await client.GetFromJsonAsync<ResultResponse<TargetRow>>("/api/import/targets/Receipts");
        result!.IsSuccess.Should().BeTrue();
        var fields = result.Data!.Fields;

        fields.Should().ContainSingle(f => f.Name == "receiptDate" && f.Required && f.Scope == 2 /* Header */);
        fields.Should().ContainSingle(f => f.Name == "warehouseCode" && f.LookupEntity == "Warehouses");
        fields.Should().ContainSingle(f => f.Name == "itemCode" && f.Required && f.Scope == 1 /* Row */);
        fields.Should().ContainSingle(f => f.Name == "qualityStatus" && f.EnumValues!.Contains("OK"));
    }

    [Fact]
    public async Task GetTarget_Unknown_Returns404()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var resp = await client.GetAsync("/api/import/targets/DoesNotExist");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task Authenticate(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private sealed record TargetRow(string TargetName, string DisplayLabel, List<FieldRow> Fields);
    private sealed record FieldRow(
        string Name, string Label, int Type, bool Required, int Scope,
        List<string>? EnumValues, string? LookupEntity, string? LookupField);
    private sealed record ResultResponse<T>(bool IsSuccess, T? Data, string? ErrorMessage);
    private sealed record LoginResponse(string AccessToken);
}
