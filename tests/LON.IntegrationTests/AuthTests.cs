using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace LON.IntegrationTests;

public class AuthTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public AuthTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_WithSeededAdmin_ReturnsJwtTokenAndAdminRole()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.Roles.Should().Contain("Administrator");
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "not-the-password" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_JwtContainsTenantIdClaim_MatchingSeededTenant()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(body!.AccessToken);

        var tenantClaim = token.Claims.FirstOrDefault(c => c.Type == "tenant_id");
        tenantClaim.Should().NotBeNull("login must emit tenant_id claim for downstream scoping");
        Guid.TryParse(tenantClaim!.Value, out var tenantId).Should().BeTrue();
        tenantId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/wms/receipts");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record LoginResponse(string AccessToken, string RefreshToken, List<string> Roles);
}
