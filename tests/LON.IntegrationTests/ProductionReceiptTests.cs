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
/// P2.5 — ProductionReceipt flow. Proves:
///   * Happy path: FG InventoryBalance upserted, InventoryMovement(Type=ProductionReceipt) written, PO bookkeeping.
///   * Status flip: ProducedQuantity + ScrapQuantity >= OrderQuantity → Completed + ActualEndDate.
///   * Auto-mode TraceLinks: one link per MaterialIssue on the PO when MaterialConsumption is omitted.
///   * Explicit consumption: WIP balance decrements + TraceLinks carry caller-supplied quantities.
///   * No-over-production: attempting to exceed OrderQuantity returns 400.
/// </summary>
public class ProductionReceiptTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public ProductionReceiptTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PR_HappyPath_BooksFgAndTraceLinksEachIssue()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (_, mrn) = await CreateIm4200Declaration(client, 30m);
        var seed = await LoadSeedAsync();

        // Receive 30 kg LON raw material.
        (await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 30m, uoMId = seed.UomId,
                batchNumber = "PR-RAW", mrn,
                locationId = seed.RcvLocationId, qualityStatus = 1
            } }
        })).EnsureSuccessStatusCode();

        // Create a PO of capacity 25 against the SAME item (no separate FG item in seed).
        var orderId = await CreateProductionOrderAsync(seed, quantity: 25m);

        // Issue 10 kg onto the PO so a MaterialIssue exists.
        (await client.PostAsJsonAsync($"/api/production/orders/{orderId}/issues", new
        {
            issueDate = DateTime.UtcNow.Date,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 10m, uoMId = seed.UomId,
                batchNumber = "PR-RAW", mrn,
                locationId = seed.RcvLocationId
            } }
        })).EnsureSuccessStatusCode();

        // Book PR for 5 kg FG, scrap 1 kg → remains open (5+1 < 25).
        var resp = await client.PostAsJsonAsync($"/api/production/orders/{orderId}/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            itemId = seed.ItemId,
            quantity = 5m,
            scrapQuantity = 1m,
            uoMId = seed.UomId,
            locationId = seed.RcvLocationId,
            batchNumber = "FG-PR-001",
            qualityStatus = 1
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK, because: body);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var fg = await ctx.InventoryBalances.IgnoreQueryFilters()
            .FirstAsync(b => b.BatchNumber == "FG-PR-001");
        fg.Quantity.Should().Be(5m);
        fg.LonProcessState.Should().BeNull("FG balance does not carry LON state — TraceLink holds lineage");
        fg.MRN.Should().BeNull();

        var movement = await ctx.InventoryMovements.IgnoreQueryFilters()
            .FirstAsync(m => m.Type == MovementType.ProductionReceipt && m.BatchNumber == "FG-PR-001");
        movement.ToLocationId.Should().Be(seed.RcvLocationId);
        movement.FromLocationId.Should().BeNull();
        movement.Quantity.Should().Be(5m);

        var pr = await ctx.ProductionReceipts.IgnoreQueryFilters()
            .FirstAsync(r => r.BatchNumber == "FG-PR-001");
        pr.Quantity.Should().Be(5m);
        pr.ScrapQuantity.Should().Be(1m);

        var order = await ctx.ProductionOrders.IgnoreQueryFilters().FirstAsync(o => o.Id == orderId);
        order.ProducedQuantity.Should().Be(5m);
        order.ScrapQuantity.Should().Be(1m);
        order.Status.Should().Be(ProductionOrderStatus.InProgress, "still 19 kg left on the PO");
        order.ActualStartDate.Should().NotBeNull("MaterialIssue already opened the PO");

        var traceLinks = await ctx.TraceLinks.IgnoreQueryFilters()
            .Where(tl => tl.TargetId == pr.Id)
            .ToListAsync();
        traceLinks.Should().HaveCount(1, "auto-mode links every MaterialIssue on the PO (one issue here)");
        var link = traceLinks.Single();
        link.SourceType.Should().Be("MaterialIssue");
        link.SourceBatchNumber.Should().Be("PR-RAW");
        link.SourceMRN.Should().Be(mrn);
        link.TargetBatchNumber.Should().Be("FG-PR-001");
        link.Quantity.Should().Be(10m, "auto-mode echoes the full MaterialIssue qty");
    }

    [Fact]
    public async Task PR_FillingOrderQuantity_CompletesOrder()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (_, mrn) = await CreateIm4200Declaration(client, 20m);
        var seed = await LoadSeedAsync();

        (await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 20m, uoMId = seed.UomId,
                batchNumber = "PR-RAW-COMP", mrn,
                locationId = seed.RcvLocationId, qualityStatus = 1
            } }
        })).EnsureSuccessStatusCode();

        var orderId = await CreateProductionOrderAsync(seed, quantity: 10m);

        (await client.PostAsJsonAsync($"/api/production/orders/{orderId}/issues", new
        {
            issueDate = DateTime.UtcNow.Date,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 10m, uoMId = seed.UomId,
                batchNumber = "PR-RAW-COMP", mrn,
                locationId = seed.RcvLocationId
            } }
        })).EnsureSuccessStatusCode();

        // PR that fills PO exactly: 8 produced + 2 scrap = 10 ordered.
        var resp = await client.PostAsJsonAsync($"/api/production/orders/{orderId}/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            itemId = seed.ItemId,
            quantity = 8m,
            scrapQuantity = 2m,
            uoMId = seed.UomId,
            locationId = seed.RcvLocationId,
            batchNumber = "FG-PR-COMP",
            qualityStatus = 1
        });
        resp.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var order = await ctx.ProductionOrders.IgnoreQueryFilters().FirstAsync(o => o.Id == orderId);
        order.Status.Should().Be(ProductionOrderStatus.Completed);
        order.ActualEndDate.Should().NotBeNull();
        order.ProducedQuantity.Should().Be(8m);
        order.ScrapQuantity.Should().Be(2m);
    }

    [Fact]
    public async Task PR_OverProduction_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var seed = await LoadSeedAsync();
        var orderId = await CreateProductionOrderAsync(seed, quantity: 5m);

        var resp = await client.PostAsJsonAsync($"/api/production/orders/{orderId}/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            itemId = seed.ItemId,
            quantity = 99m,
            uoMId = seed.UomId,
            locationId = seed.RcvLocationId,
            batchNumber = "FG-OVER",
            qualityStatus = 1
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("exceed ordered quantity");
    }

    [Fact]
    public async Task PR_ExplicitConsumption_DecrementsWipAndWeightsLinks()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (_, mrn) = await CreateIm4200Declaration(client, 40m);
        var seed = await LoadSeedAsync();

        (await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            warehouseId = seed.WarehouseId,
            partnerId = seed.PartnerId,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 40m, uoMId = seed.UomId,
                batchNumber = "PR-RAW-EXP", mrn,
                locationId = seed.RcvLocationId, qualityStatus = 1
            } }
        })).EnsureSuccessStatusCode();

        var orderId = await CreateProductionOrderAsync(seed, quantity: 30m);

        // Issue 15; WIP balance InProduction=15.
        var issueResp = await client.PostAsJsonAsync($"/api/production/orders/{orderId}/issues", new
        {
            issueDate = DateTime.UtcNow.Date,
            lines = new[] { new {
                itemId = seed.ItemId, quantity = 15m, uoMId = seed.UomId,
                batchNumber = "PR-RAW-EXP", mrn,
                locationId = seed.RcvLocationId
            } }
        });
        issueResp.EnsureSuccessStatusCode();

        Guid issueId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            issueId = (await ctx.MaterialIssues.IgnoreQueryFilters()
                .FirstAsync(mi => mi.ProductionOrderId == orderId && mi.BatchNumber == "PR-RAW-EXP")).Id;
        }

        // PR that consumes 4 kg of WIP explicitly.
        var resp = await client.PostAsJsonAsync($"/api/production/orders/{orderId}/receipts", new
        {
            receiptDate = DateTime.UtcNow.Date,
            itemId = seed.ItemId,
            quantity = 4m,
            uoMId = seed.UomId,
            locationId = seed.RcvLocationId,
            batchNumber = "FG-PR-EXP",
            qualityStatus = 1,
            materialConsumption = new[] {
                new { materialIssueId = issueId, quantity = 4m }
            }
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK, because: body);

        using var scope2 = _factory.Services.CreateScope();
        var ctx2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var wip = await ctx2.InventoryBalances.IgnoreQueryFilters()
            .FirstAsync(b => b.BatchNumber == "PR-RAW-EXP"
                             && b.LonProcessState == LonProcessState.InProduction);
        wip.Quantity.Should().Be(11m, "WIP shrinks from 15 by 4 consumed");

        var link = await ctx2.TraceLinks.IgnoreQueryFilters()
            .FirstAsync(tl => tl.TargetBatchNumber == "FG-PR-EXP");
        link.SourceId.Should().Be(issueId);
        link.Quantity.Should().Be(4m, "explicit mode uses caller-supplied consumption qty");
    }

    // ================================================================
    // Helpers
    // ================================================================

    private async Task<Guid> CreateProductionOrderAsync(SeedIds seed, decimal quantity)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenantId = (await ctx.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Code == "TEKSPORT")).Id;
        var order = new ProductionOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"PO-P25-{Guid.NewGuid().ToString()[..8]}",
            TenantId = tenantId,
            ItemId = seed.ItemId,
            OrderQuantity = quantity,
            UoMId = seed.UomId,
            Status = ProductionOrderStatus.Draft,
            PlannedStartDate = DateTime.UtcNow.Date,
            PlannedEndDate = DateTime.UtcNow.Date.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "P25-test"
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
            declarationNumber = $"DEC-P25-{Guid.NewGuid():N}"[..14],
            mrn = "",
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procId,
            lonAuthorizationId = authId,
            partnerId,
            totalCustomsValue = 1000m,
            currency = "EUR",
            senderName = "P25 Supplier", senderCountry = "DE", countryOfDispatch = "DE",
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
