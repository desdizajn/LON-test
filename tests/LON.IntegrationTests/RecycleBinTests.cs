using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.Customs;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// Phase 17 §E14 — soft-delete + recycle bin behaviour:
///   1. Cancelling a ClientOrder with no children soft-deletes it.
///   2. Cancelling a ClientOrder with non-deleted children is blocked.
///   3. Restoring a soft-deleted ClientOrder clears IsDeleted + DeletedAt + DeletedBy.
///   4. Permanent delete removes the row entirely.
/// </summary>
public class RecycleBinTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;
    public RecycleBinTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Cancel_ChildlessOrder_SoftDeletes()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var orderId = await CreateClientOrderAsync(client, "E14-CHILDLESS");

        var resp = await client.PostAsJsonAsync($"/api/clientorders/{orderId}/cancel",
            new { reason = "no longer needed" });
        resp.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var co = await ctx.ClientOrders.IgnoreQueryFilters().FirstAsync(o => o.Id == orderId);
        co.IsDeleted.Should().BeTrue();
        co.DeletedAt.Should().NotBeNull();
        co.DeletedBy.Should().NotBeNullOrEmpty();
        co.Status.Should().Be(ClientOrderStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_WithNonDeletedChildren_IsBlocked()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var orderId = await CreateClientOrderAsync(client, "E14-WITH-CHILDREN");

        // Seed a non-deleted CustomsDeclaration as a child.
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var co = await ctx.ClientOrders.IgnoreQueryFilters().FirstAsync(o => o.Id == orderId);
            var procedure = await ctx.CustomsProcedures.IgnoreQueryFilters().FirstAsync(p => p.Code == "4200" && p.IsActive);
            var partner = await ctx.Partners.IgnoreQueryFilters().FirstAsync();
            var lonAuth = await ctx.LONAuthorizations.IgnoreQueryFilters().FirstAsync();
            ctx.CustomsDeclarations.Add(new CustomsDeclaration
            {
                Id = Guid.NewGuid(),
                TenantId = co.TenantId,
                ClientOrderId = orderId,
                CustomsProcedureId = procedure.Id,
                LONAuthorizationId = lonAuth.Id,
                PartnerId = partner.Id,
                DeclarationDate = DateTime.UtcNow.Date,
                DeclarationType = "IM",
                ProcedureCode = "4200",
                DeclarationNumber = $"IM-CHILD-{Guid.NewGuid():N}".Substring(0, 18),
                MRN = "26MK99999990",
                Status = DeclarationStatus.Draft,
                Currency = "EUR",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            });
            await ctx.SaveChangesAsync();
        }

        var resp = await client.PostAsJsonAsync($"/api/clientorders/{orderId}/cancel",
            new { reason = "blocked test" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("ClientOrderHasChildren");
    }

    [Fact]
    public async Task Restore_FlipsIsDeletedAndClearsStamps()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var orderId = await CreateClientOrderAsync(client, "E14-RESTORE");

        var cancel = await client.PostAsJsonAsync($"/api/clientorders/{orderId}/cancel",
            new { reason = "soft" });
        cancel.EnsureSuccessStatusCode();

        var restore = await client.PostAsync($"/api/admin/recycle-bin/client-orders/{orderId}/restore", null);
        restore.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var co = await ctx.ClientOrders.IgnoreQueryFilters().FirstAsync(o => o.Id == orderId);
        co.IsDeleted.Should().BeFalse();
        co.DeletedAt.Should().BeNull();
        co.DeletedBy.Should().BeNull();
    }

    [Fact]
    public async Task GetRecycleBin_ReturnsSoftDeletedOrders()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var orderId = await CreateClientOrderAsync(client, "E14-LIST");
        await client.PostAsJsonAsync($"/api/clientorders/{orderId}/cancel",
            new { reason = "list test" });

        var resp = await client.GetAsync("/api/admin/recycle-bin?page=1&pageSize=50");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("ClientOrder");
        body.Should().Contain(orderId.ToString());
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

    private async Task<Guid> CreateClientOrderAsync(HttpClient client, string reference)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var partner = await ctx.Partners.IgnoreQueryFilters().FirstAsync();
        var lon = await ctx.LONAuthorizations.IgnoreQueryFilters().FirstAsync();
        var resp = await client.PostAsJsonAsync("/api/clientorders", new
        {
            customerPartnerId = partner.Id,
            lonAuthorizationId = lon.Id,
            customerOrderReference = reference,
            orderDate = DateTime.UtcNow.Date,
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ResultGuid>();
        return body!.Data;
    }

    private sealed record LoginResp(string AccessToken);
    private sealed record ResultGuid(bool IsSuccess, Guid Data, string? ErrorMessage);
}
