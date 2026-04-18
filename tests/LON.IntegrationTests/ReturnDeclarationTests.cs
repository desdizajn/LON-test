using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.Guarantee;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P2.6b — Return declaration reverses a prior EX. Proves:
///   * Partial return: Exported shrinks, Imported restores, DischargedQty drops, re-Debit pro-rata.
///   * Full return after full discharge: MRN flips back to IsActive=true, prior Credit.IsReleased=false.
///   * Over-return (&gt; DischargedQty) → 400.
///   * Unknown source MRN → 400.
/// </summary>
public class ReturnDeclarationTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public ReturnDeclarationTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Return_PartialReverseOfExport_RestoresImportedAndReDebits()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (_, imMrn) = await CreateIm4200Declaration(client, 50m);
        var seed = await LoadSeedAsync();

        // Receive 50 kg raw.
        (await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 50m, uoMId = seed.UomId,
                batchNumber = "RET-RAW", mrn = imMrn,
                locationId = seed.RcvLocationId, qualityStatus = 1
            } }
        })).EnsureSuccessStatusCode();

        // Seed FG inventory to ship out on EX.
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ctx.InventoryBalances.Add(new LON.Domain.Entities.WMS.InventoryBalance
            {
                Id = Guid.NewGuid(),
                ItemId = seed.ItemId,
                LocationId = seed.RcvLocationId,
                BatchNumber = "FG-RET-01",
                Quantity = 30m,
                UoMId = seed.UomId,
                QualityStatus = QualityStatus.OK
            });
            await ctx.SaveChangesAsync();
        }

        var exProcId = await GetProcedureIdAsync("3151");
        // EX qty=5 FG, dischargeQty=20 declared raw.
        (await client.PostAsJsonAsync("/api/customs/declarations/export", new
        {
            declarationNumber = $"EX-RET-{Guid.NewGuid():N}"[..12],
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = exProcId,
            partnerId = seed.PartnerId,
            currency = "EUR", totalCustomsValue = 200m,
            countryOfDestination = "RS",
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 5m, uoMId = seed.UomId,
                customsValue = 200m, countryOfOrigin = "MK",
                calculationMethod = "A", batchNumber = "FG-RET-01",
                locationId = seed.RcvLocationId,
                sourceMRN = imMrn, dischargeQuantity = 20m
            } }
        })).EnsureSuccessStatusCode();

        // Snapshot pre-return state.
        decimal debitAmount, creditAmount, dischargedBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            debitAmount = (await ctx.GuaranteeLedgerEntries.IgnoreQueryFilters()
                .FirstAsync(e => e.MRN == imMrn && e.EntryType == GuaranteeEntryType.Debit)).Amount;
            creditAmount = (await ctx.GuaranteeLedgerEntries.IgnoreQueryFilters()
                .FirstAsync(e => e.MRN == imMrn && e.EntryType == GuaranteeEntryType.Credit)).Amount;
            dischargedBefore = (await ctx.MRNRegistries.IgnoreQueryFilters()
                .FirstAsync(r => r.MRN == imMrn)).DischargedQuantity;
        }
        dischargedBefore.Should().Be(20m);

        // Return qty=3 FG, returnQty=12 declared raw (partial reverse).
        var retProcId = await GetProcedureIdAsync("6121");
        var resp = await client.PostAsJsonAsync("/api/customs/declarations/return", new
        {
            declarationNumber = $"RET-{Guid.NewGuid():N}"[..12],
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = retProcId,
            partnerId = seed.PartnerId,
            currency = "EUR", totalCustomsValue = 150m,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 3m, uoMId = seed.UomId,
                customsValue = 150m, countryOfOrigin = "MK",
                calculationMethod = "A",
                batchNumber = "FG-RET-01",
                locationId = seed.RcvLocationId,
                sourceMRN = imMrn, returnQuantity = 12m,
                returnTo = LonProcessState.Imported
            } }
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK, because: body);

        using var scope2 = _factory.Services.CreateScope();
        var ctx2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var reg = await ctx2.MRNRegistries.IgnoreQueryFilters().FirstAsync(r => r.MRN == imMrn);
        reg.DischargedQuantity.Should().Be(8m, "20 discharged − 12 returned");
        reg.IsActive.Should().BeTrue();

        var exported = await ctx2.InventoryBalances.IgnoreQueryFilters()
            .FirstAsync(b => b.MRN == imMrn && b.LonProcessState == LonProcessState.Exported);
        exported.Quantity.Should().Be(8m, "20 exported − 12 restored");

        // Imported restore: original Imported 52.6316 (inflate) − 20 (EX) = 32.6316;
        // + 12 returned = 44.6316 physical back in Imported.
        var imported = await ctx2.InventoryBalances.IgnoreQueryFilters()
            .FirstAsync(b => b.MRN == imMrn && b.LonProcessState == LonProcessState.Imported);
        imported.Quantity.Should().BeApproximately(44.6316m, 0.001m);

        // FG restored: started 30, EX −5, Return +3 = 28.
        var fg = await ctx2.InventoryBalances.IgnoreQueryFilters()
            .FirstAsync(b => b.BatchNumber == "FG-RET-01" && b.LonProcessState == null);
        fg.Quantity.Should().Be(28m);

        // Re-debit: imDebit × returnQty / MRN.TotalQuantity = debitAmount × 12 / 50.
        var expectedReDebit = Math.Round(debitAmount * 12m / 50m, 2, MidpointRounding.AwayFromZero);
        var reDebitRow = await ctx2.GuaranteeLedgerEntries.IgnoreQueryFilters()
            .Where(e => e.MRN == imMrn && e.EntryType == GuaranteeEntryType.Debit)
            .OrderBy(e => e.EntryDate)
            .ToListAsync();
        reDebitRow.Should().HaveCount(2, "original IM debit + return re-debit");
        reDebitRow.Last().Amount.Should().Be(expectedReDebit);
        reDebitRow.Last().Description.Should().Contain("Return re-debit");
    }

    [Fact]
    public async Task Return_AfterFullDischarge_ReactivatesMrnAndReopensCredit()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (_, imMrn) = await CreateIm4200Declaration(client, 10m);
        var seed = await LoadSeedAsync();

        (await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 10m, uoMId = seed.UomId,
                batchNumber = "RET-FULL", mrn = imMrn,
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
                BatchNumber = "FG-RET-FULL", Quantity = 8m,
                UoMId = seed.UomId, QualityStatus = QualityStatus.OK
            });
            await ctx.SaveChangesAsync();
        }

        var exProcId = await GetProcedureIdAsync("3151");
        (await client.PostAsJsonAsync("/api/customs/declarations/export", new
        {
            declarationNumber = $"EX-FULL-{Guid.NewGuid():N}"[..12],
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = exProcId,
            partnerId = seed.PartnerId,
            currency = "EUR", totalCustomsValue = 300m,
            countryOfDestination = "RS",
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 4m, uoMId = seed.UomId,
                customsValue = 300m, countryOfOrigin = "MK",
                calculationMethod = "A", batchNumber = "FG-RET-FULL",
                sourceMRN = imMrn, dischargeQuantity = 10m // full
            } }
        })).EnsureSuccessStatusCode();

        // Confirm MRN is closed + credit released
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reg = await ctx.MRNRegistries.IgnoreQueryFilters().FirstAsync(r => r.MRN == imMrn);
            reg.IsActive.Should().BeFalse();
            var credit = await ctx.GuaranteeLedgerEntries.IgnoreQueryFilters()
                .FirstAsync(e => e.MRN == imMrn && e.EntryType == GuaranteeEntryType.Credit);
            credit.IsReleased.Should().BeTrue();
        }

        // Return qty=3 declared raw (partial of the 10 discharged).
        var retProcId = await GetProcedureIdAsync("6121");
        var resp = await client.PostAsJsonAsync("/api/customs/declarations/return", new
        {
            declarationNumber = $"RET-FULL-{Guid.NewGuid():N}"[..12],
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = retProcId,
            partnerId = seed.PartnerId,
            currency = "EUR", totalCustomsValue = 100m,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 1m, uoMId = seed.UomId,
                customsValue = 100m, calculationMethod = "A",
                batchNumber = "FG-RET-FULL", locationId = seed.RcvLocationId,
                sourceMRN = imMrn, returnQuantity = 3m,
                returnTo = LonProcessState.Imported
            } }
        });
        resp.EnsureSuccessStatusCode();

        using var scope2 = _factory.Services.CreateScope();
        var ctx2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reg2 = await ctx2.MRNRegistries.IgnoreQueryFilters().FirstAsync(r => r.MRN == imMrn);
        reg2.IsActive.Should().BeTrue("re-activated — outstanding undischarged now 3");
        reg2.DischargedQuantity.Should().Be(7m);

        var priorCredit = await ctx2.GuaranteeLedgerEntries.IgnoreQueryFilters()
            .FirstAsync(e => e.MRN == imMrn && e.EntryType == GuaranteeEntryType.Credit);
        priorCredit.IsReleased.Should().BeFalse("bond is re-committed; closure rolled back");
        priorCredit.ActualReleaseDate.Should().BeNull();
    }

    [Fact]
    public async Task Return_OverDischargedQty_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (_, imMrn) = await CreateIm4200Declaration(client, 20m);
        var seed = await LoadSeedAsync();

        (await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 20m, uoMId = seed.UomId,
                batchNumber = "RET-OD", mrn = imMrn,
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
                BatchNumber = "FG-RET-OD", Quantity = 5m,
                UoMId = seed.UomId, QualityStatus = QualityStatus.OK
            });
            await ctx.SaveChangesAsync();
        }

        var exProcId = await GetProcedureIdAsync("3151");
        (await client.PostAsJsonAsync("/api/customs/declarations/export", new
        {
            declarationNumber = $"EX-OD-{Guid.NewGuid():N}"[..12],
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = exProcId,
            partnerId = seed.PartnerId,
            currency = "EUR", totalCustomsValue = 100m,
            countryOfDestination = "RS",
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 2m, uoMId = seed.UomId,
                customsValue = 100m, calculationMethod = "A",
                batchNumber = "FG-RET-OD",
                sourceMRN = imMrn, dischargeQuantity = 5m
            } }
        })).EnsureSuccessStatusCode();

        var retProcId = await GetProcedureIdAsync("6121");
        var resp = await client.PostAsJsonAsync("/api/customs/declarations/return", new
        {
            declarationNumber = $"RET-OD-{Guid.NewGuid():N}"[..12],
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = retProcId,
            partnerId = seed.PartnerId,
            currency = "EUR", totalCustomsValue = 100m,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 1m, uoMId = seed.UomId,
                customsValue = 100m, calculationMethod = "A",
                batchNumber = "FG-RET-OD", locationId = seed.RcvLocationId,
                sourceMRN = imMrn, returnQuantity = 999m,
                returnTo = LonProcessState.Imported
            } }
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("exceeds previously discharged");
    }

    [Fact]
    public async Task Return_UnknownMRN_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var seed = await LoadSeedAsync();
        var retProcId = await GetProcedureIdAsync("6121");

        var resp = await client.PostAsJsonAsync("/api/customs/declarations/return", new
        {
            declarationNumber = $"RET-UNK-{Guid.NewGuid():N}"[..12],
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = retProcId,
            partnerId = seed.PartnerId,
            currency = "EUR", totalCustomsValue = 10m,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 1m, uoMId = seed.UomId,
                customsValue = 10m, calculationMethod = "A",
                batchNumber = "FG-UNK", locationId = seed.RcvLocationId,
                sourceMRN = "26MKUNKNOWNRET01", returnQuantity = 1m,
                returnTo = LonProcessState.Imported
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
            declarationNumber = $"DEC-P26B-{Guid.NewGuid():N}"[..14],
            mrn = "",
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procId,
            lonAuthorizationId = authId,
            partnerId,
            totalCustomsValue = 1000m,
            currency = "EUR",
            senderName = "P26B Supplier", senderCountry = "DE", countryOfDispatch = "DE",
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
