using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.Finance;
using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>P16.C3.a — CostRate CRUD + scope filter + tenant isolation.</summary>
public class CostRateTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public CostRateTests(LonApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthedAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Create_ThenList_ReturnsTheRow()
    {
        var client = await AuthedAsync();
        var create = await client.PostAsJsonAsync("/api/Finance/cost-rates", new
        {
            scope = (int)CostRateScope.Machine,
            scopeId = Guid.NewGuid(),
            costPerHour = 12.50m,
            currency = "EUR",
            validFrom = DateTime.UtcNow.Date,
            notes = "Sewing machine A",
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());

        var list = await client.GetFromJsonAsync<ResultEnvelope<List<CostRateDtoRow>>>(
            "/api/Finance/cost-rates");
        list!.Data!.Should().Contain(r => r.Notes == "Sewing machine A" && r.CostPerHour == 12.50m);
    }

    [Fact]
    public async Task RequiresAtLeastOneRate()
    {
        var client = await AuthedAsync();
        var create = await client.PostAsJsonAsync("/api/Finance/cost-rates", new
        {
            scope = (int)CostRateScope.Operator,
            currency = "EUR",
            validFrom = DateTime.UtcNow.Date,
        });
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ScopeFilter_ReturnsOnlyMatchingScope()
    {
        var client = await AuthedAsync();
        await client.PostAsJsonAsync("/api/Finance/cost-rates", new
        {
            scope = (int)CostRateScope.WorkCenter,
            costPerUnit = 0.05m,
            currency = "EUR",
            validFrom = DateTime.UtcNow.Date,
            notes = "scope-filter wc",
        });
        await client.PostAsJsonAsync("/api/Finance/cost-rates", new
        {
            scope = (int)CostRateScope.Shift,
            costPerHour = 9.0m,
            currency = "EUR",
            validFrom = DateTime.UtcNow.Date,
            notes = "scope-filter shift",
        });

        var wcOnly = await client.GetFromJsonAsync<ResultEnvelope<List<CostRateDtoRow>>>(
            "/api/Finance/cost-rates?scope=5"); // WorkCenter
        wcOnly!.Data!.Should().OnlyContain(r => r.Scope == (int)CostRateScope.WorkCenter);
        wcOnly.Data.Should().Contain(r => r.Notes == "scope-filter wc");
    }

    [Fact]
    public async Task Update_ChangesRate()
    {
        var client = await AuthedAsync();
        var create = await client.PostAsJsonAsync("/api/Finance/cost-rates", new
        {
            scope = (int)CostRateScope.Operation,
            costPerUnit = 1.0m,
            currency = "EUR",
            validFrom = DateTime.UtcNow.Date,
        });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<ResultEnvelope<CostRateDtoRow>>())!.Data!.Id;

        var put = await client.PutAsJsonAsync($"/api/Finance/cost-rates/{id}", new
        {
            id,
            scope = (int)CostRateScope.Operation,
            costPerUnit = 1.25m,
            currency = "EUR",
            validFrom = DateTime.UtcNow.Date,
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await client.GetFromJsonAsync<ResultEnvelope<List<CostRateDtoRow>>>(
            "/api/Finance/cost-rates");
        list!.Data!.First(r => r.Id == id).CostPerUnit.Should().Be(1.25m);
    }

    [Fact]
    public async Task Delete_SoftDeletes_NoLongerListed()
    {
        var client = await AuthedAsync();
        var create = await client.PostAsJsonAsync("/api/Finance/cost-rates", new
        {
            scope = (int)CostRateScope.Machine,
            costPerHour = 5.0m,
            currency = "USD",
            validFrom = DateTime.UtcNow.Date,
        });
        var id = (await create.Content.ReadFromJsonAsync<ResultEnvelope<CostRateDtoRow>>())!.Data!.Id;

        var del = await client.DeleteAsync($"/api/Finance/cost-rates/{id}");
        del.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await client.GetFromJsonAsync<ResultEnvelope<List<CostRateDtoRow>>>(
            "/api/Finance/cost-rates");
        list!.Data!.Should().NotContain(r => r.Id == id);
    }

    [Fact]
    public async Task TenantIsolation_OtherTenantsRatesAreHidden()
    {
        using var seedScope = _factory.Services.CreateScope();
        var ctx = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        const string marker = "FOREIGN-COSTRATE-ISOLATION";

        var stale = await ctx.CostRates.IgnoreQueryFilters()
            .Where(c => c.Notes == marker).ToListAsync();
        ctx.CostRates.RemoveRange(stale);
        await ctx.SaveChangesAsync();

        var staleTenant = await ctx.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Code == "COSTRATE-ISO-DEMO");
        if (staleTenant is not null) ctx.Tenants.Remove(staleTenant);
        await ctx.SaveChangesAsync();

        var otherTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Code = "COSTRATE-ISO-DEMO",
            Name = "CostRate Isolation Tenant",
            Country = "MK",
            DefaultLanguage = "mk",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "CostRateIsoTest",
        };
        ctx.Tenants.Add(otherTenant);

        ctx.CostRates.Add(new CostRate
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenant.Id,
            Scope = CostRateScope.Machine,
            CostPerHour = 99m,
            Currency = "EUR",
            ValidFrom = DateTime.UtcNow.Date,
            Notes = marker,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "CostRateIsoTest",
        });
        await ctx.SaveChangesAsync();

        var client = await AuthedAsync();
        var list = await client.GetFromJsonAsync<ResultEnvelope<List<CostRateDtoRow>>>(
            "/api/Finance/cost-rates");
        list!.Data!.Should().NotContain(r => r.Notes == marker);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record ResultEnvelope<T>(bool IsSuccess, T? Data, string? ErrorMessage);
    private sealed record CostRateDtoRow(
        Guid Id, Guid TenantId, int Scope, Guid? ScopeId,
        decimal? CostPerHour, decimal? CostPerUnit,
        string Currency, DateTime ValidFrom, DateTime? ValidTo, string? Notes);
}
