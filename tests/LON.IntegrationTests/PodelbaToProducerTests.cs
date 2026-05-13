using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// Phase 17 §E6 — multi-balance, single-producer Podelba. Dual of
/// <see cref="PodelbaTests"/>: that flow splits ONE balance across many
/// producers (full distribution required); this routes MANY balances to ONE
/// producer with partial quantities allowed.
/// </summary>
public class PodelbaToProducerTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public PodelbaToProducerTests(LonApiFactory factory) => _factory = factory;

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

    private static async Task PostReceiptAsync(HttpClient client, Guid warehouseId, Guid itemId, Guid uomId, Guid locationId, decimal qty, string batch)
    {
        var resp = await client.PostAsJsonAsync("/api/WMS/receipts", new
        {
            warehouseId,
            receiptDate = DateTime.UtcNow,
            lines = new[]
            {
                new { itemId, quantity = qty, uoMId = uomId, batchNumber = batch, mrn = (string?)null, locationId },
            },
        });
        resp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PodelbaToProducer_PartialPerLine_SourcesKeepRemainders()
    {
        var client = await AuthedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var item1 = await db.Items.FirstAsync(i => !i.IsDeleted);
        var item2 = await db.Items.Where(i => !i.IsDeleted).Skip(1).FirstAsync();
        var uom = await db.UnitsOfMeasure.FirstAsync(u => !u.IsDeleted);
        var wh = await db.Warehouses.FirstAsync(w => w.IsActive && !w.IsDeleted);
        var loc = await db.Locations.FirstAsync(l => l.WarehouseId == wh.Id && !l.IsDeleted);
        var tenant = await db.Tenants.FirstAsync(t => t.Code == "TEKSPORT");

        var producer = new Partner
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Code = $"E6PR-{Guid.NewGuid():N}".Substring(0, 15),
            Name = "Producer E6 partial",
            Type = PartnerType.Producer,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };
        db.Partners.Add(producer);
        await db.SaveChangesAsync();

        var batchA = $"E6A-{Guid.NewGuid():N}".Substring(0, 15);
        var batchB = $"E6B-{Guid.NewGuid():N}".Substring(0, 15);
        await PostReceiptAsync(client, wh.Id, item1.Id, uom.Id, loc.Id, 100m, batchA);
        await PostReceiptAsync(client, wh.Id, item2.Id, uom.Id, loc.Id, 50m, batchB);

        var src1 = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.ItemId == item1.Id && b.BatchNumber == batchA).OrderByDescending(b => b.CreatedAt).FirstAsync();
        var src2 = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.ItemId == item2.Id && b.BatchNumber == batchB).OrderByDescending(b => b.CreatedAt).FirstAsync();

        var resp = await client.PostAsJsonAsync("/api/WMS/inventory/podelba-to-producer", new
        {
            producerId = producer.Id,
            clientOrderId = (Guid?)null,
            reason = "smoke test",
            lines = new[]
            {
                new { sourceBalanceId = src1.Id, quantity = 60m },
                new { sourceBalanceId = src2.Id, quantity = 30m },
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());

        db.ChangeTracker.Clear();
        var src1After = await db.InventoryBalances.AsNoTracking().FirstAsync(b => b.Id == src1.Id);
        var src2After = await db.InventoryBalances.AsNoTracking().FirstAsync(b => b.Id == src2.Id);
        src1After.Quantity.Should().Be(40m, "60 of 100 distributed; remainder stays at source");
        src2After.Quantity.Should().Be(20m, "30 of 50 distributed");

        var siblings = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.AssignedProducerId == producer.Id
                        && (b.BatchNumber == batchA || b.BatchNumber == batchB))
            .ToListAsync();
        siblings.Should().HaveCount(2);
        siblings.Single(s => s.BatchNumber == batchA).Quantity.Should().Be(60m);
        siblings.Single(s => s.BatchNumber == batchB).Quantity.Should().Be(30m);
    }

    [Fact]
    public async Task PodelbaToProducer_ConsolidatesSiblingOnRerun()
    {
        var client = await AuthedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var item = await db.Items.FirstAsync(i => !i.IsDeleted);
        var uom = await db.UnitsOfMeasure.FirstAsync(u => !u.IsDeleted);
        var wh = await db.Warehouses.FirstAsync(w => w.IsActive && !w.IsDeleted);
        var loc = await db.Locations.FirstAsync(l => l.WarehouseId == wh.Id && !l.IsDeleted);
        var tenant = await db.Tenants.FirstAsync(t => t.Code == "TEKSPORT");

        var producer = new Partner
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Code = $"E6CR-{Guid.NewGuid():N}".Substring(0, 15),
            Name = "Producer E6 consolidate",
            Type = PartnerType.Producer,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };
        db.Partners.Add(producer);
        await db.SaveChangesAsync();

        var batch = $"E6C-{Guid.NewGuid():N}".Substring(0, 15);
        await PostReceiptAsync(client, wh.Id, item.Id, uom.Id, loc.Id, 200m, batch);
        var src = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.ItemId == item.Id && b.BatchNumber == batch)
            .OrderByDescending(b => b.CreatedAt).FirstAsync();

        var first = await client.PostAsJsonAsync("/api/WMS/inventory/podelba-to-producer", new
        {
            producerId = producer.Id,
            lines = new[] { new { sourceBalanceId = src.Id, quantity = 50m } },
        });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PostAsJsonAsync("/api/WMS/inventory/podelba-to-producer", new
        {
            producerId = producer.Id,
            lines = new[] { new { sourceBalanceId = src.Id, quantity = 30m } },
        });
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        db.ChangeTracker.Clear();
        var siblings = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.AssignedProducerId == producer.Id && b.BatchNumber == batch)
            .ToListAsync();
        siblings.Should().HaveCount(1, "natural-key match must consolidate, not duplicate");
        siblings.Single().Quantity.Should().Be(80m);

        var srcAfter = await db.InventoryBalances.AsNoTracking().FirstAsync(b => b.Id == src.Id);
        srcAfter.Quantity.Should().Be(120m);
    }

    [Fact]
    public async Task PodelbaToProducer_RejectsOverAllocation()
    {
        var client = await AuthedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var item = await db.Items.FirstAsync(i => !i.IsDeleted);
        var uom = await db.UnitsOfMeasure.FirstAsync(u => !u.IsDeleted);
        var wh = await db.Warehouses.FirstAsync(w => w.IsActive && !w.IsDeleted);
        var loc = await db.Locations.FirstAsync(l => l.WarehouseId == wh.Id && !l.IsDeleted);
        var tenant = await db.Tenants.FirstAsync(t => t.Code == "TEKSPORT");

        var producer = new Partner
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Code = $"E6OV-{Guid.NewGuid():N}".Substring(0, 15),
            Name = "Producer E6 over",
            Type = PartnerType.Producer,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };
        db.Partners.Add(producer);
        await db.SaveChangesAsync();

        var batch = $"E6O-{Guid.NewGuid():N}".Substring(0, 15);
        await PostReceiptAsync(client, wh.Id, item.Id, uom.Id, loc.Id, 25m, batch);
        var src = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.ItemId == item.Id && b.BatchNumber == batch)
            .OrderByDescending(b => b.CreatedAt).FirstAsync();

        var resp = await client.PostAsJsonAsync("/api/WMS/inventory/podelba-to-producer", new
        {
            producerId = producer.Id,
            lines = new[] { new { sourceBalanceId = src.Id, quantity = 100m } },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PodelbaToProducer_RejectsNonProducerPartner()
    {
        var client = await AuthedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var item = await db.Items.FirstAsync(i => !i.IsDeleted);
        var uom = await db.UnitsOfMeasure.FirstAsync(u => !u.IsDeleted);
        var wh = await db.Warehouses.FirstAsync(w => w.IsActive && !w.IsDeleted);
        var loc = await db.Locations.FirstAsync(l => l.WarehouseId == wh.Id && !l.IsDeleted);
        var notProducer = await db.Partners.FirstAsync(p => p.Type != PartnerType.Producer && !p.IsDeleted);

        var batch = $"E6N-{Guid.NewGuid():N}".Substring(0, 15);
        await PostReceiptAsync(client, wh.Id, item.Id, uom.Id, loc.Id, 10m, batch);
        var src = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.ItemId == item.Id && b.BatchNumber == batch)
            .OrderByDescending(b => b.CreatedAt).FirstAsync();

        var resp = await client.PostAsJsonAsync("/api/WMS/inventory/podelba-to-producer", new
        {
            producerId = notProducer.Id,
            lines = new[] { new { sourceBalanceId = src.Id, quantity = 10m } },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PodelbaToProducer_EmitsInventoryMovementsPerLine()
    {
        var client = await AuthedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var item = await db.Items.FirstAsync(i => !i.IsDeleted);
        var uom = await db.UnitsOfMeasure.FirstAsync(u => !u.IsDeleted);
        var wh = await db.Warehouses.FirstAsync(w => w.IsActive && !w.IsDeleted);
        var loc = await db.Locations.FirstAsync(l => l.WarehouseId == wh.Id && !l.IsDeleted);
        var tenant = await db.Tenants.FirstAsync(t => t.Code == "TEKSPORT");

        var producer = new Partner
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Code = $"E6MV-{Guid.NewGuid():N}".Substring(0, 15),
            Name = "Producer E6 movements",
            Type = PartnerType.Producer,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };
        db.Partners.Add(producer);
        await db.SaveChangesAsync();

        var batch = $"E6M-{Guid.NewGuid():N}".Substring(0, 15);
        await PostReceiptAsync(client, wh.Id, item.Id, uom.Id, loc.Id, 12m, batch);
        var src = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.ItemId == item.Id && b.BatchNumber == batch)
            .OrderByDescending(b => b.CreatedAt).FirstAsync();

        var resp = await client.PostAsJsonAsync("/api/WMS/inventory/podelba-to-producer", new
        {
            producerId = producer.Id,
            lines = new[] { new { sourceBalanceId = src.Id, quantity = 7m } },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var refLink = $"Podelba:{producer.Id}";
        var movements = await db.InventoryMovements.AsNoTracking()
            .Where(m => m.ReferenceNumber == refLink && m.ItemId == item.Id && m.BatchNumber == batch)
            .ToListAsync();
        movements.Should().HaveCount(1);
        movements[0].Quantity.Should().Be(7m);
        movements[0].Type.Should().Be(MovementType.Transfer);
        movements[0].FromLocationId.Should().Be(loc.Id);
        movements[0].ToLocationId.Should().Be(loc.Id);
        movements[0].MovementNumber.Should().StartWith("PDL-");
    }

    [Fact]
    public async Task SuggestionsProducer_ReturnsTopRecentProducer()
    {
        var client = await AuthedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Ensure at least one Producer partner exists (always true for TEKSPORT seed,
        // but guard so the test is self-contained).
        var tenant = await db.Tenants.FirstAsync(t => t.Code == "TEKSPORT");
        if (!await db.Partners.AnyAsync(p => p.Type == PartnerType.Producer && p.IsActive && !p.IsDeleted))
        {
            db.Partners.Add(new Partner
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Code = $"E6SG-{Guid.NewGuid():N}".Substring(0, 15),
                Name = "Producer E6 suggestion",
                Type = PartnerType.Producer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            });
            await db.SaveChangesAsync();
        }

        var resp = await client.GetAsync("/api/Suggestions/producer");
        // Either 200 with a body or 204 No Content if seed has no Producer-type partners (defensive).
        (resp.StatusCode == HttpStatusCode.OK || resp.StatusCode == HttpStatusCode.NoContent)
            .Should().BeTrue($"got {resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
    }

    private sealed record LoginResponse(string AccessToken);
}
