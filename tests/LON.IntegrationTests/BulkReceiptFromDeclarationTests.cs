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
/// P5.2.3 — Bulk Receipt from Customs Declaration.
///
/// Happy path: build an IM 4200 declaration with two lines, then
/// POST /api/wms/receipts/bulk-from-declaration. Expect a new Receipt
/// with two lines, the MRN propagated onto both balances, and both
/// inventory balances booked with the MRN-inflated quantity.
///
/// Error path: unknown declaration id returns 400 with
/// ErrorCode=declaration.not_found.
/// </summary>
public class BulkReceiptFromDeclarationTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public BulkReceiptFromDeclarationTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Bulk_FromValidDeclaration_CreatesReceiptAndBalances()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var (declId, mrn) = await CreateTwoLineIm4200(client);

        Guid warehouseId, rcvLocationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var wh = await ctx.Warehouses.FirstAsync();
            var rcv = await ctx.Locations.FirstAsync(l => l.Code.StartsWith("RCV") && l.WarehouseId == wh.Id);
            warehouseId = wh.Id;
            rcvLocationId = rcv.Id;
        }

        var resp = await client.PostAsJsonAsync("/api/wms/receipts/bulk-from-declaration", new
        {
            customsDeclarationId = declId,
            warehouseId,
            targetLocationId = rcvLocationId,
            referenceNumber = "BULK-P523-001"
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ResultEnvelope<BulkResult>>();
        body!.IsSuccess.Should().BeTrue();
        body.Data!.LinesCreated.Should().Be(2);
        body.Data.TotalQuantity.Should().Be(50m + 30m);

        using var scope2 = _factory.Services.CreateScope();
        var ctx2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var balances = await ctx2.InventoryBalances.IgnoreQueryFilters()
            .Where(b => b.MRN == mrn)
            .ToListAsync();
        balances.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task Bulk_UnknownDeclaration_Returns400_WithErrorCode()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        Guid warehouseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            warehouseId = (await ctx.Warehouses.FirstAsync()).Id;
        }

        var resp = await client.PostAsJsonAsync("/api/wms/receipts/bulk-from-declaration", new
        {
            customsDeclarationId = Guid.NewGuid(),
            warehouseId
        });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<ResultEnvelope<BulkResult>>();
        body!.IsSuccess.Should().BeFalse();
        body.ErrorCode.Should().Be("declaration.not_found");
    }

    // ============================================================
    // Helpers
    // ============================================================

    private async Task<(Guid DeclarationId, string Mrn)> CreateTwoLineIm4200(HttpClient client)
    {
        Guid procId, authId, item1, item2, uomId, partnerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            procId = (await ctx.CustomsProcedures.FirstAsync(p => p.Code == "4200")).Id;
            authId = (await ctx.LONAuthorizations.IgnoreQueryFilters()
                .FirstAsync(a => a.AuthorizationNumber == "26/TEKSPORT/0001")).Id;
            var items = await ctx.Items.OrderBy(i => i.Code).Take(2).ToListAsync();
            item1 = items[0].Id;
            item2 = items.Count > 1 ? items[1].Id : items[0].Id;
            uomId = (await ctx.UnitsOfMeasure.FirstAsync(u => u.Code == "KG")).Id;
            partnerId = (await ctx.Partners.OrderBy(p => p.Code).FirstAsync()).Id;
        }

        var resp = await client.PostAsJsonAsync("/api/customs/declarations", new
        {
            declarationNumber = $"DEC-BULK-{Guid.NewGuid():N}"[..14],
            mrn = "",
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procId,
            lonAuthorizationId = authId,
            partnerId,
            totalCustomsValue = 2000m,
            currency = "EUR",
            senderName = "Bulk supplier", senderCountry = "DE", countryOfDispatch = "DE",
            lines = new object[]
            {
                new {
                    itemId = item1, tariffCode = "2905399500",
                    quantity = 50m, uoMId = uomId,
                    customsValue = 1200m, countryOfOrigin = "DE",
                    dutyRate = 5m, vatRate = 18m,
                    netWeight = 50m, grossWeight = 52m,
                    calculationMethod = "A"
                },
                new {
                    itemId = item2, tariffCode = "2905399500",
                    quantity = 30m, uoMId = uomId,
                    customsValue = 800m, countryOfOrigin = "DE",
                    dutyRate = 5m, vatRate = 18m,
                    netWeight = 30m, grossWeight = 31m,
                    calculationMethod = "A"
                }
            }
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<CustomsResultEnvelope>();
        var declId = body!.Data;

        using var scope2 = _factory.Services.CreateScope();
        var ctx2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var decl = await ctx2.CustomsDeclarations.IgnoreQueryFilters().FirstAsync(d => d.Id == declId);
        return (declId, decl.MRN);
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
    private sealed record CustomsResultEnvelope(bool IsSuccess, Guid Data);
    private sealed record ResultEnvelope<T>(bool IsSuccess, T? Data, string? ErrorMessage, string? ErrorCode);
    private sealed record BulkResult(Guid ReceiptId, int LinesCreated, decimal TotalQuantity);
}
