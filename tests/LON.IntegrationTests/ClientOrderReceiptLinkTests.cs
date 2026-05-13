using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// Phase 17 §E4 — Receipt creation from the ClientOrder hub.
/// Validates:
///   1. POST /api/WMS/receipts/bulk-from-declaration creates a Receipt whose
///      lines reference the picked IM declaration (already linked to the hub
///      ClientOrder via §E3).
///   2. GET /api/WMS/receipts?clientOrderId=… returns receipts whose lines
///      reach a declaration tied to that ClientOrder.
/// </summary>
public class ClientOrderReceiptLinkTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public ClientOrderReceiptLinkTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task BulkReceipt_FromHubLinkedDeclaration_IsReachableViaClientOrderFilter()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        // 1) Create a fresh ClientOrder.
        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();
        var orderResp = await client.PostAsJsonAsync("/api/clientorders", new
        {
            customerPartnerId = partnerId,
            lonAuthorizationId = lonAuthId,
            customerOrderReference = "E4-RECEIPT-FLOW",
            orderDate = DateTime.UtcNow.Date,
        });
        orderResp.EnsureSuccessStatusCode();
        var clientOrderId = (await orderResp.Content.ReadFromJsonAsync<ResultGuid>())!.Data;

        // 2) Create an IM declaration linked to it.
        var (procedureId, itemId, uomId, tariffNumber) = await LoadDeclarationSeedsAsync();
        var declResp = await client.PostAsJsonAsync("/api/customs/declarations", new
        {
            declarationNumber = "",
            mrn = "",
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procedureId,
            partnerId,
            lonAuthorizationId = lonAuthId,
            clientOrderId,
            totalCustomsValue = 500m, currency = "EUR",
            senderName = "E4 Sender", senderCountry = "DE", countryOfDispatch = "DE",
            lines = new[] {
                new { itemId, tariffCode = tariffNumber, quantity = 50m, uoMId = uomId,
                    customsValue = 500m, countryOfOrigin = "DE", dutyRate = 5m, vatRate = 18m,
                    netWeight = 100m, grossWeight = 110m }
            }
        });
        declResp.EnsureSuccessStatusCode();
        var declarationId = (await declResp.Content.ReadFromJsonAsync<ResultGuid>())!.Data;

        // 3) Bulk-receipt that declaration.
        var warehouseId = await GetWarehouseIdAsync();
        var bulkResp = await client.PostAsJsonAsync("/api/wms/receipts/bulk-from-declaration", new
        {
            customsDeclarationId = declarationId,
            warehouseId,
            targetLocationId = (Guid?)null,
            referenceNumber = "E4-SMOKE",
        });
        var bulkBody = await bulkResp.Content.ReadAsStringAsync();
        bulkResp.StatusCode.Should().Be(HttpStatusCode.OK, because: bulkBody);

        // 4) Filter receipts by clientOrderId — the just-created one must appear.
        var listResp = await client.GetAsync($"/api/wms/receipts?clientOrderId={clientOrderId}");
        listResp.EnsureSuccessStatusCode();
        var listBody = await listResp.Content.ReadAsStringAsync();
        listBody.Should().Contain("E4-SMOKE",
            "receipt with ReferenceNumber=E4-SMOKE must show up when filtering receipts by the hub's ClientOrderId");

        // 5) DB-level cross-check: every returned receipt has at least one line
        //    pointing at a declaration that points back at the ClientOrder.
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ourDeclIds = await ctx.CustomsDeclarations
            .IgnoreQueryFilters()
            .Where(d => d.ClientOrderId == clientOrderId)
            .Select(d => d.Id)
            .ToListAsync();
        ourDeclIds.Should().Contain(declarationId);

        var receiptsLinkedToOrder = await ctx.Receipts
            .IgnoreQueryFilters()
            .Where(r => r.Lines.Any(l => l.CustomsDeclarationId.HasValue && ourDeclIds.Contains(l.CustomsDeclarationId.Value)))
            .CountAsync();
        receiptsLinkedToOrder.Should().BeGreaterOrEqualTo(1);
    }

    // ----- helpers -----

    private async Task<(Guid partnerId, Guid lonAuthId)> GetCustomerAndAuthAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var partner = await ctx.Partners.IgnoreQueryFilters().FirstAsync();
        var auth = await ctx.LONAuthorizations.IgnoreQueryFilters().FirstAsync();
        return (partner.Id, auth.Id);
    }

    private async Task<(Guid procedureId, Guid itemId, Guid uomId, string tariffNumber)>
        LoadDeclarationSeedsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var procedure = await ctx.CustomsProcedures.IgnoreQueryFilters()
            .FirstAsync(p => p.Code == "4200" && p.IsActive);
        var item = await ctx.Items.IgnoreQueryFilters().FirstAsync();
        var uom = await ctx.UnitsOfMeasure.IgnoreQueryFilters().FirstAsync();
        var tariff = await ctx.TariffCodes.IgnoreQueryFilters().FirstAsync();
        return (procedure.Id, item.Id, uom.Id, tariff.TariffNumber);
    }

    private async Task<Guid> GetWarehouseIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var w = await ctx.Warehouses.IgnoreQueryFilters().FirstAsync();
        return w.Id;
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
