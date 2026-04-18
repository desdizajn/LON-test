using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.Customs;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P2.1 — IM 42 00 end-to-end flow: create declaration → MRN assigned →
/// registered in MRNRegistry → event emitted (implicit via successful save).
/// Compliance gates: LONAuthorization enforcement, currency/country ISO,
/// status lifecycle, auto-MRN fallback.
/// </summary>
public class CustomsDeclarationTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public CustomsDeclarationTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_IM4200_WithValidLONAuth_ReturnsOk_AndRegistersMRN()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (procedureId, lonAuthId, itemId, uomId, partnerId, tariffCode) =
            await LoadSeedIdsAsync(expectedProcedureCode: "4200");

        var payload = new
        {
            declarationNumber = $"DEC-{Guid.NewGuid():N}"[..12],
            mrn = "", // auto-generate
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procedureId,
            partnerId,
            lonAuthorizationId = lonAuthId,
            totalCustomsValue = 1000m,
            currency = "EUR",
            countryOfDispatch = "DE",
            countryOfDestination = "MK",
            senderName = "Fabric Supplier GmbH",
            senderCountry = "DE",
            lines = new[]
            {
                new
                {
                    itemId,
                    tariffCode,
                    quantity = 100m,
                    uoMId = uomId,
                    customsValue = 1000m,
                    countryOfOrigin = "DE",
                    dutyRate = 5m,
                    vatRate = 18m,
                }
            }
        };

        var resp = await client.PostAsJsonAsync("/api/customs/declarations", payload);
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK, because: body);

        var result = System.Text.Json.JsonSerializer.Deserialize<ResultResponse>(body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        result!.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);

        // DB assertions — declaration + MRN registry.
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await ctx.CustomsDeclarations
            .IgnoreQueryFilters()
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == result.Data);
        saved.Should().NotBeNull();
        saved!.ProcedureCode.Should().Be("4200");
        saved.LONAuthorizationId.Should().Be(lonAuthId);
        saved.Status.Should().Be(DeclarationStatus.Registered);
        saved.MRN.Should().MatchRegex(@"^\d{2}MK[0-9A-F]{8}A1$",
            "auto-generated MRN must match YYMK<8hex>A1");
        saved.TotalDuty.Should().Be(50m, "5% of 1000 customs value");
        saved.TotalVAT.Should().Be(189m, "(1000 + 50) * 18% = 189");

        var registry = await ctx.MRNRegistries
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.MRN == saved.MRN);
        registry.Should().NotBeNull("MRN must be registered for downstream tracking");
        registry!.TotalQuantity.Should().Be(100m);
        registry.UsedQuantity.Should().Be(0m);
        registry.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_IM4200_WithoutLONAuth_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (procedureId, _, itemId, uomId, partnerId, tariffCode) =
            await LoadSeedIdsAsync(expectedProcedureCode: "4200");

        var payload = BuildMinimalPayload(procedureId, lonAuthId: null,
            itemId, uomId, partnerId, tariffCode, currency: "EUR");

        var resp = await client.PostAsJsonAsync("/api/customs/declarations", payload);
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("LONAuthorizationId is required",
            "handler must block IM 4200 without a LON authorization");
    }

    [Fact]
    public async Task Create_IM4200_WithInvalidCurrency_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (procedureId, lonAuthId, itemId, uomId, partnerId, tariffCode) =
            await LoadSeedIdsAsync(expectedProcedureCode: "4200");

        var payload = BuildMinimalPayload(procedureId, lonAuthId,
            itemId, uomId, partnerId, tariffCode, currency: "XYZ");

        var resp = await client.PostAsJsonAsync("/api/customs/declarations", payload);
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("XYZ", "the rejection must mention the bogus currency code");
    }

    [Fact]
    public async Task Create_IM4200_WithExplicitMRN_UsesProvidedValue()
    {
        const string providedMrn = "26MKTEST0001EX99A1";

        var client = _factory.CreateClient();
        await Authenticate(client);

        var (procedureId, lonAuthId, itemId, uomId, partnerId, tariffCode) =
            await LoadSeedIdsAsync(expectedProcedureCode: "4200");

        var payload = new
        {
            declarationNumber = $"DEC-{Guid.NewGuid():N}"[..12],
            mrn = providedMrn,
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procedureId,
            partnerId,
            lonAuthorizationId = lonAuthId,
            totalCustomsValue = 500m,
            currency = "EUR",
            countryOfDispatch = "IT",
            senderName = "Italian Sender",
            senderCountry = "IT",
            lines = new[]
            {
                new
                {
                    itemId, tariffCode,
                    quantity = 50m, uoMId = uomId,
                    customsValue = 500m, countryOfOrigin = "IT",
                    dutyRate = 0m, vatRate = 18m
                }
            }
        };

        var resp = await client.PostAsJsonAsync("/api/customs/declarations", payload);
        resp.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await ctx.CustomsDeclarations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.MRN == providedMrn);
        saved.Should().NotBeNull("provided MRN must be preserved (uppercased)");
    }

    private async Task<(Guid procedureId, Guid lonAuthId, Guid itemId, Guid uomId,
        Guid partnerId, string tariffCode)> LoadSeedIdsAsync(string expectedProcedureCode)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var proc = await ctx.CustomsProcedures.FirstAsync(p => p.Code == expectedProcedureCode);
        var auth = await ctx.LONAuthorizations.IgnoreQueryFilters()
            .OrderBy(a => a.AuthorizationNumber)
            .FirstAsync();
        var item = await ctx.Items.FirstAsync();
        var uom = await ctx.UnitsOfMeasure.FirstAsync(u => u.Code == "KG");
        var partner = await ctx.Partners.OrderBy(p => p.Code).FirstAsync();
        var tariff = await ctx.TariffCodes.Where(t => t.IsActive)
            .OrderBy(t => t.TariffNumber).FirstAsync();

        return (proc.Id, auth.Id, item.Id, uom.Id, partner.Id, tariff.TariffNumber);
    }

    private static object BuildMinimalPayload(Guid procedureId, Guid? lonAuthId,
        Guid itemId, Guid uomId, Guid partnerId, string tariffCode, string currency,
        string senderName = "Sender GmbH", string senderCountry = "DE")
    {
        return new
        {
            declarationNumber = $"DEC-{Guid.NewGuid():N}"[..12],
            mrn = "",
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procedureId,
            partnerId,
            lonAuthorizationId = lonAuthId,
            totalCustomsValue = 100m,
            currency,
            countryOfDispatch = "DE",
            senderName,
            senderCountry,
            lines = new[]
            {
                new
                {
                    itemId, tariffCode,
                    quantity = 10m, uoMId = uomId,
                    customsValue = 100m, countryOfOrigin = "DE",
                    dutyRate = 5m, vatRate = 18m
                }
            }
        };
    }

    private async Task Authenticate(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record ResultResponse(bool IsSuccess, Guid Data, string? ErrorMessage);
}
