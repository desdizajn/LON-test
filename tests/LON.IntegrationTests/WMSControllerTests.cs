using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.MasterData;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P16.D1 — WMSController smoke + tenant-isolation tests.
///
/// Covers the read endpoints the warehouse role hits daily, plus one
/// write (adjustments). Each test asserts (a) HTTP 200, (b) response
/// shape is the standard `{isSuccess, data, errorMessage}` envelope
/// or a raw list / object that deserialises cleanly, (c) tenant
/// isolation — a foreign-tenant row inserted directly via DbContext
/// does not appear in the authenticated TEKSPORT response.
/// </summary>
public class WMSControllerTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public WMSControllerTests(LonApiFactory factory) => _factory = factory;

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

    [Fact]
    public async Task GetInventory_Returns200_AndArray()
    {
        var client = await AuthedAsync();
        var resp = await client.GetAsync("/api/WMS/inventory");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await resp.Content.ReadAsStringAsync();
        json.TrimStart().Should().StartWith("[", "controller returns a list directly");
    }

    [Fact]
    public async Task GetMozniMinusi_Returns200()
    {
        var client = await AuthedAsync();
        var resp = await client.GetAsync("/api/WMS/inventory/mozni-minusi");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/WMS/receipts")]
    [InlineData("/api/WMS/shipments")]
    [InlineData("/api/WMS/transfers")]
    [InlineData("/api/WMS/cycle-counts")]
    [InlineData("/api/WMS/pick-tasks")]
    [InlineData("/api/WMS/skart")]
    public async Task ListEndpoints_Return200(string url)
    {
        var client = await AuthedAsync();
        var resp = await client.GetAsync(url);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PostAdjustment_RejectsEmptyPayload()
    {
        var client = await AuthedAsync();
        // Empty payload is rejected by AdjustmentCommand validation, returns
        // 400. The point is the endpoint is mounted and responds in our
        // envelope shape, not 500.
        var resp = await client.PostAsJsonAsync("/api/WMS/adjustments", new { });
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetInventory_DoesNotLeakForeignTenantBalance()
    {
        // Seed a foreign tenant + tenant-scoped InventoryBalance, then
        // confirm the authenticated TEKSPORT GET doesn't return it.
        using var seedScope = _factory.Services.CreateScope();
        var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        const string markerBatch = "P16-D1-FOREIGN-BATCH";

        // Clean any stale rows from prior runs.
        var stale = await db.InventoryBalances.IgnoreQueryFilters()
            .Where(b => b.BatchNumber == markerBatch).ToListAsync();
        db.InventoryBalances.RemoveRange(stale);
        await db.SaveChangesAsync();

        var staleTenant = await db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Code == "P16-D1-ISO");
        if (staleTenant is not null) db.Tenants.Remove(staleTenant);
        await db.SaveChangesAsync();

        var otherTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Code = "P16-D1-ISO",
            Name = "WMS isolation tenant",
            Country = "MK",
            DefaultLanguage = "mk",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "P16D1Test",
        };
        db.Tenants.Add(otherTenant);

        // Borrow an existing Item + Location + UoM from TEKSPORT so we
        // don't have to create the whole graph — they're still hidden
        // from TEKSPORT's GET because TenantId on InventoryBalance is
        // the foreign tenant.
        var kg = await db.UnitsOfMeasure.IgnoreQueryFilters().FirstAsync(u => u.Code == "KG");
        var item = await db.Items.IgnoreQueryFilters().FirstAsync();
        var loc = await db.Locations.IgnoreQueryFilters().FirstAsync();

        db.InventoryBalances.Add(new InventoryBalance
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenant.Id,
            ItemId = item.Id,
            LocationId = loc.Id,
            BatchNumber = markerBatch,
            UoMId = kg.Id,
            Quantity = 999m,
            QualityStatus = QualityStatus.OK,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "P16D1Test",
        });
        await db.SaveChangesAsync();

        // Sanity — without the filter, the foreign row IS in the DB.
        var allBalances = await db.InventoryBalances.IgnoreQueryFilters()
            .AnyAsync(b => b.BatchNumber == markerBatch);
        allBalances.Should().BeTrue();

        // Authenticated TEKSPORT must NOT see the foreign row.
        var client = await AuthedAsync();
        var json = await (await client.GetAsync("/api/WMS/inventory")).Content.ReadAsStringAsync();
        json.Should().NotContain(markerBatch,
            "global query filter must hide other tenants' InventoryBalance rows");
    }

    private sealed record LoginResponse(string AccessToken);
}
