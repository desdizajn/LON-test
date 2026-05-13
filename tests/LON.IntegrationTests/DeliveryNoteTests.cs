using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.Logistics;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// Phase 17 §E7.6 — DeliveryNote CRUD + status transitions + auto-gen.
///
/// Auto-gen happens inside <c>CreateMaterialIssueCommandHandler</c> when the
/// source InventoryBalance carries an <c>AssignedProducerId</c> (stamped by
/// the §E6 Podelba flow). The full chain (Podelba → MaterialIssue) lives in
/// <c>PodelbaToProducerTests</c> + <c>MaterialIssueTests</c>; this file covers
/// the CRUD surface + transitions directly so failures point at the right
/// layer.
/// </summary>
public class DeliveryNoteTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public DeliveryNoteTests(LonApiFactory factory) => _factory = factory;

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

    private async Task<Guid> SeedDeliveryNoteAsync(DeliveryNoteStatus status = DeliveryNoteStatus.Draft)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tenant = await ctx.Tenants.FirstAsync(t => t.Code == "TEKSPORT");
        var wh = await ctx.Warehouses.FirstAsync(w => w.IsActive && !w.IsDeleted);
        var loc = await ctx.Locations.FirstAsync(l => l.WarehouseId == wh.Id && !l.IsDeleted);
        var partner = await ctx.Partners
            .FirstOrDefaultAsync(p => p.Type == PartnerType.Producer && p.IsActive && !p.IsDeleted);
        if (partner is null)
        {
            partner = new Partner
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Code = $"DN-PRD-{Guid.NewGuid():N}".Substring(0, 15),
                Name = "DN test producer",
                Type = PartnerType.Producer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            };
            ctx.Partners.Add(partner);
            await ctx.SaveChangesAsync();
        }
        var item = await ctx.Items.FirstAsync(i => !i.IsDeleted);
        var uom = await ctx.UnitsOfMeasure.FirstAsync(u => !u.IsDeleted);

        var dn = new DeliveryNote
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Number = $"DN-2026-test{Guid.NewGuid().ToString()[..6]}",
            DocumentType = DeliveryNoteType.ProducerDispatch,
            RelatedDocumentId = Guid.NewGuid(),
            DispatchDate = DateTime.UtcNow.Date,
            FromLocationId = loc.Id,
            ToPartnerId = partner.Id,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };
        var line = new DeliveryNoteLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            DeliveryNoteId = dn.Id,
            ItemId = item.Id,
            Description = item.Name ?? item.Code,
            Quantity = 5m,
            UoMId = uom.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };
        ctx.DeliveryNotes.Add(dn);
        ctx.DeliveryNoteLines.Add(line);
        await ctx.SaveChangesAsync();
        return dn.Id;
    }

    [Fact]
    public async Task GetById_ReturnsLinesAndStatusName()
    {
        var client = await AuthedAsync();
        var id = await SeedDeliveryNoteAsync();

        var resp = await client.GetAsync($"/api/Logistics/delivery-notes/{id}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await resp.Content.ReadFromJsonAsync<Envelope<DeliveryNoteResponse>>();
        env.Should().NotBeNull();
        env!.IsSuccess.Should().BeTrue();
        env.Data!.Id.Should().Be(id);
        env.Data.StatusName.Should().Be("Draft");
        env.Data.DocumentTypeName.Should().Be("ProducerDispatch");
        env.Data.Lines.Should().HaveCount(1);
    }

    [Fact]
    public async Task Update_OnDraft_PersistsDriverAndRemarks()
    {
        var client = await AuthedAsync();
        var id = await SeedDeliveryNoteAsync();

        var resp = await client.PutAsJsonAsync($"/api/Logistics/delivery-notes/{id}", new
        {
            driverName = "Иван",
            vehicleRegistration = "SK-1234-AB",
            remarks = "lot A",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dn = await ctx.DeliveryNotes.FirstAsync(d => d.Id == id);
        dn.DriverName.Should().Be("Иван");
        dn.VehicleRegistration.Should().Be("SK-1234-AB");
        dn.Remarks.Should().Be("lot A");
    }

    [Fact]
    public async Task Update_OnSent_Returns400()
    {
        var client = await AuthedAsync();
        var id = await SeedDeliveryNoteAsync(DeliveryNoteStatus.Sent);

        var resp = await client.PutAsJsonAsync($"/api/Logistics/delivery-notes/{id}", new
        {
            driverName = "should fail",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Confirm_DraftFlipsToSent()
    {
        var client = await AuthedAsync();
        var id = await SeedDeliveryNoteAsync();

        var resp = await client.PostAsync($"/api/Logistics/delivery-notes/{id}/confirm", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dn = await ctx.DeliveryNotes.FirstAsync(d => d.Id == id);
        dn.Status.Should().Be(DeliveryNoteStatus.Sent);
        dn.ConfirmedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Confirm_NonDraft_Returns400()
    {
        var client = await AuthedAsync();
        var id = await SeedDeliveryNoteAsync(DeliveryNoteStatus.Sent);

        var resp = await client.PostAsync($"/api/Logistics/delivery-notes/{id}/confirm", null);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancel_DraftFlipsToCancelled_WithReason()
    {
        var client = await AuthedAsync();
        var id = await SeedDeliveryNoteAsync();

        var resp = await client.PostAsJsonAsync($"/api/Logistics/delivery-notes/{id}/cancel", new { reason = "wrong producer" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dn = await ctx.DeliveryNotes.FirstAsync(d => d.Id == id);
        dn.Status.Should().Be(DeliveryNoteStatus.Cancelled);
        dn.CancelReason.Should().Be("wrong producer");
        dn.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Pdf_ReturnsHtmlContent()
    {
        var client = await AuthedAsync();
        var id = await SeedDeliveryNoteAsync();

        var resp = await client.GetAsync($"/api/Logistics/delivery-notes/{id}/pdf");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("Пропратница");
        body.Should().Contain("ProducerDispatch");
    }

    [Fact]
    public async Task GetList_FiltersByType()
    {
        var client = await AuthedAsync();
        var dispatchId = await SeedDeliveryNoteAsync();

        var resp = await client.GetAsync("/api/Logistics/delivery-notes?type=1");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await resp.Content.ReadFromJsonAsync<Envelope<List<DeliveryNoteResponse>>>();
        env.Should().NotBeNull();
        env!.IsSuccess.Should().BeTrue();
        env.Data!.Should().Contain(d => d.Id == dispatchId);
        env.Data.Should().OnlyContain(d => d.DocumentType == 1);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record Envelope<T>(bool IsSuccess, T? Data, string? ErrorMessage);
    private sealed record DeliveryNoteResponse(
        Guid Id,
        string Number,
        int DocumentType,
        string DocumentTypeName,
        int Status,
        string StatusName,
        List<DeliveryNoteLineResponse> Lines);
    private sealed record DeliveryNoteLineResponse(Guid Id, decimal Quantity);
}
