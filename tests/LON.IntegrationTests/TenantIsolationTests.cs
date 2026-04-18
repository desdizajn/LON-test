using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P1.4 — EF global query filter restricts every ITenantScoped read to the
/// authenticated tenant. Proven by seeding a second tenant, inserting an
/// Item under it, and asserting the admin (TEKSPORT) never sees it.
/// </summary>
public class TenantIsolationTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public TenantIsolationTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AuthenticatedQuery_DoesNotLeakOtherTenantsItems()
    {
        // Arrange — insert a second tenant + a foreign Item directly via DbContext.
        // IgnoreQueryFilters is used for reads so seeder can see all tenants.
        using var seedScope = _factory.Services.CreateScope();
        var ctx = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        const string foreignCode = "FOREIGN-ISOLATION-TEST";

        // Clean slate if a previous test run left artifacts.
        var existingItem = await ctx.Items.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Code == foreignCode);
        if (existingItem is not null) ctx.Items.Remove(existingItem);
        var existingTenant = await ctx.Tenants.FirstOrDefaultAsync(t => t.Code == "ISO-DEMO");
        if (existingTenant is not null) ctx.Tenants.Remove(existingTenant);
        await ctx.SaveChangesAsync();

        var otherTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Code = "ISO-DEMO",
            Name = "Isolation Test Tenant",
            Country = "MK",
            DefaultLanguage = "mk",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "IsolationTest"
        };
        ctx.Tenants.Add(otherTenant);
        await ctx.SaveChangesAsync();

        var kg = await ctx.UnitsOfMeasure.FirstAsync(u => u.Code == "KG");
        var foreignItem = new Item
        {
            Id = Guid.NewGuid(),
            Code = foreignCode,
            Name = "Raw material belonging to ISO-DEMO tenant",
            Type = ItemType.RawMaterial,
            IsBatchTracked = false,
            IsMRNTracked = false,
            BaseUoMId = kg.Id,
            StandardCost = 1m,
            TenantId = otherTenant.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "IsolationTest"
        };
        ctx.Items.Add(foreignItem);
        await ctx.SaveChangesAsync();

        // Sanity — bypassing the filter, the foreign item IS in the DB.
        var allItems = await ctx.Items.IgnoreQueryFilters().ToListAsync();
        allItems.Should().Contain(i => i.Code == foreignCode,
            "foreign item should exist when query filter is ignored");

        // Act — admin user (TEKSPORT) fetches items via the authenticated API.
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.EnsureSuccessStatusCode();
        var login = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var items = await client.GetFromJsonAsync<List<ItemRow>>("/api/masterdata/items");

        // Assert — admin (TEKSPORT) must NOT see the ISO-DEMO item.
        items.Should().NotBeNull();
        items!.Should().NotContain(i => i.Code == foreignCode,
            "global query filter must hide other tenants' rows from authenticated requests");
        items.Should().OnlyContain(i => i.Code != foreignCode);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record ItemRow(Guid Id, string Code, string Name);
}
