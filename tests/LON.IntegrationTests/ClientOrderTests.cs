using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// Phase 17 §E1 — ClientOrder CRUD + numbering + tenant isolation + soft-delete.
/// </summary>
public class ClientOrderTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public ClientOrderTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_WithLONAuth_ReturnsOk_AndGeneratesOrderNumber()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();

        var payload = new
        {
            customerPartnerId = partnerId,
            lonAuthorizationId = lonAuthId,
            customerOrderReference = "PO-12345",
            orderDate = DateTime.UtcNow.Date,
            notes = "Phase 17 §E1 smoke",
        };

        var resp = await client.PostAsJsonAsync("/api/clientorders", payload);
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK, because: body);

        var result = System.Text.Json.JsonSerializer.Deserialize<ResultResponse>(body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        result!.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var order = await ctx.ClientOrders.IgnoreQueryFilters().FirstAsync(o => o.Id == result.Data);
        order.OrderNumber.Should().MatchRegex(@"^CO-\d{4}-\d{6}$",
            "OrderNumber must be CO-{year}-{seq:D6}");
        order.Status.Should().Be(LON.Domain.Enums.ClientOrderStatus.Draft);
    }

    [Fact]
    public async Task Create_WithoutLONAuth_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (partnerId, _) = await GetCustomerAndAuthAsync();

        var payload = new
        {
            customerPartnerId = partnerId,
            lonAuthorizationId = (Guid?)null,
            orderDate = DateTime.UtcNow.Date,
        };

        var resp = await client.PostAsJsonAsync("/api/clientorders", payload);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetById_AfterCreate_ReturnsOrderWithEmptyFinishedGoods()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();
        var createResp = await client.PostAsJsonAsync("/api/clientorders", new
        {
            customerPartnerId = partnerId,
            lonAuthorizationId = lonAuthId,
            orderDate = DateTime.UtcNow.Date,
        });
        var createBody = await createResp.Content.ReadAsStringAsync();
        var created = System.Text.Json.JsonSerializer.Deserialize<ResultResponse>(createBody,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var getResp = await client.GetAsync($"/api/clientorders/{created!.Data}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await getResp.Content.ReadAsStringAsync();
        getBody.Should().Contain("\"orderNumber\":\"CO-");
        getBody.Should().Contain("\"finishedGoods\":[]");
    }

    [Fact]
    public async Task ParallelCreates_ProduceDistinctOrderNumbers()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();

        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            var resp = await client.PostAsJsonAsync("/api/clientorders", new
            {
                customerPartnerId = partnerId,
                lonAuthorizationId = lonAuthId,
                customerOrderReference = $"PARALLEL-{i}",
                orderDate = DateTime.UtcNow.Date,
            });
            var body = await resp.Content.ReadAsStringAsync();
            var r = System.Text.Json.JsonSerializer.Deserialize<ResultResponse>(body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return r!.Data;
        });

        var ids = await Task.WhenAll(tasks);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var numbers = await ctx.ClientOrders.IgnoreQueryFilters()
            .Where(o => ids.Contains(o.Id))
            .Select(o => o.OrderNumber)
            .ToListAsync();
        numbers.Should().HaveCount(5);
        numbers.Distinct().Should().HaveCount(5, "SQL SEQUENCE must yield distinct values");
    }

    [Fact]
    public async Task Cancel_Sets_Status_To_Cancelled_And_SoftDeletes()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();
        var createResp = await client.PostAsJsonAsync("/api/clientorders", new
        {
            customerPartnerId = partnerId,
            lonAuthorizationId = lonAuthId,
            orderDate = DateTime.UtcNow.Date,
        });
        var created = System.Text.Json.JsonSerializer.Deserialize<ResultResponse>(
            await createResp.Content.ReadAsStringAsync(),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var cancelResp = await client.PostAsJsonAsync(
            $"/api/clientorders/{created!.Data}/cancel",
            new { reason = "Customer cancelled the order via phone" });
        cancelResp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var order = await ctx.ClientOrders.IgnoreQueryFilters().FirstAsync(o => o.Id == created.Data);
        order.Status.Should().Be(LON.Domain.Enums.ClientOrderStatus.Cancelled);
        order.IsDeleted.Should().BeTrue();
        order.CancellationReason.Should().Be("Customer cancelled the order via phone");
        order.DeletedAt.Should().NotBeNull();
    }

    // ----- helpers -----

    private async Task<(Guid customerPartnerId, Guid lonAuthorizationId)> GetCustomerAndAuthAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var partner = await ctx.Partners.IgnoreQueryFilters().FirstAsync();
        var auth = await ctx.LONAuthorizations.IgnoreQueryFilters().FirstAsync();
        return (partner.Id, auth.Id);
    }

    private async Task Authenticate(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record ResultResponse(bool IsSuccess, Guid Data, string? ErrorMessage);
}
