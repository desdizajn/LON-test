using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.Customs;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P4.1 — Zaverka / customs certification workflow.
///
/// Verifies:
///   * Draft declaration → certify → Status=Cleared + ZaverkaNumber/Date stamped.
///   * Empty ZaverkaNumber → 400.
///   * Second call on already-cleared declaration → 400.
///   * Another declaration using the same ZaverkaNumber → 400 (uniqueness guard).
/// </summary>
public class ZaverkaCertificationTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;
    public ZaverkaCertificationTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Certify_DraftDeclaration_StampsZaverkaAndMovesToCleared()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var declId = await CreateDraftDeclarationAsync();

        var zaverkaNo = $"Z-{Guid.NewGuid():N}"[..16];
        var zaverkaDate = DateTime.UtcNow.Date;

        var resp = await client.PostAsJsonAsync(
            $"/api/customs/declarations/{declId}/certify",
            new { zaverkaNumber = zaverkaNo, zaverkaDate });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"certify should succeed, body = {await resp.Content.ReadAsStringAsync()}");

        // DB state: Status=Cleared, ZaverkaNumber set, ClearedDate==ZaverkaDate
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var decl = await ctx.CustomsDeclarations
            .IgnoreQueryFilters()
            .FirstAsync(d => d.Id == declId);

        decl.Status.Should().Be(DeclarationStatus.Cleared);
        decl.IsCleared.Should().BeTrue();
        decl.ZaverkaNumber.Should().Be(zaverkaNo);
        decl.ZaverkaDate!.Value.Date.Should().Be(zaverkaDate);
        decl.ClearedDate!.Value.Date.Should().Be(zaverkaDate);
    }

    [Fact]
    public async Task Certify_EmptyZaverkaNumber_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var declId = await CreateDraftDeclarationAsync();

        var resp = await client.PostAsJsonAsync(
            $"/api/customs/declarations/{declId}/certify",
            new { zaverkaNumber = "", zaverkaDate = DateTime.UtcNow.Date });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Certify_AlreadyCleared_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var declId = await CreateDraftDeclarationAsync();
        var first = await client.PostAsJsonAsync($"/api/customs/declarations/{declId}/certify",
            new { zaverkaNumber = $"Z-{Guid.NewGuid():N}"[..16], zaverkaDate = DateTime.UtcNow.Date });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PostAsJsonAsync($"/api/customs/declarations/{declId}/certify",
            new { zaverkaNumber = $"Z-{Guid.NewGuid():N}"[..16], zaverkaDate = DateTime.UtcNow.Date });
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Certify_ZaverkaNumberReuse_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var declA = await CreateDraftDeclarationAsync();
        var declB = await CreateDraftDeclarationAsync();

        var shared = $"Z-SHARED-{Guid.NewGuid():N}"[..16];
        (await client.PostAsJsonAsync($"/api/customs/declarations/{declA}/certify",
            new { zaverkaNumber = shared, zaverkaDate = DateTime.UtcNow.Date }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var collide = await client.PostAsJsonAsync($"/api/customs/declarations/{declB}/certify",
            new { zaverkaNumber = shared, zaverkaDate = DateTime.UtcNow.Date });
        collide.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- helpers (mirror patterns in CustomsDeclarationTests.cs) ---

    private async Task<Guid> CreateDraftDeclarationAsync()
    {
        var (procedureId, lonAuthId, itemId, uomId, partnerId, tariffCode) = await LoadSeedIdsAsync("4200");

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenantId = await ctx.Tenants.Where(t => t.Code == "TEKSPORT").Select(t => t.Id).FirstAsync();

        var decl = new CustomsDeclaration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeclarationNumber = $"DEC-{Guid.NewGuid():N}"[..14],
            MRN = $"26MK{Guid.NewGuid():N}"[..18].ToUpperInvariant() + "A1",
            DeclarationDate = DateTime.UtcNow.Date,
            CustomsProcedureId = procedureId,
            PartnerId = partnerId,
            LONAuthorizationId = lonAuthId,
            DeclarationType = "IM",
            ProcedureCode = "4200",
            Currency = "EUR",
            TotalCustomsValue = 100m,
            Status = DeclarationStatus.Registered,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };
        ctx.CustomsDeclarations.Add(decl);
        await ctx.SaveChangesAsync();
        return decl.Id;
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
}
