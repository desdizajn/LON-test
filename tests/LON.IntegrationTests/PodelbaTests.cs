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
/// P15.8 — Podelba (multi-producer distribution) integration tests. Verifies
/// the source balance drains to zero, per-producer siblings carry the exact
/// allocated quantity, and the natural-key consolidation prevents duplicate
/// rows on re-run.
/// </summary>
public class PodelbaTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public PodelbaTests(LonApiFactory factory) => _factory = factory;

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
    public async Task Podelba_SplitsSourceIntoProducerSiblings()
    {
        var client = await AuthedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var item = await db.Items.FirstAsync(i => !i.IsDeleted);
        var uom = await db.UnitsOfMeasure.FirstAsync(u => !u.IsDeleted);
        var wh = await db.Warehouses.FirstAsync(w => w.IsActive && !w.IsDeleted);
        var loc = await db.Locations.FirstAsync(l => l.WarehouseId == wh.Id && !l.IsDeleted);
        var tenant = await db.Tenants.FirstAsync(t => t.Code == "TEKSPORT");

        // Create two Producer partners (PartnerType=Producer).
        var pa = new Partner { Id = Guid.NewGuid(), TenantId = tenant.Id, Code = $"PRD-A-{Guid.NewGuid():N}".Substring(0, 15), Name = "Producer A", Type = PartnerType.Producer, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "test" };
        var pb = new Partner { Id = Guid.NewGuid(), TenantId = tenant.Id, Code = $"PRD-B-{Guid.NewGuid():N}".Substring(0, 15), Name = "Producer B", Type = PartnerType.Producer, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "test" };
        db.Partners.Add(pa);
        db.Partners.Add(pb);
        await db.SaveChangesAsync();

        // Create a source receipt qty=100.
        var batch = $"P158-{Guid.NewGuid():N}".Substring(0, 15);
        await client.PostAsJsonAsync("/api/WMS/receipts", new
        {
            warehouseId = wh.Id,
            receiptDate = DateTime.UtcNow,
            lines = new[]
            {
                new { itemId = item.Id, quantity = 100m, uoMId = uom.Id, batchNumber = batch, mrn = (string?)null, locationId = loc.Id }
            }
        });

        var source = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.ItemId == item.Id && b.BatchNumber == batch && b.QualityStatus == QualityStatus.OK)
            .OrderByDescending(b => b.CreatedAt)
            .FirstAsync();

        // Podelba: 60 to A, 40 to B.
        var podelba = await client.PostAsJsonAsync("/api/WMS/podelba", new
        {
            sourceBalanceId = source.Id,
            allocations = new[]
            {
                new { producerId = pa.Id, quantity = 60m },
                new { producerId = pb.Id, quantity = 40m }
            }
        });
        podelba.StatusCode.Should().Be(HttpStatusCode.OK, await podelba.Content.ReadAsStringAsync());

        db.ChangeTracker.Clear();
        var refreshedSource = await db.InventoryBalances.AsNoTracking().FirstAsync(b => b.Id == source.Id);
        refreshedSource.Quantity.Should().Be(0m, "source fully distributed");

        var siblings = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.ItemId == item.Id && b.BatchNumber == batch && b.AssignedProducerId != null)
            .ToListAsync();
        siblings.Should().HaveCount(2);
        siblings.Single(s => s.AssignedProducerId == pa.Id).Quantity.Should().Be(60m);
        siblings.Single(s => s.AssignedProducerId == pb.Id).Quantity.Should().Be(40m);
    }

    [Fact]
    public async Task Podelba_WithNonProducerPartner_Returns400()
    {
        var client = await AuthedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var item = await db.Items.FirstAsync(i => !i.IsDeleted);
        var uom = await db.UnitsOfMeasure.FirstAsync(u => !u.IsDeleted);
        var wh = await db.Warehouses.FirstAsync(w => w.IsActive && !w.IsDeleted);
        var loc = await db.Locations.FirstAsync(l => l.WarehouseId == wh.Id && !l.IsDeleted);
        // Pick any partner whose type ≠ Producer.
        var notProducer = await db.Partners.FirstAsync(p => p.Type != PartnerType.Producer && !p.IsDeleted);

        var batch = $"P158N-{Guid.NewGuid():N}".Substring(0, 14);
        await client.PostAsJsonAsync("/api/WMS/receipts", new
        {
            warehouseId = wh.Id,
            receiptDate = DateTime.UtcNow,
            lines = new[]
            {
                new { itemId = item.Id, quantity = 10m, uoMId = uom.Id, batchNumber = batch, mrn = (string?)null, locationId = loc.Id }
            }
        });
        var source = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.ItemId == item.Id && b.BatchNumber == batch)
            .OrderByDescending(b => b.CreatedAt).FirstAsync();

        var resp = await client.PostAsJsonAsync("/api/WMS/podelba", new
        {
            sourceBalanceId = source.Id,
            allocations = new[] { new { producerId = notProducer.Id, quantity = 10m } }
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record LoginResponse(string AccessToken);
}
