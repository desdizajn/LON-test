using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Application.Finance.FxRates;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// Phase 17 §E16 — FxRate CRUD + GetRateAsync resolution (direct / inverse /
/// cross-via-EUR).
/// </summary>
public class FxRateTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;
    public FxRateTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Migration_SeedsThreeRatesPerTenant()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var perTenant = await ctx.FxRates.IgnoreQueryFilters()
            .GroupBy(r => r.TenantId)
            .Select(g => new { Tenant = g.Key, Count = g.Count() })
            .ToListAsync();
        perTenant.Should().NotBeEmpty();
        perTenant.Should().OnlyContain(p => p.Count >= 3,
            "migration seeds EUR/MKD + USD/MKD + USD/EUR per tenant");
    }

    [Fact]
    public async Task GetRate_ExactPair_ReturnsLatestEffectiveRate()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IFxRateService>();
        var rate = await svc.GetRateAsync("EUR", "MKD", DateTime.UtcNow);
        rate.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetRate_SameCurrency_ReturnsOne()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IFxRateService>();
        var rate = await svc.GetRateAsync("EUR", "EUR", DateTime.UtcNow);
        rate.Should().Be(1m);
    }

    [Fact]
    public async Task GetRate_InverseFallback_Inverts()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IFxRateService>();
        var mkdToEur = await svc.GetRateAsync("MKD", "EUR", DateTime.UtcNow);
        var eurToMkd = await svc.GetRateAsync("EUR", "MKD", DateTime.UtcNow);
        (mkdToEur * eurToMkd).Should().BeApproximately(1m, 0.000001m);
    }

    [Fact]
    public async Task GetRate_CrossViaEur_ResolvesMkdToUsd()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IFxRateService>();
        var rate = await svc.GetRateAsync("MKD", "USD", DateTime.UtcNow);
        rate.Should().BeGreaterThan(0,
            "cross via EUR should resolve MKD→USD even when no direct pair exists");
    }

    [Fact]
    public async Task Create_DuplicateFromToOnSameDate_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var today = DateTime.UtcNow.Date;
        var dup = await client.PostAsJsonAsync("/api/Finance/fx-rates", new
        {
            fromCurrency = "EUR",
            toCurrency = "MKD",
            rate = 99m,
            effectiveDate = today,
            source = 1,
        });
        dup.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the migration already seeded EUR/MKD for today — duplicate must reject");
    }

    [Fact]
    public async Task Create_NewPairFutureDate_Persists()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var resp = await client.PostAsJsonAsync("/api/Finance/fx-rates", new
        {
            fromCurrency = "GBP",
            toCurrency = "MKD",
            rate = 70.0m,
            effectiveDate = DateTime.UtcNow.Date.AddDays(1),
            source = 1,
            notes = "test future GBP rate",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
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
}
