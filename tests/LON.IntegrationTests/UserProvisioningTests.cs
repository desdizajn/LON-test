using System.IdentityModel.Tokens.Jwt;
using System.Net;
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
/// P1.6 — Admin can provision a user for a different tenant; that user's JWT
/// carries the target tenant id, and the global query filter confines their
/// reads to that tenant's rows.
/// </summary>
public class UserProvisioningTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public UserProvisioningTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AdminProvisionsUserForSecondTenant_NewUserSeesOnlyThatTenantsData()
    {
        const string secondTenantCode = "DUP-CODE-TEST";
        const string newUsername = "duptest-admin";
        const string newPassword = "DupTest123!";
        const string foreignItemCode = "DUP-MATERIAL-001";

        // Clean slate in case a previous test run left artifacts.
        using (var seedScope = _factory.Services.CreateScope())
        {
            var ctx = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var staleUser = await ctx.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Username == newUsername);
            if (staleUser is not null)
            {
                var staleRoles = await ctx.UserRoles.Where(ur => ur.UserId == staleUser.Id).ToListAsync();
                ctx.UserRoles.RemoveRange(staleRoles);
                ctx.Users.Remove(staleUser);
            }
            var staleItem = await ctx.Items.IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Code == foreignItemCode);
            if (staleItem is not null) ctx.Items.Remove(staleItem);
            var staleTenant = await ctx.Tenants.FirstOrDefaultAsync(t => t.Code == secondTenantCode);
            if (staleTenant is not null) ctx.Tenants.Remove(staleTenant);
            await ctx.SaveChangesAsync();
        }

        var adminClient = _factory.CreateClient();
        await LoginAs(adminClient, "admin", "Admin123!");

        // 1. Admin creates a second tenant.
        var tenantResp = await adminClient.PostAsJsonAsync("/api/tenants", new
        {
            code = secondTenantCode,
            name = "Duplicate-code isolation test tenant",
            country = "MK",
            defaultLanguage = "mk"
        });
        tenantResp.EnsureSuccessStatusCode();
        var newTenant = await tenantResp.Content.ReadFromJsonAsync<TenantResponse>();
        newTenant!.Id.Should().NotBe(Guid.Empty);

        // 2. Seed a foreign Item directly under the new tenant (bypasses query filter on read).
        Guid foreignItemId;
        using (var seedScope = _factory.Services.CreateScope())
        {
            var ctx = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var kg = await ctx.UnitsOfMeasure.FirstAsync(u => u.Code == "KG");
            var item = new Item
            {
                Id = Guid.NewGuid(),
                Code = foreignItemCode,
                Name = "Material belonging to DUP-CODE-TEST",
                Type = ItemType.RawMaterial,
                IsBatchTracked = false,
                IsMRNTracked = false,
                BaseUoMId = kg.Id,
                StandardCost = 1m,
                TenantId = newTenant.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "UserProvisioningTest"
            };
            ctx.Items.Add(item);
            await ctx.SaveChangesAsync();
            foreignItemId = item.Id;
        }

        // 3. Admin fetches Administrator role id (to grant it to the new user).
        var adminRoleId = await FetchAdministratorRoleIdAsync();

        // 4. Admin provisions a user under the new tenant.
        var createUserResp = await adminClient.PostAsJsonAsync("/api/users", new
        {
            username = newUsername,
            email = $"{newUsername}@dup.test",
            fullName = "Duplicate Code Test Admin",
            password = newPassword,
            roleIds = new[] { adminRoleId },
            tenantId = newTenant.Id
        });
        createUserResp.StatusCode.Should().Be(HttpStatusCode.OK,
            because: await createUserResp.Content.ReadAsStringAsync());

        // Direct DB assertion — row exists under the new tenant.
        using (var verifyScope = _factory.Services.CreateScope())
        {
            var ctx = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await ctx.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Username == newUsername);
            persisted.Should().NotBeNull("the POST must actually persist the user");
            persisted!.TenantId.Should().Be(newTenant.Id,
                "TenantId must match the explicit value from the POST payload, not the caller's tenant");
            persisted.IsActive.Should().BeTrue();
            persisted.PasswordHash.Should().NotBeNullOrWhiteSpace();
            persisted.PasswordHash.Should().NotBe(newPassword, "password must be hashed, never stored plain");
        }

        // 5. New user logs in. Their JWT must carry the second tenant id.
        var userClient = _factory.CreateClient();
        var loginBody = await LoginAs(userClient, newUsername, newPassword);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(loginBody.AccessToken);
        var tenantClaim = jwt.Claims.FirstOrDefault(c => c.Type == "tenant_id");
        tenantClaim.Should().NotBeNull();
        Guid.Parse(tenantClaim!.Value).Should().Be(newTenant.Id,
            "the new user's JWT must scope them to the tenant they were provisioned under");

        // 6. New user queries Items → sees only foreign item, not any TEKSPORT ones.
        var visibleItems = await userClient.GetFromJsonAsync<List<ItemRow>>("/api/masterdata/items");
        visibleItems.Should().NotBeNull();
        visibleItems!.Should().Contain(i => i.Code == foreignItemCode,
            "the new user must see items under their own tenant");

        // TEKSPORT seeded with items (e.g. RM-001, SF-001). None of those should appear.
        var tekItems = await adminClient.GetFromJsonAsync<List<ItemRow>>("/api/masterdata/items");
        tekItems.Should().NotBeNull();
        var tekOnlyCodes = tekItems!
            .Select(i => i.Code)
            .Except(new[] { foreignItemCode })
            .ToList();
        tekOnlyCodes.Should().NotBeEmpty("TEKSPORT must have seeded items to compare against");
        visibleItems.Select(i => i.Code).Should().NotIntersectWith(tekOnlyCodes,
            "global query filter must hide TEKSPORT items from the DUP-CODE-TEST user");
    }

    [Fact]
    public async Task CreateUser_WithInvalidTenantId_Returns400()
    {
        var client = _factory.CreateClient();
        await LoginAs(client, "admin", "Admin123!");

        var resp = await client.PostAsJsonAsync("/api/users", new
        {
            username = $"bogus-{Guid.NewGuid():N}"[..16],
            email = "bogus@test.local",
            fullName = "Bogus",
            password = "Bogus123!",
            roleIds = Array.Empty<Guid>(),
            tenantId = Guid.NewGuid() // not in DB
        });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "handler must reject a TenantId that does not exist");
    }

    [Fact]
    public async Task CreateUser_WithoutTenantId_DefaultsToCallerTenant()
    {
        var username = $"default-{Guid.NewGuid():N}"[..16];
        var client = _factory.CreateClient();
        var loginBody = await LoginAs(client, "admin", "Admin123!");

        // Decode caller's tenant from the JWT to compare against.
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(loginBody.AccessToken);
        var callerTenantId = Guid.Parse(jwt.Claims.First(c => c.Type == "tenant_id").Value);

        var resp = await client.PostAsJsonAsync("/api/users", new
        {
            username,
            email = $"{username}@test.local",
            fullName = "Default Tenant Case",
            password = "Default123!",
            roleIds = Array.Empty<Guid>()
            // tenantId omitted on purpose
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await ctx.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Username == username);
        persisted!.TenantId.Should().Be(callerTenantId,
            "omitting TenantId must fall back to caller's tenant via DbContext auto-fill");
    }

    [Fact]
    public async Task CreateUser_AsUnauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/users", new
        {
            username = "should-not-be-created",
            email = "x@x.test",
            fullName = "x",
            password = "xxx",
            roleIds = Array.Empty<Guid>()
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<Guid> FetchAdministratorRoleIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var role = await ctx.Roles.FirstAsync(r => r.Name == "Administrator");
        return role.Id;
    }

    private static async Task<LoginResponse> LoginAs(HttpClient client, string username, string password)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return body;
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record TenantResponse(Guid Id, string Code);
    private sealed record ItemRow(Guid Id, string Code, string Name);
}
