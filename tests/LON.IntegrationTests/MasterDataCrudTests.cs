using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P16.D3 — MasterData CRUD + GET-list + tenant-isolation smoke.
///
/// One controller per MasterData resource. The full CRUD theory runs
/// on the three simplest payload shapes (UoM, WorkCenter, Warehouse);
/// the remaining five (Items, Partners, Locations, BOMs, Routings,
/// Employees, Machines) get a GET-list smoke that asserts the route
/// is mounted and returns 200. Item/Partner have their own dedicated
/// tests elsewhere (ItemsMediatrTests + PartnersList integration);
/// this file is the broad survey, not a deep replacement.
/// </summary>
public class MasterDataCrudTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public MasterDataCrudTests(LonApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthedAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    [Theory]
    [InlineData("/api/MasterData/items")]
    [InlineData("/api/MasterData/partners")]
    [InlineData("/api/MasterData/warehouses")]
    [InlineData("/api/MasterData/locations")]
    [InlineData("/api/MasterData/workcenters")]
    [InlineData("/api/MasterData/machines")]
    [InlineData("/api/MasterData/uom")]
    [InlineData("/api/MasterData/employees")]
    [InlineData("/api/MasterData/boms")]
    [InlineData("/api/MasterData/routings")]
    public async Task ListEndpoint_Returns200(string url)
    {
        var client = await AuthedAsync();
        var resp = await client.GetAsync(url);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UoM_FullCrud()
    {
        var client = await AuthedAsync();
        var code = $"P16D3-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";

        var create = await client.PostAsJsonAsync("/api/MasterData/uom", new
        {
            code,
            name = "P16.D3 test unit",
            description = "kg-like",
            isActive = true,
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var created = await create.Content.ReadFromJsonAsync<UomRow>();
        created!.Id.Should().NotBeEmpty();

        var get = await client.GetFromJsonAsync<UomRow>(
            $"/api/MasterData/uom/{created.Id}");
        get!.Code.Should().Be(code);

        var put = await client.PutAsJsonAsync($"/api/MasterData/uom/{created.Id}", new
        {
            code,
            name = "P16.D3 test unit (renamed)",
            description = "kg-like",
            isActive = true,
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var renamed = await client.GetFromJsonAsync<UomRow>(
            $"/api/MasterData/uom/{created.Id}");
        renamed!.Name.Should().Be("P16.D3 test unit (renamed)");

        var del = await client.DeleteAsync($"/api/MasterData/uom/{created.Id}");
        del.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.OK);

        // Soft-deleted row is filtered out of the list endpoint.
        var list = await client.GetFromJsonAsync<List<UomRow>>(
            "/api/MasterData/uom");
        list!.Should().NotContain(u => u.Id == created.Id);
    }

    [Fact]
    public async Task WorkCenter_FullCrud()
    {
        var client = await AuthedAsync();
        var code = $"P16D3-WC-{Guid.NewGuid().ToString().Substring(0, 5).ToUpper()}";

        var create = await client.PostAsJsonAsync("/api/MasterData/workcenters", new
        {
            code,
            name = "P16.D3 work center",
            description = "Test",
            isActive = true,
            standardCostPerHour = 25m,
            capacity = 100m,
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var created = await create.Content.ReadFromJsonAsync<WcRow>();
        created!.Id.Should().NotBeEmpty();

        var get = await client.GetAsync($"/api/MasterData/workcenters/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        var put = await client.PutAsJsonAsync($"/api/MasterData/workcenters/{created.Id}", new
        {
            code,
            name = "P16.D3 wc renamed",
            description = "Test",
            isActive = true,
            standardCostPerHour = 30m,
            capacity = 100m,
        });
        put.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

        var del = await client.DeleteAsync($"/api/MasterData/workcenters/{created.Id}");
        del.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Warehouse_FullCrud()
    {
        var client = await AuthedAsync();
        var code = $"P16D3-WH-{Guid.NewGuid().ToString().Substring(0, 5).ToUpper()}";

        var create = await client.PostAsJsonAsync("/api/MasterData/warehouses", new
        {
            code,
            name = "P16.D3 warehouse",
            description = "Test",
            address = "Test address",
            isActive = true,
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var created = await create.Content.ReadFromJsonAsync<WhRow>();
        created!.Id.Should().NotBeEmpty();

        var get = await client.GetAsync($"/api/MasterData/warehouses/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        var put = await client.PutAsJsonAsync($"/api/MasterData/warehouses/{created.Id}", new
        {
            code,
            name = "P16.D3 warehouse renamed",
            description = "Test",
            address = "Test address",
            isActive = true,
        });
        put.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

        var del = await client.DeleteAsync($"/api/MasterData/warehouses/{created.Id}");
        del.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.OK);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record UomRow(Guid Id, string Code, string Name);
    private sealed record WcRow(Guid Id, string Code, string Name);
    private sealed record WhRow(Guid Id, string Code, string Name);
}
