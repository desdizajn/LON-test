using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P15.4 — NaimU5 rollup integration test. Verifies that a declaration
/// with multiple lines that share (TariffCode, UoM, Country) collapses
/// into a single naim row with summed quantity / value / duty / VAT
/// and a weighted-average duty rate.
/// </summary>
public class DeclarationNaimTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public DeclarationNaimTests(LonApiFactory factory) => _factory = factory;

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
    public async Task NaimRollup_CollapsesSameTariffOriginLines()
    {
        var client = await AuthedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var item = await db.Items.FirstAsync(i => !i.IsDeleted);
        var uom = await db.UnitsOfMeasure.FirstAsync(u => !u.IsDeleted);
        // Prefer a non-LON procedure so we don't have to wire up a LON auth.
        var proc = await db.CustomsProcedures
            .FirstAsync(p => p.IsActive && !p.RequiresGuarantee);

        var declResp = await client.PostAsJsonAsync("/api/customs/declarations", new
        {
            declarationNumber = $"P15-4-{Guid.NewGuid():N}".Substring(0, 20),
            declarationDate = DateTime.UtcNow,
            customsProcedureId = proc.Id,
            totalCustomsValue = 1000m,
            currency = "EUR",
            lines = new object[]
            {
                // Two lines that should collapse (same tariff+uom+country)
                new { itemId = item.Id, tariffCode = "6109100010", quantity = 10m, uoMId = uom.Id,
                      customsValue = 300m, countryOfOrigin = "TR", dutyRate = 10m, vatRate = 18m,
                      grossWeight = 12m, netWeight = 10m },
                new { itemId = item.Id, tariffCode = "6109100010", quantity = 20m, uoMId = uom.Id,
                      customsValue = 700m, countryOfOrigin = "TR", dutyRate = 12m, vatRate = 18m,
                      grossWeight = 24m, netWeight = 20m },
                // Distinct group: same tariff, different country
                new { itemId = item.Id, tariffCode = "6109100010", quantity = 5m, uoMId = uom.Id,
                      customsValue = 150m, countryOfOrigin = "IT", dutyRate = 0m, vatRate = 18m,
                      grossWeight = 6m, netWeight = 5m },
            }
        });
        declResp.StatusCode.Should().Be(HttpStatusCode.OK, await declResp.Content.ReadAsStringAsync());
        var declId = await declResp.Content.ReadFromJsonAsync<ResultEnvelope>();
        declId!.Data.Should().NotBeEmpty();

        var rows = await client.GetFromJsonAsync<List<NaimRow>>(
            $"/api/customs/declarations/{declId.Data}/naim");
        rows.Should().HaveCount(2, "TR group collapses 2 lines, IT group stays separate");

        var tr = rows!.Single(r => r.CountryOfOrigin == "TR");
        tr.NaimNumber.Should().Be(1); // TariffCode tied — ordered by country
        tr.TotalQuantity.Should().Be(30m);
        tr.TotalCustomsValue.Should().Be(1000m);
        tr.TotalGrossWeight.Should().Be(36m);
        tr.TotalNetWeight.Should().Be(30m);
        tr.LineCount.Should().Be(2);
        // Weighted duty = (10 × 300 + 12 × 700) / 1000 = (3000 + 8400)/1000 = 11.4
        tr.WeightedAverageDutyRate.Should().Be(11.4m);
        tr.WeightedAverageVATRate.Should().Be(18m);
        // Duty amount = 300 × 10% + 700 × 12% = 30 + 84 = 114
        tr.TotalDutyAmount.Should().Be(114m);

        var it = rows!.Single(r => r.CountryOfOrigin == "IT");
        it.TotalQuantity.Should().Be(5m);
        it.LineCount.Should().Be(1);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record ResultEnvelope(Guid Data, bool IsSuccess, string? ErrorMessage);
    private sealed record NaimRow(
        int NaimNumber,
        string? TariffCode,
        Guid UoMId,
        string UoMCode,
        string? CountryOfOrigin,
        decimal TotalQuantity,
        decimal TotalCustomsValue,
        decimal? TotalGrossWeight,
        decimal? TotalNetWeight,
        decimal TotalDutyAmount,
        decimal TotalVATAmount,
        decimal TotalOtherCharges,
        decimal WeightedAverageDutyRate,
        decimal WeightedAverageVATRate,
        int LineCount,
        List<int> LineNumbers);
}
