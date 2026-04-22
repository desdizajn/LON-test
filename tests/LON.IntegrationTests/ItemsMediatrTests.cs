using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P6.11 regression guard — Items CRUD goes through MediatR handlers
/// in LON.Application.MasterData.Items. These tests hit the HTTP surface
/// and assert the DB actually reflects the operation (per Contract
/// Hygiene Protocol §3: POST → GET → assert DB state).
/// </summary>
public class ItemsMediatrTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public ItemsMediatrTests(LonApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthedAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Create_Then_Get_RoundTrips_Through_Mediator()
    {
        var client = await AuthedAsync();

        // Need a UoM for the FK. Pick any active one from the seeded set.
        var uoms = await client.GetFromJsonAsync<List<UoMRow>>("/api/MasterData/uom");
        var uom = uoms!.First(u => u.IsActive).Id;

        var code = $"P6-11-MED-{Guid.NewGuid():N}".Substring(0, 20);
        var create = await client.PostAsJsonAsync("/api/MasterData/items",
            new
            {
                code,
                name = "MediatR CRUD smoke",
                description = "round-trip test",
                itemType = ItemType.RawMaterial,
                uoMId = uom,
                isBatchRequired = false,
                isMRNRequired = false,
                isActive = true
            });
        create.StatusCode.Should().Be(HttpStatusCode.OK);

        var created = await create.Content.ReadFromJsonAsync<ItemRow>();
        created.Should().NotBeNull();
        created!.Code.Should().Be(code);
        created.IsActive.Should().BeTrue();

        // GET list includes the new item
        var list = await client.GetFromJsonAsync<List<ItemRow>>("/api/MasterData/items");
        list.Should().Contain(r => r.Id == created.Id && r.Code == code);

        // GET by id matches
        var detail = await client.GetFromJsonAsync<ItemRow>($"/api/MasterData/items/{created.Id}");
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task Update_PersistsChanges_Through_Mediator()
    {
        var client = await AuthedAsync();
        var uoms = await client.GetFromJsonAsync<List<UoMRow>>("/api/MasterData/uom");
        var uom = uoms!.First(u => u.IsActive).Id;

        var code = $"UPD-{Guid.NewGuid():N}".Substring(0, 20);
        var create = await client.PostAsJsonAsync("/api/MasterData/items", new
        {
            code, name = "before",
            itemType = ItemType.RawMaterial, uoMId = uom,
            isBatchRequired = false, isMRNRequired = false, isActive = true
        });
        var created = await create.Content.ReadFromJsonAsync<ItemRow>();

        var update = await client.PutAsJsonAsync($"/api/MasterData/items/{created!.Id}", new
        {
            code = created.Code, name = "after",
            itemType = ItemType.RawMaterial, uoMId = uom,
            isBatchRequired = false, isMRNRequired = false, isActive = true
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<ItemRow>();
        updated!.Name.Should().Be("after");

        // DB-level assertion: re-fetch goes through a fresh handler and reflects the change.
        var refetch = await client.GetFromJsonAsync<ItemRow>($"/api/MasterData/items/{created.Id}");
        refetch!.Name.Should().Be("after");
    }

    [Fact]
    public async Task Delete_SoftDeletes_RemovesFromList()
    {
        var client = await AuthedAsync();
        var uoms = await client.GetFromJsonAsync<List<UoMRow>>("/api/MasterData/uom");
        var uom = uoms!.First(u => u.IsActive).Id;

        var code = $"DEL-{Guid.NewGuid():N}".Substring(0, 20);
        var create = await client.PostAsJsonAsync("/api/MasterData/items", new
        {
            code, name = "to delete",
            itemType = ItemType.RawMaterial, uoMId = uom,
            isBatchRequired = false, isMRNRequired = false, isActive = true
        });
        var created = await create.Content.ReadFromJsonAsync<ItemRow>();

        var delete = await client.DeleteAsync($"/api/MasterData/items/{created!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Listed items omits soft-deleted
        var list = await client.GetFromJsonAsync<List<ItemRow>>("/api/MasterData/items");
        list!.Should().NotContain(r => r.Id == created.Id);

        // Global query filter `!IsDeleted` in ApplicationDbContext hides the
        // soft-deleted row from every tenant-scoped query, including GetById.
        // Controller surfaces that as 404 (handler returned null).
        var byId = await client.GetAsync($"/api/MasterData/items/{created.Id}");
        byId.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = await AuthedAsync();
        var resp = await client.GetAsync($"/api/MasterData/items/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// P15.1 — partner SKU (legacy ArtKatBrStara) round-trip. Creates an item
    /// with a mixed-case / whitespace-padded partnerSKU and asserts the
    /// handler normalizes to trimmed + upper-cased on write and round-trips
    /// on read. Also verifies the list-search picks it up.
    /// </summary>
    [Fact]
    public async Task Create_WithPartnerSku_NormalizesAndRoundTrips()
    {
        var client = await AuthedAsync();
        var uoms = await client.GetFromJsonAsync<List<UoMRow>>("/api/MasterData/uom");
        var uom = uoms!.First(u => u.IsActive).Id;

        var code = $"P15-1-SKU-{Guid.NewGuid():N}".Substring(0, 20);
        var rawSku = "  abc-XYZ-42  ";
        var expectedSku = "ABC-XYZ-42"; // trimmed + upper-cased
        var create = await client.PostAsJsonAsync("/api/MasterData/items", new
        {
            code,
            name = "partner sku roundtrip",
            itemType = ItemType.RawMaterial,
            uoMId = uom,
            isBatchRequired = false,
            isMRNRequired = false,
            isActive = true,
            partnerSKU = rawSku
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<ItemWithSkuRow>();
        created!.PartnerSKU.Should().Be(expectedSku);

        // Re-fetch hits a fresh handler + fresh EF query → verifies persisted state.
        var refetch = await client.GetFromJsonAsync<ItemWithSkuRow>($"/api/MasterData/items/{created.Id}");
        refetch!.PartnerSKU.Should().Be(expectedSku);

        // Search by partial partner SKU surfaces the item (P15.1 added to GetItemsQuery).
        var list = await client.GetFromJsonAsync<List<ItemWithSkuRow>>(
            $"/api/MasterData/items?search={expectedSku.Substring(0, 6)}");
        list!.Should().Contain(r => r.Id == created.Id);
    }

    /// <summary>
    /// P15.1 — update flow must clear PartnerSKU when operator removes it,
    /// not persist the previous value. Empty string normalizes to null.
    /// </summary>
    [Fact]
    public async Task Update_ClearingPartnerSku_PersistsNull()
    {
        var client = await AuthedAsync();
        var uoms = await client.GetFromJsonAsync<List<UoMRow>>("/api/MasterData/uom");
        var uom = uoms!.First(u => u.IsActive).Id;

        var code = $"P15-1-CLR-{Guid.NewGuid():N}".Substring(0, 20);
        var create = await client.PostAsJsonAsync("/api/MasterData/items", new
        {
            code,
            name = "initial with sku",
            itemType = ItemType.RawMaterial,
            uoMId = uom,
            isBatchRequired = false,
            isMRNRequired = false,
            isActive = true,
            partnerSKU = "INITIAL-SKU"
        });
        var created = await create.Content.ReadFromJsonAsync<ItemWithSkuRow>();
        created!.PartnerSKU.Should().Be("INITIAL-SKU");

        // Clear the SKU on update (empty string → null)
        var update = await client.PutAsJsonAsync($"/api/MasterData/items/{created.Id}", new
        {
            code = created.Code,
            name = created.Name,
            itemType = ItemType.RawMaterial,
            uoMId = uom,
            isBatchRequired = false,
            isMRNRequired = false,
            isActive = true,
            partnerSKU = "   " // whitespace → null
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<ItemWithSkuRow>();
        updated!.PartnerSKU.Should().BeNull();
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record UoMRow(Guid Id, string Code, string Name, bool IsActive);
    private sealed record ItemRow(Guid Id, string Code, string Name, bool IsActive);
    private sealed record ItemWithSkuRow(Guid Id, string Code, string Name, bool IsActive, string? PartnerSKU);
}
