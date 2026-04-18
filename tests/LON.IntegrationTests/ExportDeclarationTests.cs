using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.Guarantee;
using LON.Domain.Entities.Production;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P2.6a — EX declaration + guarantee credit. Proves:
///   * Happy path: FG decrement, Imported/InProduction → Exported, registry DischargedQuantity bumps, guarantee credit written.
///   * Full discharge settles the guarantee ledger to zero for that MRN + marks registry IsActive=false.
///   * Over-discharge (dischargeQty &gt; MRN.UsedQuantity - Discharged) → 400.
///   * Unknown source MRN → 400.
/// </summary>
public class ExportDeclarationTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public ExportDeclarationTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task EX_PartialDischarge_UpdatesStateAndCreditsPortion()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (_, imMrn) = await CreateIm4200Declaration(client, 50m);
        var seed = await LoadSeedAsync();
        var exProcId = await GetProcedureIdAsync("3151");

        // Receive 50 kg raw material against the IM MRN → MRNRegistry.Used=50.
        (await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 50m, uoMId = seed.UomId,
                batchNumber = "EX-RAW", mrn = imMrn,
                locationId = seed.RcvLocationId, qualityStatus = 1
            } }
        })).EnsureSuccessStatusCode();

        // Book a FG batch directly via ProductionReceipt-less shortcut: we don't
        // need the full PO flow for the EX happy path — a free-standing FG
        // InventoryBalance is enough. Insert directly to keep the test tight.
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ctx.InventoryBalances.Add(new LON.Domain.Entities.WMS.InventoryBalance
            {
                Id = Guid.NewGuid(),
                ItemId = seed.ItemId,
                LocationId = seed.RcvLocationId,
                BatchNumber = "FG-EX-01",
                MRN = null,
                Quantity = 30m,
                UoMId = seed.UomId,
                QualityStatus = QualityStatus.OK
            });
            await ctx.SaveChangesAsync();
        }

        // Act: EX discharges 10 declared units against the IM MRN, ships 5 kg FG.
        var resp = await client.PostAsJsonAsync("/api/customs/declarations/export", new
        {
            declarationNumber = $"EX-{Guid.NewGuid():N}"[..12],
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = exProcId,
            partnerId = seed.PartnerId,
            currency = "EUR",
            totalCustomsValue = 200m,
            countryOfDestination = "RS",
            lines = new[] { new {
                itemId = seed.ItemId, tariffCode = "2905399500",
                quantity = 5m, uoMId = seed.UomId,
                customsValue = 200m, countryOfOrigin = "MK",
                netWeight = 5m, grossWeight = 5.2m,
                calculationMethod = "A",
                batchNumber = "FG-EX-01",
                locationId = seed.RcvLocationId,
                sourceMRN = imMrn,
                dischargeQuantity = 10m
            } }
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK, because: body);

        using var scope2 = _factory.Services.CreateScope();
        var ctx2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var reg = await ctx2.MRNRegistries.IgnoreQueryFilters().FirstAsync(r => r.MRN == imMrn);
        reg.DischargedQuantity.Should().Be(10m);
        reg.UsedQuantity.Should().Be(50m);
        reg.IsActive.Should().BeTrue("still 40 declared units under bond");

        var importedShrunk = await ctx2.InventoryBalances.IgnoreQueryFilters()
            .FirstAsync(b => b.MRN == imMrn && b.LonProcessState == LonProcessState.Imported);
        // TEKSPORT waste inflate: 50 × 100/(100-5) = 52.6316. After 10 discharged, 42.6316.
        importedShrunk.Quantity.Should().BeApproximately(42.6316m, 0.001m);

        var exported = await ctx2.InventoryBalances.IgnoreQueryFilters()
            .FirstAsync(b => b.MRN == imMrn && b.LonProcessState == LonProcessState.Exported);
        exported.Quantity.Should().Be(10m);

        var fg = await ctx2.InventoryBalances.IgnoreQueryFilters()
            .FirstAsync(b => b.BatchNumber == "FG-EX-01");
        fg.Quantity.Should().Be(25m, "FG dropped from 30 to 25 after 5 kg shipped");

        // Guarantee credit: debit was 50% × (duty+VAT) on the IM; pro-rata credit = debit × 10/50.
        var credits = await ctx2.GuaranteeLedgerEntries.IgnoreQueryFilters()
            .Where(e => e.MRN == imMrn && e.EntryType == GuaranteeEntryType.Credit)
            .ToListAsync();
        credits.Should().HaveCount(1);
        credits[0].IsReleased.Should().BeFalse("partial discharge doesn't close the bond");
    }

    [Fact]
    public async Task EX_FullDischarge_SettlesLedgerAndDeactivatesMrn()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (_, imMrn) = await CreateIm4200Declaration(client, 20m);
        var seed = await LoadSeedAsync();
        var exProcId = await GetProcedureIdAsync("3151");

        (await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 20m, uoMId = seed.UomId,
                batchNumber = "EX-RAW-FULL", mrn = imMrn,
                locationId = seed.RcvLocationId, qualityStatus = 1
            } }
        })).EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ctx.InventoryBalances.Add(new LON.Domain.Entities.WMS.InventoryBalance
            {
                Id = Guid.NewGuid(),
                ItemId = seed.ItemId,
                LocationId = seed.RcvLocationId,
                BatchNumber = "FG-EX-FULL",
                Quantity = 15m,
                UoMId = seed.UomId,
                QualityStatus = QualityStatus.OK
            });
            await ctx.SaveChangesAsync();
        }

        var resp = await client.PostAsJsonAsync("/api/customs/declarations/export", new
        {
            declarationNumber = $"EX-FULL-{Guid.NewGuid():N}"[..12],
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = exProcId,
            partnerId = seed.PartnerId,
            currency = "EUR",
            totalCustomsValue = 1000m,
            countryOfDestination = "RS",
            lines = new[] { new {
                itemId = seed.ItemId, tariffCode = "2905399500",
                quantity = 10m, uoMId = seed.UomId,
                customsValue = 1000m, countryOfOrigin = "MK",
                netWeight = 10m, grossWeight = 10.5m,
                calculationMethod = "A",
                batchNumber = "FG-EX-FULL",
                sourceMRN = imMrn,
                dischargeQuantity = 20m  // full TotalQuantity of IM MRN
            } }
        });
        resp.EnsureSuccessStatusCode();

        using var scope2 = _factory.Services.CreateScope();
        var ctx2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reg = await ctx2.MRNRegistries.IgnoreQueryFilters().FirstAsync(r => r.MRN == imMrn);
        reg.DischargedQuantity.Should().Be(20m);
        reg.IsActive.Should().BeFalse("fully discharged MRN is closed");

        // Net guarantee movement for this MRN = Debit + Credits should be zero.
        var net = await ctx2.GuaranteeLedgerEntries.IgnoreQueryFilters()
            .Where(e => e.MRN == imMrn && !e.IsDeleted)
            .SumAsync(e => e.EntryType == GuaranteeEntryType.Debit ? e.Amount : -e.Amount);
        net.Should().Be(0m, "full discharge settles the bond to zero");

        var credit = await ctx2.GuaranteeLedgerEntries.IgnoreQueryFilters()
            .FirstAsync(e => e.MRN == imMrn && e.EntryType == GuaranteeEntryType.Credit);
        credit.IsReleased.Should().BeTrue();
        credit.ActualReleaseDate.Should().NotBeNull();
    }

    [Fact]
    public async Task EX_OverDischarge_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (_, imMrn) = await CreateIm4200Declaration(client, 10m);
        var seed = await LoadSeedAsync();
        var exProcId = await GetProcedureIdAsync("3151");

        (await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 10m, uoMId = seed.UomId,
                batchNumber = "EX-RAW-OD", mrn = imMrn,
                locationId = seed.RcvLocationId, qualityStatus = 1
            } }
        })).EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ctx.InventoryBalances.Add(new LON.Domain.Entities.WMS.InventoryBalance
            {
                Id = Guid.NewGuid(),
                ItemId = seed.ItemId, LocationId = seed.RcvLocationId,
                BatchNumber = "FG-OD", Quantity = 5m,
                UoMId = seed.UomId, QualityStatus = QualityStatus.OK
            });
            await ctx.SaveChangesAsync();
        }

        var resp = await client.PostAsJsonAsync("/api/customs/declarations/export", new
        {
            declarationNumber = $"EX-OD-{Guid.NewGuid():N}"[..12],
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = exProcId,
            partnerId = seed.PartnerId,
            currency = "EUR",
            totalCustomsValue = 100m,
            countryOfDestination = "RS",
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 2m, uoMId = seed.UomId,
                customsValue = 100m, countryOfOrigin = "MK",
                calculationMethod = "A", batchNumber = "FG-OD",
                sourceMRN = imMrn, dischargeQuantity = 999m
            } }
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("exceeds outstanding undischarged");
    }

    [Fact]
    public async Task EX_UnknownMRN_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var seed = await LoadSeedAsync();
        var exProcId = await GetProcedureIdAsync("3151");

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ctx.InventoryBalances.Add(new LON.Domain.Entities.WMS.InventoryBalance
            {
                Id = Guid.NewGuid(),
                ItemId = seed.ItemId, LocationId = seed.RcvLocationId,
                BatchNumber = "FG-UNK", Quantity = 5m,
                UoMId = seed.UomId, QualityStatus = QualityStatus.OK
            });
            await ctx.SaveChangesAsync();
        }

        var resp = await client.PostAsJsonAsync("/api/customs/declarations/export", new
        {
            declarationNumber = $"EX-UNK-{Guid.NewGuid():N}"[..12],
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = exProcId,
            partnerId = seed.PartnerId,
            currency = "EUR",
            totalCustomsValue = 100m,
            countryOfDestination = "RS",
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 1m, uoMId = seed.UomId,
                customsValue = 100m, countryOfOrigin = "MK",
                calculationMethod = "A", batchNumber = "FG-UNK",
                sourceMRN = "26MKDOESNOTEXIST01", dischargeQuantity = 1m
            } }
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("not registered");
    }

    // ================================================================
    // Helpers
    // ================================================================

    private async Task<Guid> GetProcedureIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await ctx.CustomsProcedures.FirstAsync(p => p.Code == code)).Id;
    }

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
            declarationNumber = $"DEC-P26-{Guid.NewGuid():N}"[..14],
            mrn = "",
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procId,
            lonAuthorizationId = authId,
            partnerId,
            totalCustomsValue = 1000m,
            currency = "EUR",
            senderName = "P26 Supplier", senderCountry = "DE", countryOfDispatch = "DE",
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
