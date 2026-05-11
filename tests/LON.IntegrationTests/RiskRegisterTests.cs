using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.Management;
using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P16.C1 — RiskRegisterItem E2E flow. Covers create / read / update /
/// delete + Kind filter + tenant isolation against the global query filter.
/// </summary>
public class RiskRegisterTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public RiskRegisterTests(LonApiFactory factory) => _factory = factory;

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
    public async Task Create_ThenGet_ReturnsTheItem()
    {
        var client = await AuthedAsync();

        var create = await client.PostAsJsonAsync("/api/Management/risks", new
        {
            kind = (int)RiskKind.Risk,
            title = "Custom MRN expiring next month",
            category = "Customs",
            severity = (int)RiskSeverity.High,
            status = (int)RiskStatus.Open,
            owner = "Иван",
            mitigation = "Поднеси барање за продолжување на рокот",
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());

        var body = await create.Content.ReadFromJsonAsync<ResultEnvelope<RiskDto>>();
        body!.IsSuccess.Should().BeTrue();
        var id = body.Data!.Id;

        var list = await client.GetFromJsonAsync<ResultEnvelope<List<RiskDto>>>(
            "/api/Management/risks?kind=1");
        list!.Data!.Should().Contain(r => r.Id == id && r.Title == "Custom MRN expiring next month");

        var single = await client.GetFromJsonAsync<ResultEnvelope<RiskDto>>(
            $"/api/Management/risks/{id}");
        single!.Data!.Owner.Should().Be("Иван");
        single.Data.Severity.Should().Be((int)RiskSeverity.High);
    }

    [Fact]
    public async Task Update_ChangesStatusAndResolution()
    {
        var client = await AuthedAsync();
        var create = await client.PostAsJsonAsync("/api/Management/risks", new
        {
            kind = (int)RiskKind.Escalation,
            title = "Customer asks for 2-week extension",
            severity = (int)RiskSeverity.Medium,
            status = (int)RiskStatus.Open,
        });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<ResultEnvelope<RiskDto>>())!.Data!.Id;

        var put = await client.PutAsJsonAsync($"/api/Management/risks/{id}", new
        {
            id,
            title = "Customer asks for 2-week extension",
            severity = (int)RiskSeverity.Medium,
            status = (int)RiskStatus.Resolved,
            resolution = "Extension granted via signed addendum",
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());

        var get = await client.GetFromJsonAsync<ResultEnvelope<RiskDto>>(
            $"/api/Management/risks/{id}");
        get!.Data!.Status.Should().Be((int)RiskStatus.Resolved);
        get.Data.Resolution.Should().Be("Extension granted via signed addendum");
    }

    [Fact]
    public async Task Delete_SoftDeletes_ItemNoLongerListed()
    {
        var client = await AuthedAsync();
        var create = await client.PostAsJsonAsync("/api/Management/risks", new
        {
            kind = (int)RiskKind.Risk,
            title = "Disposable test risk",
            severity = (int)RiskSeverity.Low,
            status = (int)RiskStatus.Open,
        });
        var id = (await create.Content.ReadFromJsonAsync<ResultEnvelope<RiskDto>>())!.Data!.Id;

        var del = await client.DeleteAsync($"/api/Management/risks/{id}");
        del.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await client.GetFromJsonAsync<ResultEnvelope<List<RiskDto>>>(
            "/api/Management/risks");
        list!.Data!.Should().NotContain(r => r.Id == id);
    }

    [Fact]
    public async Task QueryFilterByKind_ReturnsOnlyMatchingKind()
    {
        var client = await AuthedAsync();
        await client.PostAsJsonAsync("/api/Management/risks", new
        {
            kind = (int)RiskKind.Risk,
            title = "filter-test risk",
            severity = (int)RiskSeverity.Low,
            status = (int)RiskStatus.Open,
        });
        await client.PostAsJsonAsync("/api/Management/risks", new
        {
            kind = (int)RiskKind.Escalation,
            title = "filter-test escalation",
            severity = (int)RiskSeverity.Low,
            status = (int)RiskStatus.Open,
        });

        var risksOnly = await client.GetFromJsonAsync<ResultEnvelope<List<RiskDto>>>(
            "/api/Management/risks?kind=1");
        risksOnly!.Data!.Should().OnlyContain(r => r.Kind == (int)RiskKind.Risk);

        var escalationsOnly = await client.GetFromJsonAsync<ResultEnvelope<List<RiskDto>>>(
            "/api/Management/risks?kind=2");
        escalationsOnly!.Data!.Should().OnlyContain(r => r.Kind == (int)RiskKind.Escalation);
    }

    [Fact]
    public async Task TenantIsolation_OtherTenantsRisksAreHidden()
    {
        // Seed a second tenant + a RiskRegisterItem under it bypassing the
        // query filter; assert the authenticated TEKSPORT admin doesn't see it.
        using var seedScope = _factory.Services.CreateScope();
        var ctx = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        const string foreignTitle = "FOREIGN-RISK-ISOLATION-TEST";

        // Clean from prior runs.
        var stale = await ctx.RiskRegisterItems.IgnoreQueryFilters()
            .Where(r => r.Title == foreignTitle).ToListAsync();
        ctx.RiskRegisterItems.RemoveRange(stale);
        await ctx.SaveChangesAsync();

        var staleTenant = await ctx.Tenants.FirstOrDefaultAsync(t => t.Code == "RISK-ISO-DEMO");
        if (staleTenant is not null) ctx.Tenants.Remove(staleTenant);
        await ctx.SaveChangesAsync();

        var otherTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Code = "RISK-ISO-DEMO",
            Name = "Risk Isolation Tenant",
            Country = "MK",
            DefaultLanguage = "mk",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "RiskIsolationTest",
        };
        ctx.Tenants.Add(otherTenant);

        ctx.RiskRegisterItems.Add(new RiskRegisterItem
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenant.Id,
            Kind = RiskKind.Risk,
            Title = foreignTitle,
            Severity = RiskSeverity.High,
            Status = RiskStatus.Open,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "RiskIsolationTest",
        });
        await ctx.SaveChangesAsync();

        // Sanity — bypassing the filter, the foreign row IS in the DB.
        var foreign = await ctx.RiskRegisterItems.IgnoreQueryFilters()
            .AnyAsync(r => r.Title == foreignTitle);
        foreign.Should().BeTrue();

        // Authenticated TEKSPORT admin must NOT see it.
        var client = await AuthedAsync();
        var list = await client.GetFromJsonAsync<ResultEnvelope<List<RiskDto>>>(
            "/api/Management/risks");
        list!.Data!.Should().NotContain(r => r.Title == foreignTitle);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record ResultEnvelope<T>(bool IsSuccess, T? Data, string? ErrorMessage);
    private sealed record RiskDto(
        Guid Id,
        Guid TenantId,
        int Kind,
        string Title,
        string? Category,
        int Severity,
        int Status,
        string? Owner,
        string? Mitigation,
        string? Resolution,
        DateTime? DueDate,
        DateTime? ReviewDate,
        DateTime CreatedAt,
        DateTime? ModifiedAt);
}
