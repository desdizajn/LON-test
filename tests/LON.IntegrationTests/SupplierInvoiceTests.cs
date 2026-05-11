using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P16.C3.c — SupplierInvoice CRUD + derived `Overdue` filter logic.
/// </summary>
public class SupplierInvoiceTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public SupplierInvoiceTests(LonApiFactory factory) => _factory = factory;

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

    private async Task<Guid> SeedSupplierAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var p = await db.Partners.FirstOrDefaultAsync(x => x.Code == "P16C3C-SUPP");
        if (p is not null) return p.Id;
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Code == "TEKSPORT");
        p = new LON.Domain.Entities.MasterData.Partner
        {
            Id = Guid.NewGuid(),
            Code = "P16C3C-SUPP",
            Name = "Test Supplier for C3.c",
            Type = PartnerType.Supplier,
            IsActive = true,
            TenantId = tenant.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "C3cTest",
        };
        db.Partners.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task Create_GetById_ReturnsTheInvoice()
    {
        var client = await AuthedAsync();
        var supplierId = await SeedSupplierAsync();

        var create = await client.PostAsJsonAsync("/api/Finance/supplier-invoices", new
        {
            number = $"SI-{Guid.NewGuid().ToString().Substring(0, 6)}",
            supplierPartnerId = supplierId,
            invoiceDate = DateTime.UtcNow.Date,
            dueDate = DateTime.UtcNow.Date.AddDays(30),
            amount = 1000m,
            currency = "EUR",
            notes = "Energy bill",
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var id = (await create.Content.ReadFromJsonAsync<ResultEnvelope<InvoiceDto>>())!.Data!.Id;

        var get = await client.GetFromJsonAsync<ResultEnvelope<InvoiceDto>>(
            $"/api/Finance/supplier-invoices/{id}");
        get!.Data!.Amount.Should().Be(1000m);
        get.Data.Notes.Should().Be("Energy bill");
        get.Data.Status.Should().Be(1, "newly created is Open (not yet past due)");
    }

    [Fact]
    public async Task OverdueIsDerived_FromOpenStatusAndDueDateInThePast()
    {
        var client = await AuthedAsync();
        var supplierId = await SeedSupplierAsync();

        // Backdate due to yesterday → projected Overdue.
        var create = await client.PostAsJsonAsync("/api/Finance/supplier-invoices", new
        {
            number = $"SI-OV-{Guid.NewGuid().ToString().Substring(0, 6)}",
            supplierPartnerId = supplierId,
            invoiceDate = DateTime.UtcNow.Date.AddDays(-60),
            dueDate = DateTime.UtcNow.Date.AddDays(-1),
            amount = 200m,
            currency = "EUR",
        });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<ResultEnvelope<InvoiceDto>>())!.Data!.Id;

        var single = await client.GetFromJsonAsync<ResultEnvelope<InvoiceDto>>(
            $"/api/Finance/supplier-invoices/{id}");
        single!.Data!.Status.Should().Be(4, "DueDate < today + Status=Open -> projected Overdue");

        var overdueOnly = await client.GetFromJsonAsync<ResultEnvelope<List<InvoiceDto>>>(
            "/api/Finance/supplier-invoices?status=4"); // Overdue
        overdueOnly!.Data!.Should().Contain(i => i.Id == id);
        overdueOnly.Data.Should().OnlyContain(i => i.Status == 4);

        var openOnly = await client.GetFromJsonAsync<ResultEnvelope<List<InvoiceDto>>>(
            "/api/Finance/supplier-invoices?status=1"); // Open (not overdue)
        openOnly!.Data!.Should().NotContain(i => i.Id == id);
    }

    [Fact]
    public async Task MarkPaid_SetsPaidDate_AndExcludesFromOverdue()
    {
        var client = await AuthedAsync();
        var supplierId = await SeedSupplierAsync();

        var create = await client.PostAsJsonAsync("/api/Finance/supplier-invoices", new
        {
            number = $"SI-PAID-{Guid.NewGuid().ToString().Substring(0, 6)}",
            supplierPartnerId = supplierId,
            invoiceDate = DateTime.UtcNow.Date.AddDays(-90),
            dueDate = DateTime.UtcNow.Date.AddDays(-30),
            amount = 500m,
            currency = "EUR",
        });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<ResultEnvelope<InvoiceDto>>())!.Data!.Id;

        var put = await client.PutAsJsonAsync($"/api/Finance/supplier-invoices/{id}", new
        {
            id,
            number = "ignored-number-replaced-by-route",
            supplierPartnerId = supplierId,
            invoiceDate = DateTime.UtcNow.Date.AddDays(-90),
            dueDate = DateTime.UtcNow.Date.AddDays(-30),
            amount = 500m,
            currency = "EUR",
            status = 2, // Paid
            paidDate = DateTime.UtcNow.Date,
        });
        // Note: number must round-trip, so re-send the existing number.
        // Quick re-fetch + correct PUT.
        if (put.StatusCode != HttpStatusCode.OK)
        {
            var existing = (await (await client.GetAsync($"/api/Finance/supplier-invoices/{id}"))
                .Content.ReadFromJsonAsync<ResultEnvelope<InvoiceDto>>())!.Data!;
            put = await client.PutAsJsonAsync($"/api/Finance/supplier-invoices/{id}", new
            {
                id,
                number = existing.Number,
                supplierPartnerId = supplierId,
                invoiceDate = existing.InvoiceDate,
                dueDate = existing.DueDate,
                amount = existing.Amount,
                currency = existing.Currency,
                status = 2,
                paidDate = DateTime.UtcNow.Date,
            });
        }
        put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());

        var get = await client.GetFromJsonAsync<ResultEnvelope<InvoiceDto>>(
            $"/api/Finance/supplier-invoices/{id}");
        get!.Data!.Status.Should().Be(2, "Paid is persisted");
        get.Data.PaidDate.Should().NotBeNull();

        var overdueOnly = await client.GetFromJsonAsync<ResultEnvelope<List<InvoiceDto>>>(
            "/api/Finance/supplier-invoices?status=4");
        overdueOnly!.Data!.Should().NotContain(i => i.Id == id);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record ResultEnvelope<T>(bool IsSuccess, T? Data, string? ErrorMessage);
    private sealed record InvoiceDto(
        Guid Id, Guid TenantId, string Number, Guid SupplierPartnerId,
        string? SupplierCode, string? SupplierName,
        DateTime InvoiceDate, DateTime DueDate, decimal Amount, string Currency,
        int Status, DateTime? PaidDate, string? Notes);
}
