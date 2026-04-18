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
/// P2.6c — Waste declaration. Proves:
///   * Happy path: Imported balance shrinks, Waste sibling appears, Adjustment movement written.
///   * Multi-source drain consolidates into a single Waste row (DbSet.Local probe).
///   * Over-waste (qty &gt; available LON-state inventory) → 400.
///   * Unknown MRN → 400.
///   * Missing reason → 400 (audit requirement).
///   * No guarantee-ledger impact — waste is declared-qty-neutral in v1.
/// </summary>
public class WasteDeclarationTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public WasteDeclarationTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Waste_WithValidReason_TransitionsImportedToWaste()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (_, mrn) = await CreateIm4200Declaration(client, 20m);
        var seed = await LoadSeedAsync();

        // Receive 20 declared kg → Imported ≈ 21.0526 physical (TEKSPORT 5% inflate).
        (await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 20m, uoMId = seed.UomId,
                batchNumber = "WST-RAW", mrn,
                locationId = seed.RcvLocationId, qualityStatus = 1
            } }
        })).EnsureSuccessStatusCode();

        // Capture debit-before so we can assert no guarantee movement afterwards.
        decimal netBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            netBefore = await ctx.GuaranteeLedgerEntries.IgnoreQueryFilters()
                .Where(e => e.MRN == mrn && !e.IsDeleted)
                .SumAsync(e => e.EntryType == LON.Domain.Enums.GuaranteeEntryType.Debit ? e.Amount : -e.Amount);
        }

        var resp = await client.PostAsJsonAsync("/api/customs/declarations/waste", new
        {
            wasteDate = DateTime.UtcNow.Date,
            mrn,
            quantity = 1m,
            reason = "Spillage during transfer",
            itemId = seed.ItemId,
            batchNumber = "WST-RAW"
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK, because: body);

        using var scope2 = _factory.Services.CreateScope();
        var ctx2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var imported = await ctx2.InventoryBalances.IgnoreQueryFilters()
            .FirstAsync(b => b.MRN == mrn && b.LonProcessState == LonProcessState.Imported);
        imported.Quantity.Should().BeApproximately(20.0526m, 0.001m, "21.0526 inflated minus 1 wasted");

        var waste = await ctx2.InventoryBalances.IgnoreQueryFilters()
            .FirstAsync(b => b.MRN == mrn && b.LonProcessState == LonProcessState.Waste);
        waste.Quantity.Should().Be(1m);
        waste.BatchNumber.Should().Be("WST-RAW");

        var movement = await ctx2.InventoryMovements.IgnoreQueryFilters()
            .FirstAsync(m => m.MRN == mrn && m.Type == MovementType.Adjustment);
        movement.MovementNumber.Should().StartWith("WST-");
        movement.FromLocationId.Should().Be(seed.RcvLocationId);
        movement.ToLocationId.Should().BeNull();
        movement.Quantity.Should().Be(1m);
        movement.Notes.Should().Contain("Spillage during transfer");

        var netAfter = await ctx2.GuaranteeLedgerEntries.IgnoreQueryFilters()
            .Where(e => e.MRN == mrn && !e.IsDeleted)
            .SumAsync(e => e.EntryType == LON.Domain.Enums.GuaranteeEntryType.Debit ? e.Amount : -e.Amount);
        netAfter.Should().Be(netBefore, "waste does not move the guarantee ledger in v1");
    }

    [Fact]
    public async Task Waste_DrainsImportedThenInProduction_ConsolidatesIntoSingleWasteRow()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (_, mrn) = await CreateIm4200Declaration(client, 10m);
        var seed = await LoadSeedAsync();

        (await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 10m, uoMId = seed.UomId,
                batchNumber = "WST-DUAL", mrn,
                locationId = seed.RcvLocationId, qualityStatus = 1
            } }
        })).EnsureSuccessStatusCode();

        // Engineer a split: a MaterialIssue to create an InProduction sibling
        // alongside the Imported residual. Use the direct DbContext path since
        // MaterialIssue needs a PO fixture.
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var imported = await ctx.InventoryBalances.IgnoreQueryFilters()
                .FirstAsync(b => b.MRN == mrn && b.LonProcessState == LonProcessState.Imported);
            // Move 2 units Imported -> InProduction directly.
            imported.SubtractQuantity(2m);
            ctx.InventoryBalances.Add(new LON.Domain.Entities.WMS.InventoryBalance
            {
                Id = Guid.NewGuid(),
                ItemId = imported.ItemId,
                LocationId = imported.LocationId,
                BatchNumber = imported.BatchNumber,
                MRN = imported.MRN,
                Quantity = 2m,
                UoMId = imported.UoMId,
                QualityStatus = imported.QualityStatus,
                LonProcessState = LonProcessState.InProduction
            });
            await ctx.SaveChangesAsync();
        }

        // Waste 9 — must drain 8.5 Imported + the 0.5 spill over from InProduction.
        // Handler walks Imported-first; here Imported has 8.5263 (10.5263-2) and
        // we ask for 9 → 8.5263 from Imported + 0.4737 from InProduction.
        var resp = await client.PostAsJsonAsync("/api/customs/declarations/waste", new
        {
            wasteDate = DateTime.UtcNow.Date,
            mrn,
            quantity = 9m,
            reason = "QA rejection",
            itemId = seed.ItemId,
            batchNumber = "WST-DUAL"
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK, because: body);

        using var scope2 = _factory.Services.CreateScope();
        var ctx2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wasteRows = await ctx2.InventoryBalances.IgnoreQueryFilters()
            .Where(b => b.MRN == mrn && b.LonProcessState == LonProcessState.Waste)
            .ToListAsync();
        wasteRows.Should().HaveCount(1, "DbSet.Local probe consolidates within a single SaveChanges");
        wasteRows[0].Quantity.Should().Be(9m);
    }

    [Fact]
    public async Task Waste_OverAvailable_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (_, mrn) = await CreateIm4200Declaration(client, 5m);
        var seed = await LoadSeedAsync();

        (await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 5m, uoMId = seed.UomId,
                batchNumber = "WST-OD", mrn,
                locationId = seed.RcvLocationId, qualityStatus = 1
            } }
        })).EnsureSuccessStatusCode();

        var resp = await client.PostAsJsonAsync("/api/customs/declarations/waste", new
        {
            wasteDate = DateTime.UtcNow.Date,
            mrn,
            quantity = 999m,
            reason = "Unit test over-waste",
            batchNumber = "WST-OD"
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("Insufficient LON inventory");
    }

    [Fact]
    public async Task Waste_UnknownMRN_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var resp = await client.PostAsJsonAsync("/api/customs/declarations/waste", new
        {
            wasteDate = DateTime.UtcNow.Date,
            mrn = "26MKDOESNOTEXIST01",
            quantity = 1m,
            reason = "Unit test"
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("not registered");
    }

    [Fact]
    public async Task Waste_MissingReason_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (_, mrn) = await CreateIm4200Declaration(client, 5m);

        var resp = await client.PostAsJsonAsync("/api/customs/declarations/waste", new
        {
            wasteDate = DateTime.UtcNow.Date,
            mrn,
            quantity = 1m,
            reason = ""  // empty
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("Reason is required");
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
            declarationNumber = $"DEC-P26C-{Guid.NewGuid():N}"[..14],
            mrn = "",
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procId,
            lonAuthorizationId = authId,
            partnerId,
            totalCustomsValue = 1000m,
            currency = "EUR",
            senderName = "P26C Supplier", senderCountry = "DE", countryOfDispatch = "DE",
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
