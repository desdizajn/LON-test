using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P16.D2 — role × endpoint permission matrix.
///
/// **What this codifies (as of P16):**
/// - `BaseController` declares `[Authorize]` — every logged-in user can
///   hit any controller route by default.
/// - A handful of controllers add `[Authorize(Roles = "Administrator")]`
///   — only admin can hit them: `TenantsController`, `UsersController`,
///   `AuditController`, and `POST /api/MasterData/items`.
/// - The sidebar IA filter (`nav/filterNavGroups.test.ts`) hides modules
///   from non-matching roles purely client-side. The backend does not
///   reject a Warehouse Operator hitting `/api/Customs/declarations`,
///   it just trusts the UI to filter. These tests assert that current
///   state; if/when the backend enforces module-per-role, update the
///   matrix.
///
/// Test data lives in `RoleTopUpSeed` (Test123! password for every seeded
/// tek-* user, Admin123! for admin).
/// </summary>
public class RolePermissionTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public RolePermissionTests(LonApiFactory factory) => _factory = factory;

    private static readonly string[] AdminOnlyEndpoints =
    {
        "/api/Tenants",
        "/api/Users",
        "/api/audit",
    };

    private static readonly string[] AnyAuthEndpoints =
    {
        "/api/MasterData/items",
        "/api/MasterData/partners",
        "/api/MasterData/warehouses",
        "/api/WMS/inventory",
        "/api/Customs/declarations",
        "/api/Production/orders",
        "/api/FinishedGoods/packaging-stock",
        "/api/Hr/attendance/today",
        "/api/Finance/contracts",
        "/api/Management/risks",
    };

    private async Task<HttpClient> LoginAsync(string user, string pass)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = user, password = pass });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    [Theory]
    [InlineData("/api/Tenants")]
    [InlineData("/api/Users")]
    [InlineData("/api/audit")]
    [InlineData("/api/MasterData/items")]
    [InlineData("/api/MasterData/partners")]
    [InlineData("/api/WMS/inventory")]
    [InlineData("/api/Customs/declarations")]
    [InlineData("/api/Hr/attendance/today")]
    [InlineData("/api/Finance/contracts")]
    [InlineData("/api/Management/risks")]
    public async Task Admin_GetsAllEndpoints(string endpoint)
    {
        var client = await LoginAsync("admin", "Admin123!");
        var resp = await client.GetAsync(endpoint);
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Theory]
    [MemberData(nameof(NonAdminRoleEndpoints))]
    public async Task NonAdminRole_GetsAnyAuthEndpoints(string username, string endpoint)
    {
        var client = await LoginAsync(username, "Test123!");
        var resp = await client.GetAsync(endpoint);
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Theory]
    [MemberData(nameof(NonAdminRoleAdminOnlyEndpoints))]
    public async Task NonAdminRole_RejectedOnAdminOnlyEndpoints(string username, string endpoint)
    {
        var client = await LoginAsync(username, "Test123!");
        var resp = await client.GetAsync(endpoint);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            $"non-admin role hitting {endpoint} should be 403");
    }

    [Theory]
    [InlineData("/api/Tenants")]
    [InlineData("/api/WMS/inventory")]
    [InlineData("/api/Production/orders")]
    public async Task NoAuth_GetsRejectedOnEveryEndpoint(string endpoint)
    {
        var client = _factory.CreateClient(); // no auth
        var resp = await client.GetAsync(endpoint);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public static IEnumerable<object[]> NonAdminRoleEndpoints()
    {
        var users = new[]
        {
            "tek-customs", "tek-wh-op", "tek-operator", "tek-qc",
            "tek-hr", "tek-maint", "tek-finance", "tek-mgr",
        };
        foreach (var u in users)
            foreach (var ep in AnyAuthEndpoints)
                yield return new object[] { u, ep };
    }

    public static IEnumerable<object[]> NonAdminRoleAdminOnlyEndpoints()
    {
        // Use a representative subset of non-admin users to keep run time
        // sane while still proving the policy across distinct role names.
        var users = new[] { "tek-customs", "tek-wh-op", "tek-finance", "tek-mgr" };
        foreach (var u in users)
            foreach (var ep in AdminOnlyEndpoints)
                yield return new object[] { u, ep };
    }

    private sealed record LoginResponse(string AccessToken);
}
