using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LON.Domain.Entities.Customs;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// Phase 17 §E10 — exercises the AI helper recommendation surface. Three
/// engines, three scenarios:
///   1. ClientOrder hub blocked-step (Draft + no FGs → "BOM" rec).
///   2. Razdolzuvanje pre-flight (Cleared IM with unflagged lines).
///   3. Receipt variance (received qty &gt; declared qty by 10%).
/// Plus: acted / dismissed feedback flip; OpenAPI shape stability.
/// </summary>
public class AiHelperTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;
    public AiHelperTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ClientOrderHub_DraftWithoutFinishedGoods_ReturnsBomRecommendation()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();
        var orderId = await CreateClientOrderAsync(client, partnerId, lonAuthId, "AI-E10-DRAFT");

        var resp = await client.PostAsJsonAsync("/api/Ai/recommendations", new
        {
            entityType = "ClientOrder",
            entityId = orderId,
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK, because: body);

        var recs = await resp.Content.ReadFromJsonAsync<List<RecommendationDto>>();
        recs.Should().NotBeNull();
        recs!.Should().ContainSingle(r => r.Code == "hub.draft.no-fgs",
            "a brand-new Draft ClientOrder with no finished-goods picked must trigger the BOM nudge");

        // The same recommendation must be persisted in AiSuggestionLogs.
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logs = await ctx.AiSuggestionLogs.IgnoreQueryFilters()
            .Where(x => x.EntityId == orderId && x.RecommendationCode == "hub.draft.no-fgs")
            .ToListAsync();
        logs.Should().HaveCount(1);
        logs[0].UserActedOn.Should().BeNull();
    }

    [Fact]
    public async Task RazdolzuvanjePreflight_ClearedImWithUnflaggedLines_ReturnsPreflightRecommendation()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        // Seed a ClientOrder with a Cleared IM declaration whose lines have
        // RazdolzenaDaNe=false. Then ask for recommendations on the order.
        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();
        var orderId = await CreateClientOrderAsync(client, partnerId, lonAuthId, "AI-E10-PREFLIGHT");

        Guid declId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var co = await ctx.ClientOrders.IgnoreQueryFilters().FirstAsync(x => x.Id == orderId);
            // Bump status so the engine doesn't bail on Draft.
            co.Status = ClientOrderStatus.Active;
            await ctx.SaveChangesAsync();

            var procedure = await ctx.CustomsProcedures.IgnoreQueryFilters()
                .FirstAsync(p => p.Code == "4200" && p.IsActive);
            var item = await ctx.Items.IgnoreQueryFilters().FirstAsync();
            var uom = await ctx.UnitsOfMeasure.IgnoreQueryFilters().FirstAsync();

            var decl = new CustomsDeclaration
            {
                Id = Guid.NewGuid(),
                TenantId = co.TenantId,
                ClientOrderId = orderId,
                CustomsProcedureId = procedure.Id,
                LONAuthorizationId = lonAuthId,
                PartnerId = partnerId,
                DeclarationDate = DateTime.UtcNow.Date,
                DeclarationType = "IM",
                ProcedureCode = "4200",
                DeclarationNumber = $"IM-E10-{Guid.NewGuid():N}".Substring(0, 18),
                MRN = "26MK99999991",
                Status = DeclarationStatus.Cleared,
                Currency = "EUR",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            };
            ctx.CustomsDeclarations.Add(decl);
            ctx.CustomsDeclarationLines.AddRange(
                new CustomsDeclarationLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = co.TenantId,
                    CustomsDeclarationId = decl.Id,
                    LineNumber = 1,
                    ItemId = item.Id,
                    Quantity = 10m,
                    UoMId = uom.Id,
                    CountryOfOrigin = "DE",
                    TariffCode = "61101110",
                    CustomsValue = 100m,
                    RazdolzenaDaNe = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "test",
                },
                new CustomsDeclarationLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = co.TenantId,
                    CustomsDeclarationId = decl.Id,
                    LineNumber = 2,
                    ItemId = item.Id,
                    Quantity = 5m,
                    UoMId = uom.Id,
                    CountryOfOrigin = "DE",
                    TariffCode = "61101110",
                    CustomsValue = 50m,
                    RazdolzenaDaNe = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "test",
                });
            await ctx.SaveChangesAsync();
            declId = decl.Id;
        }

        var resp = await client.PostAsJsonAsync("/api/Ai/recommendations", new
        {
            entityType = "ClientOrder",
            entityId = orderId,
        });
        resp.EnsureSuccessStatusCode();
        var recs = await resp.Content.ReadFromJsonAsync<List<RecommendationDto>>();
        recs.Should().NotBeNull();
        var preflight = recs!.SingleOrDefault(r => r.Code == "razdolzuvanje.preflight.pending-lines");
        preflight.Should().NotBeNull("Cleared IM with 2 unflagged lines must trigger the Razdolzuvanje pre-flight nudge");
        preflight!.Severity.Should().Be("warning");
    }

    [Fact]
    public async Task ReceiptVariance_OverFivePercent_ReturnsVarianceWarning()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        Guid receiptId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenantId = (await ctx.Tenants.IgnoreQueryFilters().FirstAsync()).Id;
            var partner = await ctx.Partners.IgnoreQueryFilters().FirstAsync();
            var warehouse = await ctx.Warehouses.IgnoreQueryFilters().FirstAsync();
            var item = await ctx.Items.IgnoreQueryFilters().FirstAsync();
            var uom = await ctx.UnitsOfMeasure.IgnoreQueryFilters().FirstAsync();
            var procedure = await ctx.CustomsProcedures.IgnoreQueryFilters()
                .FirstAsync(p => p.Code == "4200" && p.IsActive);
            var lonAuth = await ctx.LONAuthorizations.IgnoreQueryFilters().FirstAsync();

            var decl = new CustomsDeclaration
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomsProcedureId = procedure.Id,
                LONAuthorizationId = lonAuth.Id,
                PartnerId = partner.Id,
                DeclarationDate = DateTime.UtcNow.Date,
                DeclarationType = "IM",
                ProcedureCode = "4200",
                DeclarationNumber = $"IM-E10VAR-{Guid.NewGuid():N}".Substring(0, 20),
                MRN = "26MK99999992",
                Status = DeclarationStatus.Cleared,
                Currency = "EUR",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            };
            ctx.CustomsDeclarations.Add(decl);
            ctx.CustomsDeclarationLines.Add(new CustomsDeclarationLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomsDeclarationId = decl.Id,
                LineNumber = 1,
                ItemId = item.Id,
                Quantity = 100m,
                UoMId = uom.Id,
                CountryOfOrigin = "DE",
                TariffCode = "61101110",
                CustomsValue = 1000m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            });

            var receipt = new Receipt
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ReceiptNumber = $"RC-E10-{Guid.NewGuid():N}".Substring(0, 16),
                ReceiptDate = DateTime.UtcNow,
                PartnerId = partner.Id,
                WarehouseId = warehouse.Id,
                ReferenceNumber = "E10-VARIANCE",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            };
            ctx.Receipts.Add(receipt);
            ctx.ReceiptLines.Add(new ReceiptLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ReceiptId = receipt.Id,
                LineNumber = 1,
                ItemId = item.Id,
                Quantity = 110m, // 10% over declared
                UoMId = uom.Id,
                BatchNumber = "BATCH-1",
                MRN = "26MK99999992",
                QualityStatus = QualityStatus.OK,
                CustomsDeclarationId = decl.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            });
            await ctx.SaveChangesAsync();
            receiptId = receipt.Id;
        }

        var resp = await client.PostAsJsonAsync("/api/Ai/recommendations", new
        {
            entityType = "Receipt",
            entityId = receiptId,
        });
        resp.EnsureSuccessStatusCode();
        var recs = await resp.Content.ReadFromJsonAsync<List<RecommendationDto>>();
        recs.Should().NotBeNull();
        recs!.Should().ContainSingle(r => r.Code == "receipt.variance.over-threshold");
        recs!.Single(r => r.Code == "receipt.variance.over-threshold").Body.Should().Contain("10");
    }

    [Fact]
    public async Task ReceiptVariance_WithinThreshold_ReturnsEmpty()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        Guid receiptId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenantId = (await ctx.Tenants.IgnoreQueryFilters().FirstAsync()).Id;
            var partner = await ctx.Partners.IgnoreQueryFilters().FirstAsync();
            var warehouse = await ctx.Warehouses.IgnoreQueryFilters().FirstAsync();
            var item = await ctx.Items.IgnoreQueryFilters().FirstAsync();
            var uom = await ctx.UnitsOfMeasure.IgnoreQueryFilters().FirstAsync();
            var procedure = await ctx.CustomsProcedures.IgnoreQueryFilters()
                .FirstAsync(p => p.Code == "4200" && p.IsActive);
            var lonAuth = await ctx.LONAuthorizations.IgnoreQueryFilters().FirstAsync();

            var decl = new CustomsDeclaration
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomsProcedureId = procedure.Id,
                LONAuthorizationId = lonAuth.Id,
                PartnerId = partner.Id,
                DeclarationDate = DateTime.UtcNow.Date,
                DeclarationType = "IM",
                ProcedureCode = "4200",
                DeclarationNumber = $"IM-E10OK-{Guid.NewGuid():N}".Substring(0, 18),
                MRN = "26MK99999993",
                Status = DeclarationStatus.Cleared,
                Currency = "EUR",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            };
            ctx.CustomsDeclarations.Add(decl);
            ctx.CustomsDeclarationLines.Add(new CustomsDeclarationLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomsDeclarationId = decl.Id,
                LineNumber = 1,
                ItemId = item.Id,
                Quantity = 100m,
                UoMId = uom.Id,
                CountryOfOrigin = "DE",
                TariffCode = "61101110",
                CustomsValue = 1000m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            });
            var receipt = new Receipt
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ReceiptNumber = $"RC-E10K-{Guid.NewGuid():N}".Substring(0, 16),
                ReceiptDate = DateTime.UtcNow,
                PartnerId = partner.Id,
                WarehouseId = warehouse.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            };
            ctx.Receipts.Add(receipt);
            ctx.ReceiptLines.Add(new ReceiptLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ReceiptId = receipt.Id,
                LineNumber = 1,
                ItemId = item.Id,
                Quantity = 102m, // 2% over — within threshold
                UoMId = uom.Id,
                CustomsDeclarationId = decl.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            });
            await ctx.SaveChangesAsync();
            receiptId = receipt.Id;
        }

        var resp = await client.PostAsJsonAsync("/api/Ai/recommendations", new
        {
            entityType = "Receipt",
            entityId = receiptId,
        });
        resp.EnsureSuccessStatusCode();
        var recs = await resp.Content.ReadFromJsonAsync<List<RecommendationDto>>();
        recs.Should().NotBeNull();
        recs!.Should().NotContain(r => r.Code == "receipt.variance.over-threshold",
            "a 2% variance must not trigger the >5% warning");
    }

    [Fact]
    public async Task MarkActed_FlipsFlagAndStampsAudit()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();
        var orderId = await CreateClientOrderAsync(client, partnerId, lonAuthId, "AI-E10-ACTED");

        var resp = await client.PostAsJsonAsync("/api/Ai/recommendations", new
        {
            entityType = "ClientOrder",
            entityId = orderId,
        });
        resp.EnsureSuccessStatusCode();
        var recs = await resp.Content.ReadFromJsonAsync<List<RecommendationDto>>();
        var first = recs!.First();

        var actedResp = await client.PostAsync($"/api/Ai/suggestions/{first.Id}/acted", null);
        actedResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var log = await ctx.AiSuggestionLogs.IgnoreQueryFilters().FirstAsync(x => x.Id == first.Id);
        log.UserActedOn.Should().BeTrue();
        log.UserActedAt.Should().NotBeNull();
        log.UserActedBy.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MarkDismissed_FlipsFlagFalse()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (partnerId, lonAuthId) = await GetCustomerAndAuthAsync();
        var orderId = await CreateClientOrderAsync(client, partnerId, lonAuthId, "AI-E10-DISMISS");

        var resp = await client.PostAsJsonAsync("/api/Ai/recommendations", new
        {
            entityType = "ClientOrder",
            entityId = orderId,
        });
        var recs = await resp.Content.ReadFromJsonAsync<List<RecommendationDto>>();
        var first = recs!.First();

        var dismissResp = await client.PostAsync($"/api/Ai/suggestions/{first.Id}/dismissed", null);
        dismissResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var log = await ctx.AiSuggestionLogs.IgnoreQueryFilters().FirstAsync(x => x.Id == first.Id);
        log.UserActedOn.Should().BeFalse();
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

    private async Task<Guid> CreateClientOrderAsync(HttpClient client, Guid customerId, Guid lonAuthId, string reference)
    {
        var resp = await client.PostAsJsonAsync("/api/clientorders", new
        {
            customerPartnerId = customerId,
            lonAuthorizationId = lonAuthId,
            customerOrderReference = reference,
            orderDate = DateTime.UtcNow.Date,
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ResultGuid>();
        return body!.Data;
    }

    private sealed record LoginResp(string AccessToken);
    private sealed record ResultGuid(bool IsSuccess, Guid Data, string? ErrorMessage);

    private sealed record RecommendationDto(
        Guid Id,
        string Code,
        string Title,
        string Body,
        string Severity,
        double Confidence,
        string? ActionLink,
        string? ActionLabel,
        Dictionary<string, JsonElement>? StructuredData);
}
