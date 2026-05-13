using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.Customs;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// Phase 17 §E8 — proves that the hub EX wizard's `BulkShipmentFromFGCommand`
/// invocation stamps the parent ClientOrderId on both the resulting Shipment
/// and the chained EX `CustomsDeclaration`. Without this linkage the hub's
/// Shipments tab + Razdolzuvanje aggregations would lose the FK they query on.
/// </summary>
public class ClientOrderShipmentLinkTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public ClientOrderShipmentLinkTests(LonApiFactory factory) => _factory = factory;

    private async Task Authenticate(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginBody>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    [Fact]
    public async Task BulkShipmentFromFG_WithClientOrderId_StampsBothShipmentAndChainedExDeclaration()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        Guid tenantId, itemId, uomId, locId, balId, procedureId, partnerId;
        Guid clientOrderId;

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = await ctx.Tenants.FirstAsync(t => t.Code == "TEKSPORT");
            var item = await ctx.Items.OrderBy(i => i.Code).FirstAsync();
            var uom = await ctx.UnitsOfMeasure.FirstAsync();
            var loc = await ctx.Locations.FirstAsync(l => l.Code.StartsWith("RCV"));
            var partner = await ctx.Partners.FirstAsync(p => !p.IsDeleted && p.IsActive);
            // EX procedure: 3151 (export of inward-processed goods).
            var procedure = await ctx.CustomsProcedures
                .FirstOrDefaultAsync(p => p.Code == "3151" && p.IsActive)
                ?? await ctx.CustomsProcedures.FirstAsync(p => p.IsActive);

            tenantId = tenant.Id;
            itemId = item.Id;
            uomId = uom.Id;
            locId = loc.Id;
            partnerId = partner.Id;
            procedureId = procedure.Id;

            var co = new ClientOrder
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderNumber = $"CO-E8-{Guid.NewGuid().ToString()[..6]}",
                CustomerPartnerId = partner.Id,
                OrderDate = DateTime.UtcNow.Date,
                Status = ClientOrderStatus.Active,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            };
            ctx.ClientOrders.Add(co);
            clientOrderId = co.Id;

            balId = Guid.NewGuid();
            ctx.InventoryBalances.Add(new InventoryBalance
            {
                Id = balId,
                TenantId = tenantId,
                ItemId = itemId,
                LocationId = locId,
                BatchNumber = "E8-FG-LINK",
                MRN = "26MKE8000000000001",
                Quantity = 20m,
                UoMId = uomId,
                QualityStatus = QualityStatus.OK,
                LonProcessState = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            });
            await ctx.SaveChangesAsync();
        }

        var resp = await client.PostAsJsonAsync("/api/wms/shipments/bulk-from-fg", new
        {
            itemId,
            mrn = "26MKE8000000000001",
            partnerId,
            customsProcedureId = procedureId,
            reference = "E8 hub smoke",
            createExportDeclaration = true,
            clientOrderId,
        });
        var bodyText = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK, bodyText);
        var env = await resp.Content.ReadFromJsonAsync<ResultEnvelope<ShipResult>>();
        env!.IsSuccess.Should().BeTrue();
        env.Data!.ExportDeclarationId.Should().NotBeNull();

        using var verifyScope = _factory.Services.CreateScope();
        var verifyCtx = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var shipment = await verifyCtx.Shipments.IgnoreQueryFilters()
            .FirstAsync(s => s.Id == env.Data.ShipmentId);
        shipment.ClientOrderId.Should().Be(clientOrderId, "BulkShipmentFromFG must stamp Shipment.ClientOrderId");

        var declaration = await verifyCtx.CustomsDeclarations.IgnoreQueryFilters()
            .FirstAsync(d => d.Id == env.Data.ExportDeclarationId!.Value);
        declaration.ClientOrderId.Should().Be(clientOrderId, "chained EX declaration must also carry ClientOrderId");
        declaration.DeclarationType.Should().Be("EX");
    }

    [Fact]
    public async Task GetShipments_WithClientOrderIdFilter_ReturnsOnlyMatching()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        // Create a fresh ClientOrder + a single Shipment stamped with its id.
        Guid clientOrderId, shipmentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = await ctx.Tenants.FirstAsync(t => t.Code == "TEKSPORT");
            var partner = await ctx.Partners.FirstAsync(p => !p.IsDeleted && p.IsActive);

            var co = new ClientOrder
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                OrderNumber = $"CO-E8F-{Guid.NewGuid().ToString()[..6]}",
                CustomerPartnerId = partner.Id,
                OrderDate = DateTime.UtcNow.Date,
                Status = ClientOrderStatus.Active,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            };
            ctx.ClientOrders.Add(co);
            clientOrderId = co.Id;

            var shipment = new Shipment
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                ShipmentNumber = $"SHP-E8F-{Guid.NewGuid().ToString()[..6]}",
                ShipmentDate = DateTime.UtcNow,
                Status = ShipmentStatus.Draft,
                ClientOrderId = co.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            };
            ctx.Shipments.Add(shipment);
            shipmentId = shipment.Id;
            await ctx.SaveChangesAsync();
        }

        var resp = await client.GetAsync($"/api/wms/shipments?clientOrderId={clientOrderId}&pageSize=100");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await resp.Content.ReadFromJsonAsync<List<ShipRow>>();
        rows.Should().NotBeNull();
        rows!.Should().OnlyContain(r => r.ClientOrderId == clientOrderId);
        rows.Should().Contain(r => r.Id == shipmentId);
    }

    [Fact]
    public async Task UpdateQualityStatus_AcceptsBothBalanceIdNames_AndStampsAudit()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        Guid balId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = await ctx.Tenants.FirstAsync(t => t.Code == "TEKSPORT");
            var item = await ctx.Items.FirstAsync();
            var uom = await ctx.UnitsOfMeasure.FirstAsync();
            var loc = await ctx.Locations.FirstAsync();
            balId = Guid.NewGuid();
            ctx.InventoryBalances.Add(new InventoryBalance
            {
                Id = balId,
                TenantId = tenant.Id,
                ItemId = item.Id,
                LocationId = loc.Id,
                BatchNumber = "QC-E8-A",
                MRN = "26MKE8QC0000000001",
                Quantity = 5m,
                UoMId = uom.Id,
                QualityStatus = QualityStatus.Quarantine,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            });
            await ctx.SaveChangesAsync();
        }

        // Legacy field name path.
        var resp1 = await client.PostAsJsonAsync("/api/wms/inventory/quality-status", new
        {
            inventoryBalanceId = balId,
            newQualityStatus = 1, // OK
            reason = "passed QC",
            notes = "ClientOrderShipmentLinkTests",
        });
        resp1.StatusCode.Should().Be(HttpStatusCode.OK);

        using var v = _factory.Services.CreateScope();
        var vctx = v.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bal = await vctx.InventoryBalances.IgnoreQueryFilters().FirstAsync(b => b.Id == balId);
        bal.QualityStatus.Should().Be(QualityStatus.OK);

        // Audit movement emitted with reason.
        var movement = await vctx.InventoryMovements.IgnoreQueryFilters()
            .Where(m => m.ReferenceNumber == "QC:OK" && m.ItemId == bal.ItemId)
            .OrderByDescending(m => m.CreatedAt)
            .FirstAsync();
        movement.Notes.Should().Contain("passed QC");

        // Short field name path.
        var resp2 = await client.PostAsJsonAsync("/api/wms/inventory/quality-status", new
        {
            balanceId = balId,
            newQualityStatus = 2, // Blocked
            reason = "follow-up reject",
        });
        resp2.StatusCode.Should().Be(HttpStatusCode.OK);

        using var v2 = _factory.Services.CreateScope();
        var vctx2 = v2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bal2 = await vctx2.InventoryBalances.IgnoreQueryFilters().FirstAsync(b => b.Id == balId);
        bal2.QualityStatus.Should().Be(QualityStatus.Blocked);
    }

    private sealed record LoginBody(string AccessToken);
    private sealed record ResultEnvelope<T>(bool IsSuccess, T? Data, string? ErrorMessage, string? ErrorCode);
    private sealed record ShipResult(Guid ShipmentId, string ShipmentNumber, int LinesCreated, decimal TotalQuantity, Guid? ExportDeclarationId);
    private sealed record ShipRow(Guid Id, Guid? ClientOrderId, string ShipmentNumber);
}
