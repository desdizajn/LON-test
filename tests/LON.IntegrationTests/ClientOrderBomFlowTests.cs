using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// Phase 17 §E5 — adding a ClientOrderFinishedGood + creating a ProductionOrder
/// from the hub.
///
/// Validates:
///   1. POST /api/ClientOrders/{id}/finished-goods persists the FG row.
///   2. POST /api/Production/orders with clientOrderId persists the PO and
///      transitions the parent ClientOrder Draft/Active → Producing.
///   3. GET /api/Production/orders?clientOrderId=… filters correctly.
/// </summary>
public class ClientOrderBomFlowTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public ClientOrderBomFlowTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AddFinishedGood_AndCreatePO_LinksAndTransitionsToProducing()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        // 1) Fresh ClientOrder.
        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();
        var orderResp = await client.PostAsJsonAsync("/api/clientorders", new
        {
            customerPartnerId = partnerId,
            lonAuthorizationId = lonAuthId,
            customerOrderReference = "E5-BOM-FLOW",
            orderDate = DateTime.UtcNow.Date,
        });
        orderResp.EnsureSuccessStatusCode();
        var clientOrderId = (await orderResp.Content.ReadFromJsonAsync<ResultGuid>())!.Data;

        // 2) Add a FG row.
        var (itemId, uomId) = await LoadItemSeedsAsync();
        var fgResp = await client.PostAsJsonAsync($"/api/clientorders/{clientOrderId}/finished-goods", new
        {
            itemId, quantity = 100m, uoMId = uomId, currency = "EUR", notes = "E5 smoke",
        });
        var fgBody = await fgResp.Content.ReadAsStringAsync();
        fgResp.StatusCode.Should().Be(HttpStatusCode.OK, because: fgBody);

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var fgs = await ctx.ClientOrderFinishedGoods.IgnoreQueryFilters()
                .Where(g => g.ClientOrderId == clientOrderId)
                .ToListAsync();
            fgs.Should().HaveCount(1);
            fgs[0].Quantity.Should().Be(100m);
        }

        // 3) Create a ProductionOrder linked to the same ClientOrder.
        var poResp = await client.PostAsJsonAsync("/api/Production/orders", new
        {
            itemId,
            orderQuantity = 100m,
            uoMId = uomId,
            plannedStartDate = DateTime.UtcNow.Date,
            plannedEndDate = DateTime.UtcNow.Date.AddDays(14),
            partnerId,
            clientOrderId,
            salesOrderReference = "E5-BOM-FLOW",
        });
        var poBody = await poResp.Content.ReadAsStringAsync();
        poResp.StatusCode.Should().Be(HttpStatusCode.OK, because: poBody);
        var poId = (await poResp.Content.ReadFromJsonAsync<ResultGuid>())!.Data;

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var po = await ctx.ProductionOrders.IgnoreQueryFilters().FirstAsync(p => p.Id == poId);
            po.ClientOrderId.Should().Be(clientOrderId);

            var order = await ctx.ClientOrders.IgnoreQueryFilters().FirstAsync(o => o.Id == clientOrderId);
            order.Status.Should().Be(ClientOrderStatus.Producing,
                "first ProductionOrder linked to a Draft/Active ClientOrder must transition it to Producing");
        }

        // 4) Filter PO list by clientOrderId.
        var listResp = await client.GetAsync($"/api/Production/orders?clientOrderId={clientOrderId}");
        listResp.EnsureSuccessStatusCode();
        var listBody = await listResp.Content.ReadAsStringAsync();
        listBody.Should().Contain(poId.ToString());
    }

    // ----- helpers -----

    private async Task<(Guid partnerId, Guid lonAuthorizationId)> GetCustomerAndAuthAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var partner = await ctx.Partners.IgnoreQueryFilters().FirstAsync();
        var auth = await ctx.LONAuthorizations.IgnoreQueryFilters().FirstAsync();
        return (partner.Id, auth.Id);
    }

    private async Task<(Guid itemId, Guid uomId)> LoadItemSeedsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await ctx.Items.IgnoreQueryFilters().FirstAsync();
        var uom = await ctx.UnitsOfMeasure.IgnoreQueryFilters().FirstAsync();
        return (item.Id, uom.Id);
    }

    private async Task Authenticate(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResp>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private sealed record LoginResp(string AccessToken);
    private sealed record ResultGuid(bool IsSuccess, Guid Data, string? ErrorMessage);
}
