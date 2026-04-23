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
/// P15.5 — GuaranteeBalanceSnapshot integration test. Verifies the
/// snapshot command aggregates outstanding debits / credits correctly and
/// that a re-run for the same date is idempotent (soft-deletes the prior
/// row, inserts a fresh one).
/// </summary>
public class GuaranteeSnapshotTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public GuaranteeSnapshotTests(LonApiFactory factory) => _factory = factory;

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
    public async Task RunSnapshots_AggregatesLedgerAndIsIdempotent()
    {
        var client = await AuthedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var account = await db.GuaranteeAccounts.FirstAsync(a => a.IsActive && !a.IsDeleted);
        var today = DateTime.UtcNow.Date;

        // First run — snapshot rows created for every active account.
        var run1 = await client.PostAsJsonAsync("/api/Guarantee/snapshots/run", new
        {
            snapshotDate = today,
            notes = "P15.5 initial"
        });
        run1.StatusCode.Should().Be(HttpStatusCode.OK, await run1.Content.ReadAsStringAsync());

        var snap1 = await db.GuaranteeBalanceSnapshots
            .AsNoTracking()
            .Where(s => s.GuaranteeAccountId == account.Id && s.SnapshotDate == today && !s.IsDeleted)
            .ToListAsync();
        snap1.Should().HaveCount(1);
        var first = snap1.Single();
        first.TotalLimit.Should().Be(account.TotalLimit);
        first.Currency.Should().Be(account.Currency);
        first.Notes.Should().Be("P15.5 initial");
        first.NetBalance.Should().Be(first.DebitedAmount - first.CreditedAmount);
        first.AvailableLimit.Should().Be(account.TotalLimit - first.NetBalance);

        // Second run for same date — replaces prior snapshot row (soft-delete + insert).
        var run2 = await client.PostAsJsonAsync("/api/Guarantee/snapshots/run", new
        {
            snapshotDate = today,
            notes = "P15.5 re-run"
        });
        run2.StatusCode.Should().Be(HttpStatusCode.OK);

        db.ChangeTracker.Clear();
        var activeAfter = await db.GuaranteeBalanceSnapshots
            .AsNoTracking()
            .Where(s => s.GuaranteeAccountId == account.Id && s.SnapshotDate == today && !s.IsDeleted)
            .ToListAsync();
        activeAfter.Should().HaveCount(1, "second run replaces the prior snapshot for this (account, date)");
        activeAfter.Single().Notes.Should().Be("P15.5 re-run");
        activeAfter.Single().Id.Should().NotBe(first.Id, "fresh row, not the same id");

        // Bypass the soft-delete filter — prior row should still be in the table, marked deleted.
        var allIncludingDeleted = await db.GuaranteeBalanceSnapshots
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.GuaranteeAccountId == account.Id && s.SnapshotDate == today)
            .ToListAsync();
        allIncludingDeleted.Should().HaveCount(2);
        allIncludingDeleted.Count(s => s.IsDeleted).Should().Be(1);
    }

    private sealed record LoginResponse(string AccessToken);
}
