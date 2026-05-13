using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// Phase 17 §E11 — verify the SaveChangesAsync dispatcher persists one
/// DomainEventLog row per emitted IDomainEvent, with stable EventId +
/// EventType + serialised payload.
/// </summary>
public class DomainEventLogTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;
    public DomainEventLogTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CreatingClientOrder_PersistsClientOrderCreatedEvent()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();
        var resp = await client.PostAsJsonAsync("/api/clientorders", new
        {
            customerPartnerId = partnerId,
            lonAuthorizationId = lonAuthId,
            customerOrderReference = "E11-DOMAIN-EVENT",
            orderDate = DateTime.UtcNow.Date,
        });
        resp.EnsureSuccessStatusCode();
        var co = await resp.Content.ReadFromJsonAsync<ResultGuid>();
        co!.Data.Should().NotBe(Guid.Empty);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logs = await ctx.DomainEventLogs.IgnoreQueryFilters()
            .Where(d => d.EventType == "ClientOrderCreatedEvent")
            .OrderByDescending(d => d.OccurredAt)
            .Take(5)
            .ToListAsync();
        logs.Should().NotBeEmpty(
            "creating a ClientOrder must persist a ClientOrderCreatedEvent in DomainEventLogs");

        var matching = logs.FirstOrDefault(l => l.PayloadJson.Contains(co.Data.ToString()));
        matching.Should().NotBeNull("the persisted event payload must include the new ClientOrder id");
        matching!.Status.Should().Be("published");
    }

    [Fact]
    public async Task EventIdsAreUnique_AcrossMultipleCreates()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();

        for (int i = 0; i < 3; i++)
        {
            var resp = await client.PostAsJsonAsync("/api/clientorders", new
            {
                customerPartnerId = partnerId,
                lonAuthorizationId = lonAuthId,
                customerOrderReference = $"E11-UNIQ-{i}",
                orderDate = DateTime.UtcNow.Date,
            });
            resp.EnsureSuccessStatusCode();
        }

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var eventIds = await ctx.DomainEventLogs.IgnoreQueryFilters()
            .Where(d => d.EventType == "ClientOrderCreatedEvent")
            .Select(d => d.EventId)
            .ToListAsync();
        eventIds.Distinct().Count().Should().Be(eventIds.Count,
            "the unique index on EventId must keep DomainEventLogs duplicate-free");
    }

    [Fact]
    public async Task AdminEndpoint_FiltersByEventType()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        // Ensure at least one event exists.
        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();
        await client.PostAsJsonAsync("/api/clientorders", new
        {
            customerPartnerId = partnerId,
            lonAuthorizationId = lonAuthId,
            customerOrderReference = "E11-ADMIN-EVENT",
            orderDate = DateTime.UtcNow.Date,
        });

        var resp = await client.GetAsync("/api/admin/domain-events?eventType=ClientOrderCreatedEvent");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("ClientOrderCreatedEvent");
        body.Should().Contain("E11-ADMIN-EVENT");
    }

    // ----- helpers -----

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
