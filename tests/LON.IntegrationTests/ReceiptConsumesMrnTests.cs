using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P2.3 — Receipt-consumes-MRN flow. Proves:
///   * MRN must exist, be active, unexpired, and have enough remaining qty.
///   * MRNRegistry.UsedQuantity is incremented in the SAME transaction as the receipt.
///   * Inventory balance carries the declared MRN + LonProcessState=Imported.
///   * TEKSPORT tenants get legacy inflate-for-waste on the balance (not the receipt line).
///   * Non-MRN path is unchanged.
/// </summary>
public class ReceiptConsumesMrnTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public ReceiptConsumesMrnTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Receipt_WithValidMRN_IncrementsRegistryAndBooksInflatedBalance()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        // Arrange: create an IM 4200 declaration so a fresh MRN lands in the
        // registry with TotalQuantity = 100 and waste% = 5 (seeded).
        var (declId, mrn) = await CreateIm4200Declaration(client, quantity: 100m);

        var seed = await LoadSeedAsync();

        // Act: receipt declares 80 kg against that MRN.
        var resp = await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[]
            {
                new
                {
                    itemId = seed.ItemId,
                    quantity = 80m,
                    uoMId = seed.UomId,
                    batchNumber = "BATCH-P23-001",
                    mrn,
                    locationId = seed.RcvLocationId,
                    qualityStatus = 1
                }
            }
        });
        resp.EnsureSuccessStatusCode();

        // Assert: MRN registry UsedQuantity == 80; InventoryBalance has the
        // INFLATED quantity (100 × 80 / (100 - 5) ≈ 84.2105) for TEKSPORT.
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var reg = await ctx.MRNRegistries.IgnoreQueryFilters().FirstAsync(r => r.MRN == mrn);
        reg.UsedQuantity.Should().Be(80m, "declared qty must be what counts against the bond");
        reg.TotalQuantity.Should().Be(100m);
        reg.IsActive.Should().BeTrue("registry still has 20 units remaining");

        var balance = await ctx.InventoryBalances.IgnoreQueryFilters()
            .FirstAsync(b => b.MRN == mrn && b.BatchNumber == "BATCH-P23-001");
        balance.Quantity.Should().BeApproximately(84.2105m, 0.001m,
            "TEKSPORT inflate-for-waste: 80 × 100 / (100 − 5%)");
        balance.LonProcessState.Should().Be(LonProcessState.Imported,
            "4200 procedure puts the parcel at Proces=1 Imported");

        // Movement also inflated.
        var movement = await ctx.InventoryMovements.IgnoreQueryFilters()
            .FirstAsync(m => m.MRN == mrn && m.ReferenceNumber!.StartsWith("RCP-"));
        movement.Quantity.Should().BeApproximately(84.2105m, 0.001m);
    }

    [Fact]
    public async Task Receipt_WithUnknownMRN_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var seed = await LoadSeedAsync();

        var resp = await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[]
            {
                new
                {
                    itemId = seed.ItemId, quantity = 10m, uoMId = seed.UomId,
                    batchNumber = "B-UNK", mrn = "26MKUNKNOWNXXXA1",
                    locationId = seed.RcvLocationId, qualityStatus = 1
                }
            }
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("not registered");
    }

    [Fact]
    public async Task Receipt_OverdrawingMRN_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var (_, mrn) = await CreateIm4200Declaration(client, quantity: 50m);
        var seed = await LoadSeedAsync();

        // Consume 40 first — OK.
        var first = await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 40m, uoMId = seed.UomId,
                batchNumber = "B-OD-1", mrn,
                locationId = seed.RcvLocationId, qualityStatus = 1
            } }
        });
        first.EnsureSuccessStatusCode();

        // Attempt 20 more — would exceed 50.
        var second = await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 20m, uoMId = seed.UomId,
                batchNumber = "B-OD-2", mrn,
                locationId = seed.RcvLocationId, qualityStatus = 1
            } }
        });
        var body = await second.Content.ReadAsStringAsync();
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("overdraw");
    }

    [Fact]
    public async Task Receipt_ExpiredMRN_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var (declId, mrn) = await CreateIm4200Declaration(client, quantity: 50m);

        // Force registry expiry to yesterday.
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reg = await ctx.MRNRegistries.IgnoreQueryFilters().FirstAsync(r => r.MRN == mrn);
            reg.ExpiryDate = DateTime.UtcNow.Date.AddDays(-1);
            await ctx.SaveChangesAsync();
        }

        var seed = await LoadSeedAsync();
        var resp = await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 10m, uoMId = seed.UomId,
                batchNumber = "B-EXP", mrn,
                locationId = seed.RcvLocationId, qualityStatus = 1
            } }
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("expired");
    }

    [Fact]
    public async Task Receipt_WithoutMRN_UnchangedLegacyBehavior()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var seed = await LoadSeedAsync();

        var resp = await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 25m, uoMId = seed.UomId,
                batchNumber = "B-NOMRN",
                locationId = seed.RcvLocationId, qualityStatus = 1
                // no mrn
            } }
        });
        resp.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var balance = await ctx.InventoryBalances.IgnoreQueryFilters()
            .FirstAsync(b => b.BatchNumber == "B-NOMRN");
        balance.Quantity.Should().Be(25m, "no MRN → no inflation, quantity equals declared");
        balance.LonProcessState.Should().BeNull("no MRN means not part of LON suspension chain");
    }

    [Fact]
    public async Task Receipt_FullyConsumingMRN_DeactivatesRegistry()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var (_, mrn) = await CreateIm4200Declaration(client, quantity: 30m);
        var seed = await LoadSeedAsync();

        // Receipt consumes the full declared qty.
        var resp = await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 30m, uoMId = seed.UomId,
                batchNumber = "B-FULL", mrn,
                locationId = seed.RcvLocationId, qualityStatus = 1
            } }
        });
        resp.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reg = await ctx.MRNRegistries.IgnoreQueryFilters().FirstAsync(r => r.MRN == mrn);
        reg.UsedQuantity.Should().Be(30m);
        reg.IsActive.Should().BeFalse("MRN that hits TotalQuantity is auto-deactivated");
    }

    // ================================================================
    // Helpers
    // ================================================================

    private async Task<(Guid DeclarationId, string Mrn)> CreateIm4200Declaration(HttpClient client, decimal quantity)
    {
        Guid procId, authId, itemId, uomId, partnerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            procId = (await ctx.CustomsProcedures.FirstAsync(p => p.Code == "4200")).Id;
            authId = (await ctx.LONAuthorizations.IgnoreQueryFilters()
                .FirstAsync(a => a.AuthorizationNumber == "26/TEKSPORT/0001")).Id;
            itemId = (await ctx.Items.FirstAsync()).Id;
            uomId = (await ctx.UnitsOfMeasure.FirstAsync(u => u.Code == "KG")).Id;
            partnerId = (await ctx.Partners.OrderBy(p => p.Code).FirstAsync()).Id;
        }

        var resp = await client.PostAsJsonAsync("/api/customs/declarations", new
        {
            declarationNumber = $"DEC-P23-{Guid.NewGuid():N}"[..14],
            mrn = "",
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procId,
            lonAuthorizationId = authId,
            partnerId,
            totalCustomsValue = 1000m,
            currency = "EUR",
            senderName = "P23 Supplier", senderCountry = "DE", countryOfDispatch = "DE",
            lines = new[] { new {
                itemId, tariffCode = "2905399500",
                quantity, uoMId = uomId,
                customsValue = 1000m, countryOfOrigin = "DE",
                dutyRate = 5m, vatRate = 18m,
                netWeight = quantity, grossWeight = quantity + 2m,
                calculationMethod = "A"
            } }
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<CustomsResult>();
        var declId = body!.Data;

        using var scope2 = _factory.Services.CreateScope();
        var ctx2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var decl = await ctx2.CustomsDeclarations.IgnoreQueryFilters().FirstAsync(d => d.Id == declId);
        return (declId, decl.MRN);
    }

    private async Task<SeedIds> LoadSeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wh = await ctx.Warehouses.FirstAsync();
        var rcv = await ctx.Locations.FirstAsync(l => l.Code.StartsWith("RCV") && l.WarehouseId == wh.Id);
        return new SeedIds
        {
            WarehouseId = wh.Id,
            RcvLocationId = rcv.Id,
            ItemId = (await ctx.Items.FirstAsync()).Id,
            UomId = (await ctx.UnitsOfMeasure.FirstAsync(u => u.Code == "KG")).Id,
            PartnerId = (await ctx.Partners.OrderBy(p => p.Code).FirstAsync()).Id
        };
    }

    private async Task Authenticate(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private sealed class SeedIds
    {
        public Guid WarehouseId;
        public Guid RcvLocationId;
        public Guid ItemId;
        public Guid UomId;
        public Guid PartnerId;
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record CustomsResult(bool IsSuccess, Guid Data, string? ErrorMessage);
}
