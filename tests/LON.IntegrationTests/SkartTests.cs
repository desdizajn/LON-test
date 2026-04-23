using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P15.3 — Skart (defective-on-intake) integration tests. Covers the
/// Report → GET → DB-assert contract plus the Resolve lifecycle and
/// overdraw guard (legacy NetoKol_Exit semantic).
/// </summary>
public class SkartTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public SkartTests(LonApiFactory factory) => _factory = factory;

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

    /// <summary>
    /// Creates a minimal receipt (item, uom, warehouse, location) then
    /// reports a Skart against the receipt's line. Asserts: (a) Skart record
    /// persisted with normalized reason + Open resolution, (b) OK balance
    /// decremented, (c) Blocked sibling created/incremented at the same
    /// location, (d) InventoryMovement Adjustment row exists referencing
    /// the Skart number.
    /// </summary>
    [Fact]
    public async Task ReportSkart_SplitsBalanceAndRecordsAudit()
    {
        var client = await AuthedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Pull existing seed: first active item + UoM + warehouse + location.
        var item = await db.Items.FirstAsync(i => !i.IsDeleted);
        var uom = await db.UnitsOfMeasure.FirstAsync(u => !u.IsDeleted);
        var wh = await db.Warehouses.FirstAsync(w => w.IsActive && !w.IsDeleted);
        var loc = await db.Locations.FirstAsync(l => l.WarehouseId == wh.Id && !l.IsDeleted);

        // Create the Receipt through the normal endpoint so the OK balance
        // also gets created by the CreateReceipt handler.
        var batch = $"P15-3-BATCH-{Guid.NewGuid():N}".Substring(0, 15);
        var mrn = $"26MK15{Guid.NewGuid():N}".Substring(0, 14).ToUpper();
        var createReceipt = await client.PostAsJsonAsync("/api/WMS/receipts", new
        {
            warehouseId = wh.Id,
            receiptDate = DateTime.UtcNow,
            lines = new[]
            {
                new
                {
                    itemId = item.Id,
                    quantity = 100m,
                    uoMId = uom.Id,
                    batchNumber = batch,
                    mrn = (string?)null, // non-LON — skip MRN machinery
                    locationId = loc.Id
                }
            }
        });
        createReceipt.StatusCode.Should().Be(HttpStatusCode.OK, await createReceipt.Content.ReadAsStringAsync());

        // Look up the freshly created ReceiptLine id.
        var line = await db.ReceiptLines
            .AsNoTracking()
            .Where(l => l.BatchNumber == batch)
            .OrderByDescending(l => l.CreatedAt)
            .FirstAsync();

        var okBefore = await db.InventoryBalances.AsNoTracking()
            .FirstAsync(b => b.ItemId == item.Id && b.LocationId == loc.Id
                              && b.BatchNumber == batch && b.QualityStatus == QualityStatus.OK);
        okBefore.Quantity.Should().Be(100m);

        // Report skart.
        var report = await client.PostAsJsonAsync("/api/WMS/skart", new
        {
            receiptLineId = line.Id,
            skartQuantity = 15m,
            reason = "Torn on 3 rolls — supplier ack'd"
        });
        report.StatusCode.Should().Be(HttpStatusCode.OK, await report.Content.ReadAsStringAsync());

        // List endpoint surfaces the new Skart.
        var list = await client.GetFromJsonAsync<List<SkartRow>>("/api/WMS/skart?openOnly=true");
        list.Should().NotBeNull();
        var row = list!.First(s => s.ReceiptLineId == line.Id);
        row.SkartQuantity.Should().Be(15m);
        row.Resolution.Should().Be(0); // Open
        row.Reason.Should().Be("Torn on 3 rolls — supplier ack'd");
        row.SkartNumber.Should().StartWith($"SKT-{DateTime.UtcNow:yyyyMMdd}-");

        // Balance assertions: OK 100 → 85, Blocked sibling = 15 at same loc.
        db.ChangeTracker.Clear();
        var okAfter = await db.InventoryBalances
            .FirstAsync(b => b.ItemId == item.Id && b.LocationId == loc.Id
                              && b.BatchNumber == batch && b.QualityStatus == QualityStatus.OK);
        okAfter.Quantity.Should().Be(85m);
        var blocked = await db.InventoryBalances
            .FirstOrDefaultAsync(b => b.ItemId == item.Id && b.LocationId == loc.Id
                                       && b.BatchNumber == batch
                                       && b.QualityStatus == QualityStatus.Blocked);
        blocked.Should().NotBeNull();
        blocked!.Quantity.Should().Be(15m);

        // Audit movement exists, references Skart#.
        var movement = await db.InventoryMovements
            .FirstAsync(m => m.ItemId == item.Id && m.BatchNumber == batch
                              && m.Type == MovementType.Adjustment
                              && m.ReferenceNumber == $"Skart:{row.SkartNumber}");
        movement.Quantity.Should().Be(15m);
    }

    /// <summary>
    /// NetoKol_Exit semantic — cannot cumulatively exceed the received
    /// quantity across multiple skart reports on the same line.
    /// </summary>
    [Fact]
    public async Task ReportSkart_CumulativeOverdraw_Returns400()
    {
        var client = await AuthedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var item = await db.Items.FirstAsync(i => !i.IsDeleted);
        var uom = await db.UnitsOfMeasure.FirstAsync(u => !u.IsDeleted);
        var wh = await db.Warehouses.FirstAsync(w => w.IsActive && !w.IsDeleted);
        var loc = await db.Locations.FirstAsync(l => l.WarehouseId == wh.Id && !l.IsDeleted);

        var batch = $"P15-3-CUM-{Guid.NewGuid():N}".Substring(0, 14);
        await client.PostAsJsonAsync("/api/WMS/receipts", new
        {
            warehouseId = wh.Id,
            receiptDate = DateTime.UtcNow,
            lines = new[]
            {
                new { itemId = item.Id, quantity = 50m, uoMId = uom.Id, batchNumber = batch, mrn = (string?)null, locationId = loc.Id }
            }
        });
        var line = await db.ReceiptLines.AsNoTracking().Where(l => l.BatchNumber == batch)
            .OrderByDescending(l => l.CreatedAt).FirstAsync();

        // First skart 30 — OK.
        var r1 = await client.PostAsJsonAsync("/api/WMS/skart", new
        {
            receiptLineId = line.Id,
            skartQuantity = 30m,
            reason = "first"
        });
        r1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second skart 25 — would total 55 > 50. Must reject.
        var r2 = await client.PostAsJsonAsync("/api/WMS/skart", new
        {
            receiptLineId = line.Id,
            skartQuantity = 25m,
            reason = "second"
        });
        r2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Resolve flips the Skart from Open to a terminal state. Second
    /// resolve on the same Skart rejects (no double-close).
    /// </summary>
    [Fact]
    public async Task ResolveSkart_ClosesAndRejectsDoubleClose()
    {
        var client = await AuthedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var item = await db.Items.FirstAsync(i => !i.IsDeleted);
        var uom = await db.UnitsOfMeasure.FirstAsync(u => !u.IsDeleted);
        var wh = await db.Warehouses.FirstAsync(w => w.IsActive && !w.IsDeleted);
        var loc = await db.Locations.FirstAsync(l => l.WarehouseId == wh.Id && !l.IsDeleted);

        var batch = $"P15-3-RES-{Guid.NewGuid():N}".Substring(0, 14);
        await client.PostAsJsonAsync("/api/WMS/receipts", new
        {
            warehouseId = wh.Id,
            receiptDate = DateTime.UtcNow,
            lines = new[]
            {
                new { itemId = item.Id, quantity = 20m, uoMId = uom.Id, batchNumber = batch, mrn = (string?)null, locationId = loc.Id }
            }
        });
        var line = await db.ReceiptLines.AsNoTracking().Where(l => l.BatchNumber == batch)
            .OrderByDescending(l => l.CreatedAt).FirstAsync();
        var reportResp = await client.PostAsJsonAsync("/api/WMS/skart", new
        {
            receiptLineId = line.Id,
            skartQuantity = 5m,
            reason = "to resolve"
        });
        reportResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await client.GetFromJsonAsync<List<SkartRow>>("/api/WMS/skart");
        var skart = list!.First(s => s.ReceiptLineId == line.Id);

        var firstClose = await client.PostAsJsonAsync($"/api/WMS/skart/{skart.Id}/resolve", new
        {
            resolution = (int)SkartResolution.ReturnedToSupplier,
            resolutionNote = "Debit note 2026-04-23"
        });
        firstClose.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second close must 400.
        var secondClose = await client.PostAsJsonAsync($"/api/WMS/skart/{skart.Id}/resolve", new
        {
            resolution = (int)SkartResolution.Destroyed,
            resolutionNote = "second try"
        });
        secondClose.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // State reflects first close only.
        db.ChangeTracker.Clear();
        var persisted = await db.Skarts.AsNoTracking().FirstAsync(s => s.Id == skart.Id);
        persisted.Resolution.Should().Be(SkartResolution.ReturnedToSupplier);
        persisted.ResolutionNote.Should().Be("Debit note 2026-04-23");
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record SkartRow(
        Guid Id,
        string SkartNumber,
        DateTime ReportedAt,
        Guid ReceiptLineId,
        string? ReceiptNumber,
        Guid ItemId,
        string ItemCode,
        string ItemName,
        string? BatchNumber,
        string? MRN,
        decimal SkartQuantity,
        Guid UoMId,
        string UoMCode,
        string Reason,
        int Resolution,
        DateTime? ResolvedAt,
        string? ResolutionNote);
}
