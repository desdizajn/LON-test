using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.Production;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P5.3.1 — BOMTemplate auto-apply. When POST /production/orders is called
/// without BOMId, the handler should pick the latest Active BOM for the Item
/// (valid-now), so the created PO has BOMId populated. Repeat products →
/// zero BOM keystrokes.
/// </summary>
public class BomTemplateAutoApplyTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public BomTemplateAutoApplyTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateOrder_NoBOMId_AutoPicksLatestActiveBOMForItem()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        // Seed: fresh item + two BOMs (v1 old, v2 latest active) — the handler
        // should attach v2.
        Guid itemId, uoMId, v1Id, v2Id, tenantId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = await ctx.Tenants.FirstAsync(t => t.Code == "TEKSPORT");
            tenantId = tenant.Id;
            var uom = await ctx.UnitsOfMeasure.IgnoreQueryFilters().FirstAsync();
            uoMId = uom.Id;

            var code = $"BOM-AUTO-{Guid.NewGuid().ToString("N")[..6]}";
            var item = new LON.Domain.Entities.MasterData.Item
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                Code = code, Name = "P5.3.1 test",
                BaseUoMId = uoMId,
                Type = LON.Domain.Enums.ItemType.FinishedGood,
                CreatedAt = DateTime.UtcNow, CreatedBy = "test"
            };
            ctx.Items.Add(item);

            var v1 = new BOM
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                Code = $"{code}-BOM1", ItemId = item.Id,
                Version = 1, ValidFrom = DateTime.UtcNow.AddDays(-10),
                ValidTo = DateTime.UtcNow.AddDays(-1), // expired — must NOT be picked
                IsActive = true, BaseQuantity = 1m,
                CreatedAt = DateTime.UtcNow, CreatedBy = "test"
            };
            var v2 = new BOM
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                Code = $"{code}-BOM2", ItemId = item.Id,
                Version = 2, ValidFrom = DateTime.UtcNow.AddDays(-1),
                ValidTo = null, IsActive = true, BaseQuantity = 1m,
                CreatedAt = DateTime.UtcNow, CreatedBy = "test"
            };
            ctx.BOMs.AddRange(v1, v2);
            await ctx.SaveChangesAsync();
            itemId = item.Id;
            v1Id = v1.Id;
            v2Id = v2.Id;
        }

        // POST with no BOMId / RoutingId
        var resp = await client.PostAsJsonAsync("/api/production/orders", new
        {
            itemId,
            orderQuantity = 5m,
            uoMId,
            plannedStartDate = DateTime.UtcNow,
            plannedEndDate = DateTime.UtcNow.AddDays(1)
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ResultResponse>();
        body!.IsSuccess.Should().BeTrue();

        // Verify persisted PO has BOMId = v2 (latest valid-now)
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var po = await ctx.ProductionOrders.FirstAsync(p => p.Id == body.Data);
            po.BOMId.Should().Be(v2Id, "handler should auto-pick the latest currently-valid active BOM");
            po.BOMId.Should().NotBe(v1Id);
        }
    }

    private static async Task Authenticate(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record ResultResponse(bool IsSuccess, Guid Data, string? ErrorMessage);
}
