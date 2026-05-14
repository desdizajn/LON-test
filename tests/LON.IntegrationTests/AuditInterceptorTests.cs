using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.Customs;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// Phase 17 §E13 — confirms the SaveChanges-time audit capture writes one
/// AuditLogEntry per modification to an IAuditable entity, with the
/// admin /api/audit endpoint serving the rows.
/// </summary>
public class AuditInterceptorTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;
    public AuditInterceptorTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CreatingClientOrder_WritesCreateAuditEntry()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();
        var resp = await client.PostAsJsonAsync("/api/clientorders", new
        {
            customerPartnerId = partnerId,
            lonAuthorizationId = lonAuthId,
            customerOrderReference = "E13-AUDIT-CREATE",
            orderDate = DateTime.UtcNow.Date,
        });
        resp.EnsureSuccessStatusCode();
        var co = await resp.Content.ReadFromJsonAsync<ResultGuid>();
        co!.Data.Should().NotBe(Guid.Empty);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entries = await ctx.AuditLogEntries.IgnoreQueryFilters()
            .Where(a => a.EntityType == nameof(ClientOrder) && a.EntityId == co.Data)
            .ToListAsync();
        entries.Should().NotBeEmpty();
        entries.Should().Contain(e => e.Action == "Create");
    }

    [Fact]
    public async Task UpdatingClientOrder_WritesUpdateAuditEntryWithFieldDiff()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();
        var createResp = await client.PostAsJsonAsync("/api/clientorders", new
        {
            customerPartnerId = partnerId,
            lonAuthorizationId = lonAuthId,
            customerOrderReference = "E13-AUDIT-UPDATE-INIT",
            orderDate = DateTime.UtcNow.Date,
        });
        createResp.EnsureSuccessStatusCode();
        var co = await createResp.Content.ReadFromJsonAsync<ResultGuid>();
        var coId = co!.Data;

        var updateResp = await client.PutAsJsonAsync($"/api/clientorders/{coId}", new
        {
            id = coId,
            customerPartnerId = partnerId,
            lonAuthorizationId = lonAuthId,
            customerOrderReference = "E13-AUDIT-UPDATE-FINAL",
            orderDate = DateTime.UtcNow.Date,
            notes = "post-update note",
        });
        updateResp.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entries = await ctx.AuditLogEntries.IgnoreQueryFilters()
            .Where(a => a.EntityType == nameof(ClientOrder)
                        && a.EntityId == coId
                        && a.Action == "Update")
            .ToListAsync();
        entries.Should().NotBeEmpty(
            "updating a ClientOrder must produce an Update audit row");
        entries[0].ChangesJson.Should().Contain("CustomerOrderReference");
    }

    [Fact]
    public async Task AdminEndpoint_FiltersByEntityTypeAndId()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();
        var resp = await client.PostAsJsonAsync("/api/clientorders", new
        {
            customerPartnerId = partnerId,
            lonAuthorizationId = lonAuthId,
            customerOrderReference = "E13-AUDIT-FILTER",
            orderDate = DateTime.UtcNow.Date,
        });
        resp.EnsureSuccessStatusCode();
        var co = await resp.Content.ReadFromJsonAsync<ResultGuid>();

        var auditResp = await client.GetAsync(
            $"/api/audit?entityType={nameof(ClientOrder)}&entityId={co!.Data}");
        auditResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await auditResp.Content.ReadAsStringAsync();
        body.Should().Contain("Create");
        body.Should().Contain(co.Data.ToString());
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

    private async Task<(Guid partnerId, Guid lonAuthId)> GetCustomerAndAuthAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var partner = await ctx.Partners.IgnoreQueryFilters().FirstAsync();
        var auth = await ctx.LONAuthorizations.IgnoreQueryFilters().FirstAsync();
        return (partner.Id, auth.Id);
    }

    private sealed record LoginResp(string AccessToken);
    private sealed record ResultGuid(bool IsSuccess, Guid Data, string? ErrorMessage);
}
