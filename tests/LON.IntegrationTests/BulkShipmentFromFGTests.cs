using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P5.2.4 — Bulk Shipment from Finished-Goods selection.
///
/// Seeds two FG InventoryBalances that share an Item, then fires the bulk
/// endpoint with an ItemId filter. Expects one Shipment with two lines,
/// both source balances drained to zero, and movements emitted.
///
/// Error path: absent filter → 400 with ErrorCode=transfer.no_filter.
/// </summary>
public class BulkShipmentFromFGTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public BulkShipmentFromFGTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Bulk_WithItemFilter_DrainsAllBalancesAndCreatesShipment()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        Guid itemId, uomId, locationId, tenantId;
        Guid b1, b2;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await ctx.Items.OrderBy(i => i.Code).LastAsync();
            var uom = await ctx.UnitsOfMeasure.FirstAsync(u => u.Code == "KG");
            var loc = await ctx.Locations.FirstAsync(l => l.Code.StartsWith("RCV"));
            itemId = item.Id;
            uomId = uom.Id;
            locationId = loc.Id;
            tenantId = item.TenantId;

            b1 = Guid.NewGuid();
            b2 = Guid.NewGuid();
            ctx.InventoryBalances.Add(new InventoryBalance
            {
                Id = b1, TenantId = tenantId, ItemId = itemId, LocationId = locationId,
                BatchNumber = "P524-FG-A", MRN = null,
                Quantity = 15m, UoMId = uomId,
                QualityStatus = QualityStatus.OK,
                LonProcessState = null
            });
            ctx.InventoryBalances.Add(new InventoryBalance
            {
                Id = b2, TenantId = tenantId, ItemId = itemId, LocationId = locationId,
                BatchNumber = "P524-FG-B", MRN = null,
                Quantity = 25m, UoMId = uomId,
                QualityStatus = QualityStatus.OK,
                LonProcessState = null
            });
            await ctx.SaveChangesAsync();
        }

        var resp = await client.PostAsJsonAsync("/api/wms/shipments/bulk-from-fg", new
        {
            itemId,
            reference = "P524 smoke",
            createExportDeclaration = false
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ResultEnvelope<ShipResult>>();
        body!.IsSuccess.Should().BeTrue();
        body.Data!.LinesCreated.Should().BeGreaterOrEqualTo(2);
        body.Data.TotalQuantity.Should().BeGreaterOrEqualTo(40m);

        using var scope2 = _factory.Services.CreateScope();
        var ctx2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var drained1 = await ctx2.InventoryBalances.IgnoreQueryFilters().FirstAsync(b => b.Id == b1);
        var drained2 = await ctx2.InventoryBalances.IgnoreQueryFilters().FirstAsync(b => b.Id == b2);
        drained1.Quantity.Should().Be(0m);
        drained2.Quantity.Should().Be(0m);

        var shipment = await ctx2.Shipments.IgnoreQueryFilters()
            .FirstAsync(s => s.Id == body.Data.ShipmentId);
        shipment.Status.Should().Be(ShipmentStatus.Draft);
    }

    [Fact]
    public async Task Bulk_NoFilter_Returns400_WithErrorCode()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var resp = await client.PostAsJsonAsync("/api/wms/shipments/bulk-from-fg", new { });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<ResultEnvelope<ShipResult>>();
        body!.IsSuccess.Should().BeFalse();
        body.ErrorCode.Should().Be("transfer.no_filter");
    }

    private async Task Authenticate(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginBody>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private sealed record LoginBody(string AccessToken);
    private sealed record ResultEnvelope<T>(bool IsSuccess, T? Data, string? ErrorMessage, string? ErrorCode);
    private sealed record ShipResult(Guid ShipmentId, string ShipmentNumber, int LinesCreated, decimal TotalQuantity, Guid? ExportDeclarationId);
}
