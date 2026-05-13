using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Application.Management.Alerts;
using LON.Domain.Entities.Customs;
using LON.Domain.Entities.Guarantee;
using LON.Domain.Entities.Management;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// Phase 17 §E10.5 — exercises seed + evaluator + endpoints.
/// </summary>
public class AlertRulesTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;
    public AlertRulesTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Migration_SeedsSixActiveRulesPerTenant()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var counts = await ctx.AlertRules.IgnoreQueryFilters()
            .GroupBy(r => r.TenantId)
            .Select(g => new { Tenant = g.Key, Active = g.Count(r => r.IsActive) })
            .ToListAsync();
        counts.Should().NotBeEmpty();
        counts.Should().OnlyContain(c => c.Active == 6,
            "every active tenant must get 6 seeded rules");
    }

    [Fact]
    public async Task Evaluator_GuaranteeUtilizationOverThreshold_CreatesAlertEvent()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        Guid acctId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenantId = (await ctx.Tenants.IgnoreQueryFilters().FirstAsync()).Id;
            var acct = new GuaranteeAccount
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountNumber = "GA-E10.5",
                AccountName = "AlertRules test guarantee",
                Currency = "EUR",
                TotalLimit = 1000m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            };
            ctx.GuaranteeAccounts.Add(acct);
            ctx.GuaranteeLedgerEntries.Add(new GuaranteeLedgerEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                GuaranteeAccountId = acct.Id,
                EntryType = GuaranteeEntryType.Debit,
                Amount = 950m, // 95% utilisation
                EntryDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            });
            await ctx.SaveChangesAsync();
            acctId = acct.Id;
        }

        var runResp = await client.PostAsync("/api/Management/alert-events/run-evaluator", null);
        runResp.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var events = await ctx.AlertEvents.IgnoreQueryFilters()
                .Where(e => e.EntityType == "GuaranteeAccount" && e.EntityId == acctId)
                .ToListAsync();
            events.Should().ContainSingle(
                "the 95% utilisation account must trigger one Open event after the first pass");
            events[0].Status.Should().Be(AlertEventStatus.Open);
        }
    }

    [Fact]
    public async Task Evaluator_TwoPasses_DoesNotDuplicate()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenantId = (await ctx.Tenants.IgnoreQueryFilters().FirstAsync()).Id;
            var acct = new GuaranteeAccount
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountNumber = "GA-E10.5-DEDUPE",
                AccountName = "dedupe",
                Currency = "EUR",
                TotalLimit = 1000m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            };
            ctx.GuaranteeAccounts.Add(acct);
            ctx.GuaranteeLedgerEntries.Add(new GuaranteeLedgerEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                GuaranteeAccountId = acct.Id,
                EntryType = GuaranteeEntryType.Debit,
                Amount = 920m,
                EntryDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            });
            await ctx.SaveChangesAsync();
        }

        await client.PostAsync("/api/Management/alert-events/run-evaluator", null);
        var countBefore = await CountEventsAsync();

        await client.PostAsync("/api/Management/alert-events/run-evaluator", null);
        var countAfter = await CountEventsAsync();

        countAfter.Should().Be(countBefore,
            "running the evaluator twice without resolving must not produce duplicate Open events");
    }

    [Fact]
    public async Task Acknowledge_FlipsStatusAndStampsAudit()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        // Seed a guarantee account that breaches and run the evaluator.
        Guid eventId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenantId = (await ctx.Tenants.IgnoreQueryFilters().FirstAsync()).Id;
            var acct = new GuaranteeAccount
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountNumber = "GA-E10.5-ACK",
                AccountName = "ack test",
                Currency = "EUR",
                TotalLimit = 1000m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            };
            ctx.GuaranteeAccounts.Add(acct);
            ctx.GuaranteeLedgerEntries.Add(new GuaranteeLedgerEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                GuaranteeAccountId = acct.Id,
                EntryType = GuaranteeEntryType.Debit,
                Amount = 930m,
                EntryDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            });
            await ctx.SaveChangesAsync();
        }

        await client.PostAsync("/api/Management/alert-events/run-evaluator", null);

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            eventId = await ctx.AlertEvents.IgnoreQueryFilters()
                .Where(e => e.EntityType == "GuaranteeAccount" && e.Status == AlertEventStatus.Open)
                .OrderByDescending(e => e.OccurredAt)
                .Select(e => e.Id)
                .FirstAsync();
        }

        var ack = await client.PostAsJsonAsync($"/api/Management/alert-events/{eventId}/acknowledge", new { reason = "investigating" });
        ack.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ev = await ctx.AlertEvents.IgnoreQueryFilters().FirstAsync(e => e.Id == eventId);
            ev.Status.Should().Be(AlertEventStatus.Acknowledged);
            ev.AcknowledgedAt.Should().NotBeNull();
            ev.AcknowledgedBy.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task GetAlertEvents_FiltersByStatus()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var resp = await client.GetAsync("/api/Management/alert-events?status=0");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("isSuccess");
    }

    // ----- helpers -----

    private async Task<int> CountEventsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await ctx.AlertEvents.IgnoreQueryFilters().CountAsync();
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
