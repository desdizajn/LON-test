using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.Production;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P2.4 — MaterialIssue consumes inventory. Proves:
///   * Happy path: Imported balance shrinks, InProduction balance grows, movement/issue recorded.
///   * Over-issue returns 400 (no-negative rule).
///   * Unknown batch/MRN combination returns 400.
///   * FEFO auto-pick chooses earliest ExpiryDate when caller leaves batch/MRN blank.
///   * LON material without batch+MRN is rejected even if a non-LON balance exists.
/// </summary>
public class MaterialIssueTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public MaterialIssueTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Issue_FromImportedBalance_SplitsLonState()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (_, mrn) = await CreateIm4200Declaration(client, 50m);
        var seed = await LoadSeedAsync();

        // Book a receipt so there's an Imported balance to consume.
        var rcpt = await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 50m, uoMId = seed.UomId,
                batchNumber = "BATCH-P24-A", mrn,
                locationId = seed.RcvLocationId, qualityStatus = 1
            } }
        });
        rcpt.EnsureSuccessStatusCode();

        // Arrange ProductionOrder directly in the DB (no CreateProductionOrder endpoint
        // that returns a stable id, plus that handler has a pre-existing Add bug).
        var orderId = await CreateProductionOrderAsync(seed);

        // Act: issue 20 kg with explicit batch+MRN.
        var resp = await client.PostAsJsonAsync($"/api/production/orders/{orderId}/issues", new
        {
            issueDate = DateTime.UtcNow.Date,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 20m, uoMId = seed.UomId,
                batchNumber = "BATCH-P24-A", mrn,
                locationId = seed.RcvLocationId
            } }
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK, because: body);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Happy-path TEKSPORT inflate: 50 declared × 100/(100-5) ≈ 52.6316 physical.
        var imported = await ctx.InventoryBalances.IgnoreQueryFilters()
            .FirstAsync(b => b.BatchNumber == "BATCH-P24-A"
                             && b.LonProcessState == LonProcessState.Imported);
        imported.Quantity.Should().BeApproximately(32.6316m, 0.001m,
            "Imported bucket = 52.6316 physical − 20 issued");

        var inProd = await ctx.InventoryBalances.IgnoreQueryFilters()
            .FirstAsync(b => b.BatchNumber == "BATCH-P24-A"
                             && b.LonProcessState == LonProcessState.InProduction);
        inProd.Quantity.Should().Be(20m, "InProduction bucket = issued qty");
        inProd.MRN.Should().Be(mrn);

        var movement = await ctx.InventoryMovements.IgnoreQueryFilters()
            .FirstAsync(m => m.Type == MovementType.ProductionIssue && m.MRN == mrn);
        movement.Quantity.Should().Be(20m);
        movement.FromLocationId.Should().Be(seed.RcvLocationId);
        movement.ToLocationId.Should().BeNull();

        var issue = await ctx.MaterialIssues.IgnoreQueryFilters()
            .FirstAsync(i => i.ProductionOrderId == orderId);
        issue.BatchNumber.Should().Be("BATCH-P24-A");
        issue.MRN.Should().Be(mrn);
        issue.Quantity.Should().Be(20m);

        var order = await ctx.ProductionOrders.IgnoreQueryFilters().FirstAsync(o => o.Id == orderId);
        order.Status.Should().Be(ProductionOrderStatus.InProgress, "first issue flips Draft → InProgress");
    }

    [Fact]
    public async Task Issue_OverDraw_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (_, mrn) = await CreateIm4200Declaration(client, 10m);
        var seed = await LoadSeedAsync();

        var rcpt = await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 10m, uoMId = seed.UomId,
                batchNumber = "BATCH-P24-OD", mrn,
                locationId = seed.RcvLocationId, qualityStatus = 1
            } }
        });
        rcpt.EnsureSuccessStatusCode();

        var orderId = await CreateProductionOrderAsync(seed);

        var resp = await client.PostAsJsonAsync($"/api/production/orders/{orderId}/issues", new
        {
            issueDate = DateTime.UtcNow.Date,
            lines = new[] { new {
                itemId = seed.ItemId,
                quantity = 999m, // more than receipt inflate (≈10.5)
                uoMId = seed.UomId,
                batchNumber = "BATCH-P24-OD", mrn,
                locationId = seed.RcvLocationId
            } }
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("insufficient inventory");
    }

    [Fact]
    public async Task Issue_UnknownBatchMrn_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var seed = await LoadSeedAsync();
        var orderId = await CreateProductionOrderAsync(seed);

        var resp = await client.PostAsJsonAsync($"/api/production/orders/{orderId}/issues", new
        {
            issueDate = DateTime.UtcNow.Date,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 5m, uoMId = seed.UomId,
                batchNumber = "BATCH-DOES-NOT-EXIST", mrn = "26MKDOESNOTEXIST1",
                locationId = seed.RcvLocationId
            } }
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("no inventory matches");
    }

    [Fact]
    public async Task Issue_WithoutBatchOrMrn_FEFOAutoPicksOldest()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (_, mrnEarly) = await CreateIm4200Declaration(client, 30m);
        var (_, mrnLate) = await CreateIm4200Declaration(client, 30m);
        var seed = await LoadSeedAsync();

        // Two receipts, different expiry. FEFO should pick the earlier one.
        var earlyExpiry = DateTime.UtcNow.Date.AddDays(30);
        var lateExpiry = DateTime.UtcNow.Date.AddDays(180);

        await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 30m, uoMId = seed.UomId,
                batchNumber = "BATCH-FEFO-LATE", mrn = mrnLate,
                locationId = seed.RcvLocationId, qualityStatus = 1,
                expiryDate = lateExpiry
            } }
        });
        await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 30m, uoMId = seed.UomId,
                batchNumber = "BATCH-FEFO-EARLY", mrn = mrnEarly,
                locationId = seed.RcvLocationId, qualityStatus = 1,
                expiryDate = earlyExpiry
            } }
        });

        var orderId = await CreateProductionOrderAsync(seed);

        // Issue 5 without specifying batch/MRN — must land on BATCH-FEFO-EARLY.
        var resp = await client.PostAsJsonAsync($"/api/production/orders/{orderId}/issues", new
        {
            issueDate = DateTime.UtcNow.Date,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 5m, uoMId = seed.UomId
            } }
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK, because: body);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var issue = await ctx.MaterialIssues.IgnoreQueryFilters()
            .FirstAsync(i => i.ProductionOrderId == orderId);
        issue.BatchNumber.Should().Be("BATCH-FEFO-EARLY",
            "FEFO picks the row with the earliest ExpiryDate");
        issue.MRN.Should().Be(mrnEarly);
    }

    [Fact]
    public async Task Issue_LonMaterial_ExplicitNullBatch_Rejected()
    {
        // Edge case: user specifies only a location (no batch/MRN) but the target
        // has LON material. Auto-pick lands on Imported → handler's post-resolve
        // check still demands batch+MRN. Since auto-pick filled them, this scenario
        // actually passes. The real rejection is when the *resolved* balance has no
        // batch+MRN, which can't happen for LON. So we test instead: an exact-match
        // query that lands on an Imported balance with batch=null must fail.
        //
        // Engineer an Imported balance with null batch directly in DB to exercise.
        var client = _factory.CreateClient();
        await Authenticate(client);
        var seed = await LoadSeedAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ctx.InventoryBalances.Add(new LON.Domain.Entities.WMS.InventoryBalance
            {
                Id = Guid.NewGuid(),
                ItemId = seed.ItemId,
                LocationId = seed.RcvLocationId,
                BatchNumber = null,
                MRN = null,
                Quantity = 10m,
                UoMId = seed.UomId,
                QualityStatus = QualityStatus.OK,
                LonProcessState = LonProcessState.Imported
            });
            await ctx.SaveChangesAsync();
        }

        var orderId = await CreateProductionOrderAsync(seed);

        // Location-narrowed query will resolve to the engineered row.
        var resp = await client.PostAsJsonAsync($"/api/production/orders/{orderId}/issues", new
        {
            issueDate = DateTime.UtcNow.Date,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 1m, uoMId = seed.UomId,
                locationId = seed.RcvLocationId
            } }
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("LON material requires");
    }

    // ================================================================
    // Helpers
    // ================================================================

    private async Task<Guid> CreateProductionOrderAsync(SeedIds seed)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenantId = (await ctx.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Code == "TEKSPORT")).Id;
        var order = new ProductionOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"PO-P24-{Guid.NewGuid().ToString()[..8]}",
            TenantId = tenantId,
            ItemId = seed.ItemId,
            OrderQuantity = 100m,
            UoMId = seed.UomId,
            Status = ProductionOrderStatus.Draft,
            PlannedStartDate = DateTime.UtcNow.Date,
            PlannedEndDate = DateTime.UtcNow.Date.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "P24-test"
        };
        ctx.ProductionOrders.Add(order);
        await ctx.SaveChangesAsync();
        return order.Id;
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
            declarationNumber = $"DEC-P24-{Guid.NewGuid():N}"[..14],
            mrn = "",
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procId,
            lonAuthorizationId = authId,
            partnerId,
            totalCustomsValue = 1000m,
            currency = "EUR",
            senderName = "P24 Supplier", senderCountry = "DE", countryOfDispatch = "DE",
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
