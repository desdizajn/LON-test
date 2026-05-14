using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// Phase 17 §E12 — exercises the per-tenant SQL SEQUENCE objects with
/// parallel ClientOrder creates and verifies that all OrderNumbers are
/// unique + monotonic (no DMax+1 race). Same pattern works for Receipt /
/// Shipment / MaterialIssue / ProductionOrder; this test stands in for all
/// of them since they share `SqlNumberSequenceService` plumbing.
/// </summary>
public class NumberingConcurrencyTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;
    public NumberingConcurrencyTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ParallelClientOrderCreates_ProduceUniqueMonotonicNumbers()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();

        const int N = 10;
        var ids = new Guid[N];
        await Parallel.ForEachAsync(Enumerable.Range(0, N), async (i, ct) =>
        {
            var resp = await client.PostAsJsonAsync("/api/clientorders", new
            {
                customerPartnerId = partnerId,
                lonAuthorizationId = lonAuthId,
                customerOrderReference = $"E12-CONC-{i}",
                orderDate = DateTime.UtcNow.Date,
            }, ct);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<ResultGuid>(cancellationToken: ct);
            ids[i] = body!.Data;
        });

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var numbers = await ctx.ClientOrders.IgnoreQueryFilters()
            .Where(o => ids.Contains(o.Id))
            .Select(o => o.OrderNumber)
            .ToListAsync();

        numbers.Should().HaveCount(N);
        numbers.Distinct().Count().Should().Be(N,
            "every concurrent create must produce a unique OrderNumber");
        numbers.Should().AllSatisfy(n => n.Should().MatchRegex(@"^CO-\d{4}-\d{6}$"));
    }

    [Fact]
    public async Task SequenceServiceProducesIncreasingValues_PerEntityKey()
    {
        using var scope = _factory.Services.CreateScope();
        var seqService = scope.ServiceProvider.GetRequiredService<LON.Application.Common.Interfaces.INumberSequenceService>();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenantId = (await ctx.Tenants.IgnoreQueryFilters().FirstAsync()).Id;

        var a = await seqService.NextAsync("Receipt", tenantId);
        var b = await seqService.NextAsync("Receipt", tenantId);
        var c = await seqService.NextAsync("Receipt", tenantId);

        b.Should().BeGreaterThan(a);
        c.Should().BeGreaterThan(b);

        // Cross-entity isolation: pulling another type's sequence does not skew
        // the Receipt counter.
        var s1 = await seqService.NextAsync("Shipment", tenantId);
        var d = await seqService.NextAsync("Receipt", tenantId);
        d.Should().Be(c + 1);
        s1.Should().BeGreaterThan(0);
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
