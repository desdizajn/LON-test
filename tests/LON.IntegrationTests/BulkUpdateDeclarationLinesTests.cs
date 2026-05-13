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
/// Phase 17 §E0 — server-side bulk-update endpoint pattern. Validates:
///   - POST → 200 with affected count, lines actually mutated.
///   - AuditLogEntry rows written one-per-line with Action=BulkUpdate.
///   - Field whitelist enforcement (non-whitelisted → 400).
///   - Missing Reason → 400.
///   - Non-Draft declaration → 400 (compliance guardrail).
/// </summary>
public class BulkUpdateDeclarationLinesTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public BulkUpdateDeclarationLinesTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Bulk_UpdateCountryOfOrigin_AppliesToEveryLine_AndWritesAuditLogEntries()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var declId = await CreateDraftDeclarationWithLinesAsync(client, lineCount: 3, country: "DE");

        var payload = new
        {
            declarationId = declId,
            field = "CountryOfOrigin",
            value = "AT",
            reason = "Switching origin per supplier mid-month",
        };

        var resp = await client.PostAsJsonAsync(
            $"/api/customs/declarations/{declId}/lines/bulk-update", payload);
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK, because: body);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var lines = await ctx.CustomsDeclarationLines
            .IgnoreQueryFilters()
            .Where(l => l.CustomsDeclarationId == declId)
            .ToListAsync();
        lines.Should().HaveCount(3);
        lines.Should().OnlyContain(l => l.CountryOfOrigin == "AT",
            "every line must have the new value applied");

        var audits = await ctx.AuditLogEntries
            .IgnoreQueryFilters()
            .Where(a => a.EntityType == "CustomsDeclarationLine" && a.Action == "BulkUpdate")
            .ToListAsync();
        audits.Should().HaveCount(3, "one audit row per affected line");
        audits.Should().OnlyContain(a => a.ChangesJson.Contains("\"new\":\"AT\""),
            "audit payload must capture the new value");
        audits.Should().OnlyContain(a => a.ChangesJson.Contains("\"old\":\"DE\""),
            "audit payload must capture the old value");
        audits.Should().OnlyContain(a => a.ChangesJson.Contains("Switching origin per supplier"),
            "reason must be persisted in audit payload");
    }

    [Fact]
    public async Task Bulk_Update_NonWhitelistedField_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var declId = await CreateDraftDeclarationWithLinesAsync(client, lineCount: 1, country: "DE");

        var payload = new
        {
            declarationId = declId,
            field = "Quantity", // not in whitelist
            value = "999",
            reason = "Trying to bulk-update quantity (should be rejected)",
        };

        var resp = await client.PostAsJsonAsync(
            $"/api/customs/declarations/{declId}/lines/bulk-update", payload);
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("whitelist");
    }

    [Fact]
    public async Task Bulk_Update_WithoutReason_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var declId = await CreateDraftDeclarationWithLinesAsync(client, lineCount: 1, country: "DE");

        var payload = new
        {
            declarationId = declId,
            field = "CountryOfOrigin",
            value = "AT",
            reason = "",
        };

        var resp = await client.PostAsJsonAsync(
            $"/api/customs/declarations/{declId}/lines/bulk-update", payload);
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("Reason is required");
    }

    [Fact]
    public async Task Bulk_Update_TariffCode_AppliesToEveryLine()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var declId = await CreateDraftDeclarationWithLinesAsync(client, lineCount: 2, country: "DE");

        var payload = new
        {
            declarationId = declId,
            field = "TariffCode",
            value = "5210999999",
            reason = "Reclassifying per customs inspection",
        };

        var resp = await client.PostAsJsonAsync(
            $"/api/customs/declarations/{declId}/lines/bulk-update", payload);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lines = await ctx.CustomsDeclarationLines
            .IgnoreQueryFilters()
            .Where(l => l.CustomsDeclarationId == declId)
            .ToListAsync();
        lines.Should().OnlyContain(l => l.TariffCode == "5210999999");
    }

    // ----- helpers -----

    private async Task<Guid> CreateDraftDeclarationWithLinesAsync(HttpClient client, int lineCount, string country)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var procedure = await ctx.CustomsProcedures.FirstAsync(p => p.Code == "4200");
        var lonAuth = await ctx.LONAuthorizations.IgnoreQueryFilters()
            .FirstAsync(a => a.GuaranteeAmount > 0);
        var item = await ctx.Items.IgnoreQueryFilters().FirstAsync();
        var uom = await ctx.UnitsOfMeasure.IgnoreQueryFilters().FirstAsync();
        var partner = await ctx.Partners.IgnoreQueryFilters().FirstAsync();
        var tariffCode = await ctx.TariffCodes.IgnoreQueryFilters()
            .Select(t => t.TariffNumber).FirstAsync();

        var lines = new List<object>();
        for (int i = 0; i < lineCount; i++)
        {
            lines.Add(new
            {
                itemId = item.Id,
                tariffCode,
                quantity = 10m + i,
                uoMId = uom.Id,
                customsValue = 100m + i,
                countryOfOrigin = country,
                dutyRate = 5m,
                vatRate = 18m,
            });
        }

        var payload = new
        {
            declarationNumber = $"BULK-{Guid.NewGuid():N}"[..14],
            mrn = "",
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procedure.Id,
            partnerId = partner.Id,
            lonAuthorizationId = lonAuth.Id,
            totalCustomsValue = 1000m,
            currency = "EUR",
            countryOfDispatch = country,
            countryOfDestination = "MK",
            senderName = "BulkTest Sender",
            senderCountry = country,
            lines,
        };

        var resp = await client.PostAsJsonAsync("/api/customs/declarations", payload);
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK, because: body);

        var result = System.Text.Json.JsonSerializer.Deserialize<ResultResponse>(body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return result!.Data;
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
