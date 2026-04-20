using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.MasterData;
using LON.Domain.Entities.Production;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P9.1 / P9.6 regression guard — FinishedGoods simple queries.
/// </summary>
public class FinishedGoodsTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;
    public FinishedGoodsTests(LonApiFactory factory) => _factory = factory;

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
    public async Task PackagingStock_FiltersItemTypeEqualsPackaging()
    {
        var client = await AuthedAsync();

        // Seed: one Packaging item + one RawMaterial item + one inventory row
        // for the packaging item. Only the packaging item should show up.
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = ctx.Tenants.First();
            var uom = ctx.UnitsOfMeasure.First();
            var warehouse = ctx.Warehouses.First();
            var location = ctx.Locations.First(l => l.WarehouseId == warehouse.Id);

            var packaging = await ctx.Items.FirstOrDefaultAsync(i => i.Code == "P9-PKG-TEST");
            if (packaging is null)
            {
                packaging = new Item
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Code = "P9-PKG-TEST",
                    Name = "P9 packaging",
                    Type = ItemType.Packaging,
                    BaseUoMId = uom.Id,
                };
                ctx.Items.Add(packaging);
            }

            var raw = await ctx.Items.FirstOrDefaultAsync(i => i.Code == "P9-RAW-TEST");
            if (raw is null)
            {
                raw = new Item
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Code = "P9-RAW-TEST",
                    Name = "P9 raw",
                    Type = ItemType.RawMaterial,
                    BaseUoMId = uom.Id,
                };
                ctx.Items.Add(raw);
            }
            await ctx.SaveChangesAsync();

            var hasBalance = await ctx.InventoryBalances
                .AnyAsync(b => b.ItemId == packaging.Id);
            if (!hasBalance)
            {
                ctx.InventoryBalances.Add(new InventoryBalance
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    ItemId = packaging.Id,
                    LocationId = location.Id,
                    UoMId = uom.Id,
                    Quantity = 500m,
                    QualityStatus = QualityStatus.OK,
                });
                await ctx.SaveChangesAsync();
            }
        }

        var resp = await client.GetFromJsonAsync<Envelope<List<PackagingStockRow>>>(
            "/api/FinishedGoods/packaging-stock");
        resp!.IsSuccess.Should().BeTrue();

        var pkg = resp.Data!.FirstOrDefault(r => r.ItemCode == "P9-PKG-TEST");
        pkg.Should().NotBeNull();
        pkg!.TotalOnHand.Should().BeGreaterThanOrEqualTo(500m);

        // RawMaterial item must NOT appear.
        resp.Data!.Should().NotContain(r => r.ItemCode == "P9-RAW-TEST");
    }

    [Fact]
    public async Task AwaitingPack_Excludes_FullyShipped_ProductionOrders()
    {
        var client = await AuthedAsync();

        Guid poFullyShippedId;
        Guid poRemainingId;

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = ctx.Tenants.First();
            var uom = ctx.UnitsOfMeasure.First();

            var item = await ctx.Items.FirstOrDefaultAsync(i => i.Code == "P9-FG-TEST");
            if (item is null)
            {
                item = new Item
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Code = "P9-FG-TEST",
                    Name = "P9 FG",
                    Type = ItemType.FinishedGood,
                    BaseUoMId = uom.Id,
                };
                ctx.Items.Add(item);
                await ctx.SaveChangesAsync();
            }

            var warehouse = ctx.Warehouses.First();
            var location = ctx.Locations.First(l => l.WarehouseId == warehouse.Id);

            // PO 1 — fully shipped (ProducedQty=10, ShipmentLine Qty=10 on same batch).
            var po1 = new ProductionOrder
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                OrderNumber = $"P9-PO-FULLY-{Guid.NewGuid():N}".Substring(0, 20),
                ItemId = item.Id,
                OrderQuantity = 10m,
                ProducedQuantity = 10m,
                UoMId = uom.Id,
                Status = ProductionOrderStatus.Completed,
                PlannedStartDate = DateTime.UtcNow.AddDays(-7),
                PlannedEndDate = DateTime.UtcNow.AddDays(-1),
                ActualEndDate = DateTime.UtcNow.AddDays(-1),
            };
            var po2 = new ProductionOrder
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                OrderNumber = $"P9-PO-REM-{Guid.NewGuid():N}".Substring(0, 20),
                ItemId = item.Id,
                OrderQuantity = 20m,
                ProducedQuantity = 20m,
                UoMId = uom.Id,
                Status = ProductionOrderStatus.Completed,
                PlannedStartDate = DateTime.UtcNow.AddDays(-7),
                PlannedEndDate = DateTime.UtcNow.AddDays(-1),
                ActualEndDate = DateTime.UtcNow.AddDays(-1),
            };
            ctx.ProductionOrders.AddRange(po1, po2);

            var batch1 = $"P9-B1-{Guid.NewGuid():N}".Substring(0, 12);
            var batch2 = $"P9-B2-{Guid.NewGuid():N}".Substring(0, 12);

            ctx.ProductionReceipts.AddRange(
                new ProductionReceipt
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    ReceiptNumber = $"PR-{Guid.NewGuid():N}".Substring(0, 14),
                    ProductionOrderId = po1.Id,
                    ItemId = item.Id,
                    BatchNumber = batch1,
                    Quantity = 10m,
                    UoMId = uom.Id,
                    LocationId = location.Id,
                    ReceiptDate = DateTime.UtcNow,
                    QualityStatus = QualityStatus.OK,
                },
                new ProductionReceipt
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    ReceiptNumber = $"PR-{Guid.NewGuid():N}".Substring(0, 14),
                    ProductionOrderId = po2.Id,
                    ItemId = item.Id,
                    BatchNumber = batch2,
                    Quantity = 20m,
                    UoMId = uom.Id,
                    LocationId = location.Id,
                    ReceiptDate = DateTime.UtcNow,
                    QualityStatus = QualityStatus.OK,
                });

            var shipment = new Shipment
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                ShipmentNumber = $"SH-{Guid.NewGuid():N}".Substring(0, 14),
                ShipmentDate = DateTime.UtcNow,
                Status = ShipmentStatus.Shipped,
            };
            ctx.Shipments.Add(shipment);
            ctx.ShipmentLines.Add(new ShipmentLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                ShipmentId = shipment.Id,
                LineNumber = 1,
                ItemId = item.Id,
                BatchNumber = batch1,
                Quantity = 10m,
                UoMId = uom.Id,
            });

            await ctx.SaveChangesAsync();
            poFullyShippedId = po1.Id;
            poRemainingId = po2.Id;
        }

        var resp = await client.GetFromJsonAsync<Envelope<List<AwaitingRow>>>(
            "/api/FinishedGoods/awaiting-pack");
        resp!.IsSuccess.Should().BeTrue();

        resp.Data!.Should().NotContain(r => r.ProductionOrderId == poFullyShippedId);
        var remaining = resp.Data!.FirstOrDefault(r => r.ProductionOrderId == poRemainingId);
        remaining.Should().NotBeNull();
        remaining!.RemainingToPack.Should().Be(20m);
        remaining.ShippedQuantity.Should().Be(0m);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record Envelope<T>(bool IsSuccess, T? Data, string? ErrorMessage, string? ErrorCode);

    private sealed record PackagingStockRow(
        Guid ItemId, string ItemCode, string ItemName,
        Guid UoMId, string UoMCode,
        decimal TotalOnHand, int LocationCount);

    private sealed record AwaitingRow(
        Guid ProductionOrderId, string OrderNumber, Guid ItemId,
        string ItemCode, string ItemName,
        decimal ProducedQuantity, decimal ShippedQuantity, decimal RemainingToPack,
        Guid UoMId, string UoMCode, DateTime? ActualEndDate,
        Guid? CustomerPartnerId, string? CustomerOrderNumber);
}
