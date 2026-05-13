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
/// Phase 17 §E9 — Razdolzuvanje view per ClientOrder. Validates:
///   1. GET aggregate: IM duty / EX duty / variance / per-line breakdown.
///   2. POST mark-line: RazdolzenaDaNe flag flips with timestamp + audit user.
///   3. POST mark-line rejects line that doesn't belong to the ClientOrder.
///   4. POST snapshot: creates GuaranteeBalanceSnapshot rows + reconciled order
///      auto-transitions to Closed when every line is flagged.
///   5. GET /pdf returns HTML cover-sheet referencing the order number.
///   6. GET /pee060 returns XML for the linked LONAuthorization.
/// </summary>
public class RazdolzuvanjeFlowTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public RazdolzuvanjeFlowTests(LonApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthedAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginResp>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    /// <summary>
    /// Seeds a Razdolzuvanje-ready ClientOrder directly via DbContext —
    /// 1 IM declaration with 2 lines (€80 duty) + 1 EX with 1 line (€80 duty).
    /// Variance = 0; IsReconciled = true; AllLinesFlagged = false (until test
    /// flips the flag). Returns the order id + the IM line ids.
    /// </summary>
    private async Task<(Guid orderId, Guid imLine1, Guid imLine2)> SeedReconciledOrderAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = await ctx.Tenants.FirstAsync(t => t.Code == "TEKSPORT");
        var partner = await ctx.Partners.IgnoreQueryFilters().FirstAsync();
        var auth = await ctx.LONAuthorizations.IgnoreQueryFilters().FirstAsync();
        var procedure = await ctx.CustomsProcedures.IgnoreQueryFilters()
            .FirstAsync(p => p.IsActive);
        var item = await ctx.Items.IgnoreQueryFilters().FirstAsync(i => !i.IsDeleted);
        var uom = await ctx.UnitsOfMeasure.IgnoreQueryFilters().FirstAsync(u => !u.IsDeleted);
        var mrn = $"RZD-{Guid.NewGuid():N}".Substring(0, 16);

        var order = new ClientOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            OrderNumber = $"RZD-CO-{Guid.NewGuid().ToString()[..8]}",
            CustomerPartnerId = partner.Id,
            LONAuthorizationId = auth.Id,
            OrderDate = DateTime.UtcNow.Date,
            Status = ClientOrderStatus.Shipped,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };
        ctx.ClientOrders.Add(order);

        var imDecl = new CustomsDeclaration
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            DeclarationNumber = $"IM-RZD-{Guid.NewGuid().ToString()[..6]}",
            MRN = mrn,
            DeclarationDate = DateTime.UtcNow.Date.AddDays(-30),
            CustomsProcedureId = procedure.Id,
            PartnerId = partner.Id,
            LONAuthorizationId = auth.Id,
            ClientOrderId = order.Id,
            DeclarationType = "IM",
            ProcedureCode = procedure.Code,
            Currency = "EUR",
            TotalCustomsValue = 1600m,
            TotalDuty = 80m,
            TotalVAT = 288m,
            Status = DeclarationStatus.Cleared,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };
        var imLineA = new CustomsDeclarationLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            CustomsDeclarationId = imDecl.Id,
            LineNumber = 1,
            ItemId = item.Id,
            Quantity = 10m,
            UoMId = uom.Id,
            CustomsValue = 800m,
            DutyRate = 5m,
            DutyAmount = 40m,
            VATRate = 18m,
            VATAmount = 144m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };
        var imLineB = new CustomsDeclarationLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            CustomsDeclarationId = imDecl.Id,
            LineNumber = 2,
            ItemId = item.Id,
            Quantity = 10m,
            UoMId = uom.Id,
            CustomsValue = 800m,
            DutyRate = 5m,
            DutyAmount = 40m,
            VATRate = 18m,
            VATAmount = 144m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };
        imDecl.Lines.Add(imLineA);
        imDecl.Lines.Add(imLineB);
        ctx.CustomsDeclarations.Add(imDecl);

        var exDecl = new CustomsDeclaration
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            DeclarationNumber = $"EX-RZD-{Guid.NewGuid().ToString()[..6]}",
            MRN = $"EX-{Guid.NewGuid():N}".Substring(0, 16),
            DeclarationDate = DateTime.UtcNow.Date,
            CustomsProcedureId = procedure.Id,
            PartnerId = partner.Id,
            LONAuthorizationId = auth.Id,
            ClientOrderId = order.Id,
            DeclarationType = "EX",
            ProcedureCode = procedure.Code,
            Currency = "EUR",
            TotalCustomsValue = 1600m,
            TotalDuty = 80m,
            TotalVAT = 0m,
            Status = DeclarationStatus.Cleared,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };
        exDecl.Lines.Add(new CustomsDeclarationLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            CustomsDeclarationId = exDecl.Id,
            LineNumber = 1,
            ItemId = item.Id,
            Quantity = 20m,
            UoMId = uom.Id,
            CustomsValue = 1600m,
            DutyRate = 5m,
            DutyAmount = 80m,
            VATRate = 0m,
            VATAmount = 0m,
            PreviousMRN = mrn,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        });
        ctx.CustomsDeclarations.Add(exDecl);

        await ctx.SaveChangesAsync();
        return (order.Id, imLineA.Id, imLineB.Id);
    }

    [Fact]
    public async Task GetRazdolzuvanje_ReturnsImVsCreditedTotals_AndPerLineBreakdown()
    {
        var client = await AuthedAsync();
        var (orderId, _, _) = await SeedReconciledOrderAsync();

        var resp = await client.GetAsync($"/api/ClientOrders/{orderId}/razdolzuvanje");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await resp.Content.ReadFromJsonAsync<RazdolzuvanjeResp>();
        report.Should().NotBeNull();
        report!.TotalImDuty.Should().Be(80m);
        report.TotalExDuty.Should().Be(80m);
        report.TotalCredited.Should().Be(80m);
        report.Variance.Should().Be(0m);
        report.IsReconciled.Should().BeTrue();
        report.Lines.Should().HaveCount(2, "two IM lines on the seeded declaration");
        report.LinesRazdolzeno.Should().Be(0);
        report.AllLinesFlagged.Should().BeFalse();
    }

    [Fact]
    public async Task MarkLine_OnDraftLine_FlipsFlagWithTimestamp()
    {
        var client = await AuthedAsync();
        var (orderId, lineId, _) = await SeedReconciledOrderAsync();

        var resp = await client.PostAsJsonAsync(
            $"/api/ClientOrders/{orderId}/razdolzuvanje/mark-line",
            new { lineId, razdolzenaDaNe = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var line = await ctx.CustomsDeclarationLines.IgnoreQueryFilters()
            .FirstAsync(l => l.Id == lineId);
        line.RazdolzenaDaNe.Should().BeTrue();
        line.RazdolzenaAt.Should().NotBeNull();
        line.RazdolzenaBy.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MarkLine_OnLineFromDifferentOrder_Returns400()
    {
        var client = await AuthedAsync();
        var (orderA, _, _) = await SeedReconciledOrderAsync();
        var (_, foreignLine, _) = await SeedReconciledOrderAsync();

        var resp = await client.PostAsJsonAsync(
            $"/api/ClientOrders/{orderA}/razdolzuvanje/mark-line",
            new { lineId = foreignLine, razdolzenaDaNe = true });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TakeSnapshot_ReconciledOrderWithAllLinesFlagged_AutoClosesOrder()
    {
        var client = await AuthedAsync();
        var (orderId, line1, line2) = await SeedReconciledOrderAsync();
        // Flag both IM lines.
        foreach (var l in new[] { line1, line2 })
        {
            (await client.PostAsJsonAsync(
                $"/api/ClientOrders/{orderId}/razdolzuvanje/mark-line",
                new { lineId = l, razdolzenaDaNe = true })).EnsureSuccessStatusCode();
        }

        var resp = await client.PostAsJsonAsync(
            $"/api/ClientOrders/{orderId}/razdolzuvanje/snapshot",
            new { });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await resp.Content.ReadFromJsonAsync<SnapshotEnvelope>();
        env!.IsSuccess.Should().BeTrue();
        env.Data!.IsReconciled.Should().BeTrue();
        env.Data.AllLinesFlagged.Should().BeTrue();
        env.Data.ClosedClientOrder.Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var order = await ctx.ClientOrders.IgnoreQueryFilters().FirstAsync(o => o.Id == orderId);
        order.Status.Should().Be(ClientOrderStatus.Closed);
    }

    [Fact]
    public async Task TakeSnapshot_WithUnflaggedLines_DoesNotClose()
    {
        var client = await AuthedAsync();
        var (orderId, line1, _) = await SeedReconciledOrderAsync();
        // Flag only one of two lines.
        (await client.PostAsJsonAsync(
            $"/api/ClientOrders/{orderId}/razdolzuvanje/mark-line",
            new { lineId = line1, razdolzenaDaNe = true })).EnsureSuccessStatusCode();

        var resp = await client.PostAsJsonAsync(
            $"/api/ClientOrders/{orderId}/razdolzuvanje/snapshot",
            new { });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await resp.Content.ReadFromJsonAsync<SnapshotEnvelope>();
        env!.Data!.AllLinesFlagged.Should().BeFalse();
        env.Data.ClosedClientOrder.Should().BeFalse();

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var order = await ctx.ClientOrders.IgnoreQueryFilters().FirstAsync(o => o.Id == orderId);
        order.Status.Should().NotBe(ClientOrderStatus.Closed);
    }

    [Fact]
    public async Task Pdf_ReturnsHtmlContent_WithOrderNumber()
    {
        var client = await AuthedAsync();
        var (orderId, _, _) = await SeedReconciledOrderAsync();

        var resp = await client.GetAsync($"/api/ClientOrders/{orderId}/razdolzuvanje/pdf");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("Razdolzuvanje");
        body.Should().Contain("RZD-CO-");
    }

    [Fact]
    public async Task Pee060_ReturnsXmlForAuthorizationWindow()
    {
        var client = await AuthedAsync();
        var (orderId, _, _) = await SeedReconciledOrderAsync();

        var resp = await client.GetAsync($"/api/ClientOrders/{orderId}/razdolzuvanje/pee060");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("application/xml");
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("PEE060");
    }

    private sealed record LoginResp(string AccessToken);
    private sealed record RazdolzuvanjeResp(
        Guid ClientOrderId,
        string OrderNumber,
        int Status,
        decimal TotalImDuty,
        decimal TotalExDuty,
        decimal TotalCredited,
        decimal Variance,
        bool IsReconciled,
        int TotalLines,
        int LinesRazdolzeno,
        bool AllLinesFlagged,
        List<RazdolzuvanjeLineResp> Lines);
    private sealed record RazdolzuvanjeLineResp(Guid LineId, decimal DutyAmount, bool RazdolzenaDaNe);
    private sealed record SnapshotEnvelope(bool IsSuccess, SnapshotData? Data, string? ErrorMessage);
    private sealed record SnapshotData(
        int SnapshotRowsCreated,
        bool ClosedClientOrder,
        bool IsReconciled,
        bool AllLinesFlagged,
        decimal Variance);
}
