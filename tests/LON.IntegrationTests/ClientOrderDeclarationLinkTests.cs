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
/// Phase 17 §E3 — wiring IM declaration creation from the ClientOrder hub.
/// Validates:
///   1. Empty DeclarationNumber → auto-generated IM-{year}-{seq:D6} via SEQUENCE.
///   2. ClientOrderId persists on CustomsDeclaration.
///   3. First declaration linked to a Draft ClientOrder transitions status → Active.
///   4. Two parallel hub-driven creates yield distinct declaration numbers (SEQUENCE atomicity).
/// </summary>
public class ClientOrderDeclarationLinkTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public ClientOrderDeclarationLinkTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_FromHub_AutoNumbers_LinksClientOrder_AndTransitionsToActive()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();
        var (procedureId, itemId, uomId, tariffCode) = await LoadDeclarationSeedsAsync();

        // 1) Create a ClientOrder via the §E1 endpoint.
        var orderResp = await client.PostAsJsonAsync("/api/clientorders", new
        {
            customerPartnerId = partnerId,
            lonAuthorizationId = lonAuthId,
            customerOrderReference = "E3-LINK-TEST",
            orderDate = DateTime.UtcNow.Date,
        });
        orderResp.EnsureSuccessStatusCode();
        var orderResult = await orderResp.Content.ReadFromJsonAsync<ResultGuid>();
        var clientOrderId = orderResult!.Data;

        // 2) Create an IM declaration linked to that ClientOrder, with an
        //    EMPTY declarationNumber so the SEQUENCE generates one.
        var declResp = await client.PostAsJsonAsync("/api/customs/declarations", new
        {
            declarationNumber = "",           // <-- empty triggers auto-numbering
            mrn = "",                         // auto-generate dev MRN
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procedureId,
            partnerId,
            lonAuthorizationId = lonAuthId,
            clientOrderId,                    // <-- NEW: hub linkage
            totalCustomsValue = 500m,
            currency = "EUR",
            countryOfDispatch = "IT",
            countryOfDestination = "MK",
            senderName = "E3 Italian Sender",
            senderCountry = "IT",
            lines = new[]
            {
                new
                {
                    itemId, tariffCode,
                    quantity = 50m, uoMId = uomId,
                    customsValue = 500m, countryOfOrigin = "IT",
                    dutyRate = 5m, vatRate = 18m,
                }
            }
        });
        var declBody = await declResp.Content.ReadAsStringAsync();
        declResp.StatusCode.Should().Be(HttpStatusCode.OK, because: declBody);
        var declResult = System.Text.Json.JsonSerializer.Deserialize<ResultGuid>(declBody,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var decl = await ctx.CustomsDeclarations.IgnoreQueryFilters()
            .FirstAsync(d => d.Id == declResult!.Data);
        decl.DeclarationNumber.Should().MatchRegex(@"^IM-\d{4}-\d{6}$",
            "empty DeclarationNumber must be auto-filled via seq_IMDeclaration_<tenant>");
        decl.ClientOrderId.Should().Be(clientOrderId,
            "the ClientOrderId from the hub must be persisted");

        var order = await ctx.ClientOrders.IgnoreQueryFilters().FirstAsync(o => o.Id == clientOrderId);
        order.Status.Should().Be(ClientOrderStatus.Active,
            "first declaration on a Draft ClientOrder must trigger status → Active");
    }

    [Fact]
    public async Task TwoParallelCreates_FromSameHub_YieldDistinctIMDeclarationNumbers()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();
        var (procedureId, itemId, uomId, tariffCode) = await LoadDeclarationSeedsAsync();

        // One ClientOrder shared by both declarations.
        var orderResp = await client.PostAsJsonAsync("/api/clientorders", new
        {
            customerPartnerId = partnerId,
            lonAuthorizationId = lonAuthId,
            customerOrderReference = "E3-PARALLEL",
            orderDate = DateTime.UtcNow.Date,
        });
        orderResp.EnsureSuccessStatusCode();
        var clientOrderId = (await orderResp.Content.ReadFromJsonAsync<ResultGuid>())!.Data;

        // Fire 2 concurrent IM creates against the SAME hub.
        async Task<Guid> CreateAsync(int index)
        {
            var resp = await client.PostAsJsonAsync("/api/customs/declarations", new
            {
                declarationNumber = "",
                mrn = "",
                declarationDate = DateTime.UtcNow.Date,
                customsProcedureId = procedureId,
                partnerId, lonAuthorizationId = lonAuthId, clientOrderId,
                totalCustomsValue = 100m, currency = "EUR",
                senderName = $"Parallel #{index}", senderCountry = "DE", countryOfDispatch = "DE",
                lines = new[] {
                    new { itemId, tariffCode, quantity = 10m, uoMId = uomId,
                        customsValue = 100m, countryOfOrigin = "DE", dutyRate = 0m, vatRate = 18m }
                }
            });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<ResultGuid>())!.Data;
        }

        var ids = await Task.WhenAll(CreateAsync(1), CreateAsync(2));

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var numbers = await ctx.CustomsDeclarations.IgnoreQueryFilters()
            .Where(d => ids.Contains(d.Id))
            .Select(d => d.DeclarationNumber)
            .ToListAsync();
        numbers.Should().HaveCount(2);
        numbers.Distinct().Should().HaveCount(2,
            "SQL SEQUENCE seq_IMDeclaration_<tenant> must yield distinct values under concurrent hub creates");
        numbers.Should().AllSatisfy(n => n.Should().MatchRegex(@"^IM-\d{4}-\d{6}$"));
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

    private async Task<(Guid procedureId, Guid itemId, Guid uomId, string tariffCode)>
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
