using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.Customs;
using LON.Domain.Entities.MasterData;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// Phase 17 §E8.5 (D4) — CommercialInvoice CRUD + numbering + tenant isolation
/// + status transitions + suggest-from-shipment + PDF render.
/// </summary>
public class CommercialInvoiceTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public CommercialInvoiceTests(LonApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthedAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    private async Task<(Guid consignor, Guid consignee, Guid itemId, Guid uomId)> SeedReferencesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = await ctx.Tenants.FirstAsync(t => t.Code == "TEKSPORT");

        // Consignor: any active customer-type partner; the consignee is a distinct one.
        var consignor = await ctx.Partners
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Type == PartnerType.Customer && !p.IsDeleted);
        if (consignor is null)
        {
            consignor = new Partner
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Code = $"CI-CONS-{Guid.NewGuid():N}".Substring(0, 14),
                Name = "CI consignor",
                Type = PartnerType.Customer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            };
            ctx.Partners.Add(consignor);
            await ctx.SaveChangesAsync();
        }
        var consignee = new Partner
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Code = $"CI-CSE-{Guid.NewGuid():N}".Substring(0, 14),
            Name = "CI consignee",
            Type = PartnerType.Customer,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };
        ctx.Partners.Add(consignee);

        var item = await ctx.Items.IgnoreQueryFilters().FirstAsync(i => !i.IsDeleted);
        var uom = await ctx.UnitsOfMeasure.IgnoreQueryFilters().FirstAsync(u => !u.IsDeleted);

        await ctx.SaveChangesAsync();
        return (consignor.Id, consignee.Id, item.Id, uom.Id);
    }

    private async Task<Guid> CreateDraftAsync(HttpClient client)
    {
        var (consignor, consignee, itemId, uomId) = await SeedReferencesAsync();
        var payload = new
        {
            consigneePartnerId = consignee,
            consignorPartnerId = consignor,
            invoiceDate = DateTime.UtcNow.Date,
            currency = "EUR",
            incoterms = "FOB",
            countryOfDestination = "DE",
            paymentTerms = "Net 30",
            lines = new[]
            {
                new
                {
                    itemId,
                    description = "Test item line",
                    quantity = 10m,
                    uoMId = uomId,
                    unitPrice = 5m,
                    countryOfOrigin = "MK",
                },
            },
        };
        var resp = await client.PostAsJsonAsync("/api/Customs/commercial-invoices", payload);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        var env = await resp.Content.ReadFromJsonAsync<ResultEnvelope<Guid>>();
        env!.IsSuccess.Should().BeTrue();
        return env.Data;
    }

    [Fact]
    public async Task Create_GeneratesCISequenceNumber_And_ComputesTotals()
    {
        var client = await AuthedAsync();
        var id = await CreateDraftAsync(client);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ci = await ctx.CommercialInvoices.IgnoreQueryFilters().FirstAsync(c => c.Id == id);
        ci.Number.Should().MatchRegex(@"^CI-\d{4}-\d{6}$");
        ci.Status.Should().Be(CommercialInvoiceStatus.Draft);
        ci.Subtotal.Should().Be(50m);          // 10 × 5
        ci.TotalAmount.Should().Be(50m);
        ci.TenantId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Create_WithEmptyLines_Returns400()
    {
        var client = await AuthedAsync();
        var (consignor, consignee, _, _) = await SeedReferencesAsync();

        var resp = await client.PostAsJsonAsync("/api/Customs/commercial-invoices", new
        {
            consigneePartnerId = consignee,
            consignorPartnerId = consignor,
            invoiceDate = DateTime.UtcNow.Date,
            currency = "EUR",
            incoterms = "FOB",
            lines = Array.Empty<object>(),
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetById_ReturnsLinesAndPartyNames()
    {
        var client = await AuthedAsync();
        var id = await CreateDraftAsync(client);

        var resp = await client.GetAsync($"/api/Customs/commercial-invoices/{id}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await resp.Content.ReadFromJsonAsync<ResultEnvelope<CiDto>>();
        env!.IsSuccess.Should().BeTrue();
        env.Data!.Id.Should().Be(id);
        env.Data.Lines.Should().HaveCount(1);
        env.Data.StatusName.Should().Be("Draft");
        env.Data.ConsigneeName.Should().NotBeNullOrEmpty();
        env.Data.ConsignorName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ParallelCreates_ProduceDistinctCINumbers()
    {
        var client = await AuthedAsync();
        var (consignor, consignee, itemId, uomId) = await SeedReferencesAsync();

        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            var resp = await client.PostAsJsonAsync("/api/Customs/commercial-invoices", new
            {
                consigneePartnerId = consignee,
                consignorPartnerId = consignor,
                invoiceDate = DateTime.UtcNow.Date,
                currency = "EUR",
                incoterms = "FOB",
                lines = new[]
                {
                    new
                    {
                        itemId,
                        description = $"parallel {i}",
                        quantity = 1m,
                        uoMId = uomId,
                        unitPrice = 1m,
                    },
                },
            });
            resp.EnsureSuccessStatusCode();
            var env = await resp.Content.ReadFromJsonAsync<ResultEnvelope<Guid>>();
            return env!.Data;
        });

        var ids = await Task.WhenAll(tasks);
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var numbers = await ctx.CommercialInvoices.IgnoreQueryFilters()
            .Where(c => ids.Contains(c.Id))
            .Select(c => c.Number)
            .ToListAsync();
        numbers.Should().HaveCount(5);
        numbers.Distinct().Should().HaveCount(5, "SQL SEQUENCE must yield distinct values under concurrency");
    }

    [Fact]
    public async Task Update_OnDraft_RecomputesTotals()
    {
        var client = await AuthedAsync();
        var id = await CreateDraftAsync(client);
        var (_, _, itemId, uomId) = await SeedReferencesAsync();

        var resp = await client.PutAsJsonAsync($"/api/Customs/commercial-invoices/{id}", new
        {
            taxAmount = 3.5m,
            lines = new[]
            {
                new { itemId, description = "edited", quantity = 4m, uoMId = uomId, unitPrice = 10m },
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ci = await ctx.CommercialInvoices.Include(c => c.Lines).IgnoreQueryFilters().FirstAsync(c => c.Id == id);
        ci.Lines.Should().HaveCount(1);
        ci.Subtotal.Should().Be(40m); // 4 × 10
        ci.TaxAmount.Should().Be(3.5m);
        ci.TotalAmount.Should().Be(43.5m);
    }

    [Fact]
    public async Task Issue_DraftFlipsToIssued_LocksUpdate()
    {
        var client = await AuthedAsync();
        var id = await CreateDraftAsync(client);

        var issue = await client.PostAsync($"/api/Customs/commercial-invoices/{id}/issue", null);
        issue.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ci = await ctx.CommercialInvoices.IgnoreQueryFilters().FirstAsync(c => c.Id == id);
            ci.Status.Should().Be(CommercialInvoiceStatus.Issued);
            ci.IssuedAt.Should().NotBeNull();
        }

        // Second issue must fail.
        var second = await client.PostAsync($"/api/Customs/commercial-invoices/{id}/issue", null);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Update on Issued must fail.
        var update = await client.PutAsJsonAsync($"/api/Customs/commercial-invoices/{id}", new { notes = "x" });
        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancel_FromIssued_RecordsReason()
    {
        var client = await AuthedAsync();
        var id = await CreateDraftAsync(client);
        (await client.PostAsync($"/api/Customs/commercial-invoices/{id}/issue", null)).EnsureSuccessStatusCode();

        var resp = await client.PostAsJsonAsync($"/api/Customs/commercial-invoices/{id}/cancel", new { reason = "rebooked under new MRN" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ci = await ctx.CommercialInvoices.IgnoreQueryFilters().FirstAsync(c => c.Id == id);
        ci.Status.Should().Be(CommercialInvoiceStatus.Cancelled);
        ci.CancellationReason.Should().Be("rebooked under new MRN");
        ci.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_OnDraft_SoftDeletes()
    {
        var client = await AuthedAsync();
        var id = await CreateDraftAsync(client);

        var resp = await client.DeleteAsync($"/api/Customs/commercial-invoices/{id}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ci = await ctx.CommercialInvoices.IgnoreQueryFilters().FirstAsync(c => c.Id == id);
        ci.IsDeleted.Should().BeTrue();
        ci.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SuggestFromShipment_ReturnsDraftWithLines()
    {
        var client = await AuthedAsync();
        var (consignor, consignee, itemId, uomId) = await SeedReferencesAsync();

        Guid shipmentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = await ctx.Tenants.FirstAsync(t => t.Code == "TEKSPORT");
            var ship = new Shipment
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                ShipmentNumber = $"SHP-TEST-{Guid.NewGuid().ToString()[..6]}",
                ShipmentDate = DateTime.UtcNow,
                CustomerId = consignee,
                Status = ShipmentStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            };
            ship.Lines.Add(new ShipmentLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                ShipmentId = ship.Id,
                LineNumber = 1,
                ItemId = itemId,
                BatchNumber = "B-001",
                Quantity = 7m,
                UoMId = uomId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            });
            ship.Lines.Add(new ShipmentLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                ShipmentId = ship.Id,
                LineNumber = 2,
                ItemId = itemId,
                BatchNumber = "B-002",
                Quantity = 3m,
                UoMId = uomId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test",
            });
            ctx.Shipments.Add(ship);
            await ctx.SaveChangesAsync();
            shipmentId = ship.Id;
            _ = consignor; // (no further use; just here to indicate sourcing)
        }

        var resp = await client.PostAsync(
            $"/api/Customs/commercial-invoices/suggest-from-shipment?shipmentId={shipmentId}",
            null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await resp.Content.ReadFromJsonAsync<ResultEnvelope<CiDto>>();
        env!.IsSuccess.Should().BeTrue();
        env.Data!.Lines.Should().HaveCount(2);
        env.Data.ShipmentId.Should().Be(shipmentId);
        env.Data.ConsigneePartnerId.Should().Be(consignee);
    }

    [Fact]
    public async Task Pdf_ReturnsHtmlContent_WithNumberAndLines()
    {
        var client = await AuthedAsync();
        var id = await CreateDraftAsync(client);

        var resp = await client.GetAsync($"/api/Customs/commercial-invoices/{id}/pdf");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("Commercial Invoice");
        body.Should().Contain("CI-");
    }

    [Fact]
    public async Task GetList_FiltersByStatus()
    {
        var client = await AuthedAsync();
        var draftId = await CreateDraftAsync(client);
        var issuedId = await CreateDraftAsync(client);
        (await client.PostAsync($"/api/Customs/commercial-invoices/{issuedId}/issue", null)).EnsureSuccessStatusCode();

        var resp = await client.GetAsync("/api/Customs/commercial-invoices?status=1");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await resp.Content.ReadFromJsonAsync<ResultEnvelope<List<CiDto>>>();
        env!.IsSuccess.Should().BeTrue();
        env.Data!.Should().Contain(c => c.Id == draftId);
        env.Data.Should().NotContain(c => c.Id == issuedId);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record ResultEnvelope<T>(bool IsSuccess, T Data, string? ErrorMessage);
    private sealed record CiDto(
        Guid Id,
        string Number,
        Guid? ShipmentId,
        Guid ConsigneePartnerId,
        string? ConsigneeName,
        string? ConsignorName,
        int Status,
        string StatusName,
        decimal Subtotal,
        decimal TotalAmount,
        List<CiLineDto> Lines);
    private sealed record CiLineDto(Guid Id, decimal Quantity, decimal UnitPrice, decimal LineTotal);
}
