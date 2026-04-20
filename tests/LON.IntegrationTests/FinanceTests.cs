using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.MasterData;
using LON.Domain.Entities.Production;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P12.2 / P12.3 regression guard — client contracts + rate cards, invoice
/// lifecycle, and GenerateFromPO rate-resolution. Follows Contract Hygiene
/// Protocol §3: POST → GET → DB-level assert.
/// </summary>
public class FinanceTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;
    public FinanceTests(LonApiFactory factory) => _factory = factory;

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

    private async Task<(Guid PartnerId, Guid ItemId, Guid UoMId)> SeedFixturesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = ctx.Tenants.First();

        var partner = await ctx.Partners.FirstOrDefaultAsync(p => p.Code == "P12-CUST");
        if (partner is null)
        {
            partner = new Partner
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Code = "P12-CUST",
                Name = "Phase12 Customer",
                Type = PartnerType.Customer,
                IsActive = true,
            };
            ctx.Partners.Add(partner);
            await ctx.SaveChangesAsync();
        }

        var uom = ctx.UnitsOfMeasure.First();
        var item = await ctx.Items.FirstOrDefaultAsync(i => i.Code == "P12-FG");
        if (item is null)
        {
            item = new Item
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Code = "P12-FG",
                Name = "Phase12 finished good",
                Type = ItemType.FinishedGood,
                BaseUoMId = uom.Id,
            };
            ctx.Items.Add(item);
            await ctx.SaveChangesAsync();
        }

        return (partner.Id, item.Id, uom.Id);
    }

    [Fact]
    public async Task CreateContract_WithRateCard_PersistsBothRowsAndReturnsOnGet()
    {
        var client = await AuthedAsync();
        var (partnerId, itemId, _) = await SeedFixturesAsync();

        var number = $"CT-{Guid.NewGuid():N}".Substring(0, 10);
        var create = await client.PostAsJsonAsync("/api/Finance/contracts", new
        {
            number,
            partnerId,
            validFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            validTo = (DateTime?)new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            paymentTermsDays = 45,
            currency = "EUR",
            notes = "test",
            rateCard = new[]
            {
                new {
                    rateType = (int)RateType.PerPiece,
                    itemId = (Guid?)itemId,
                    operationCode = (string?)null,
                    ratePerUnit = 2.50m,
                    currency = "EUR",
                    validFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    validTo = (DateTime?)null,
                    notes = (string?)"piece"
                },
                new {
                    rateType = (int)RateType.PerMinute,
                    itemId = (Guid?)null,
                    operationCode = (string?)"SEW",
                    ratePerUnit = 0.30m,
                    currency = "EUR",
                    validFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    validTo = (DateTime?)null,
                    notes = (string?)"min"
                }
            }
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var createBody = await create.Content.ReadFromJsonAsync<Envelope<Guid>>();
        var contractId = createBody!.Data;

        var get = await client.GetFromJsonAsync<Envelope<ContractRow>>($"/api/Finance/contracts/{contractId}");
        get!.Data!.Number.Should().Be(number);
        get.Data.PaymentTermsDays.Should().Be(45);
        get.Data.RateCard.Should().HaveCount(2);
        get.Data.RateCard.Should().Contain(r => r.RateType == (int)RateType.PerPiece && r.RatePerUnit == 2.50m);
        get.Data.RateCard.Should().Contain(r => r.RateType == (int)RateType.PerMinute && r.OperationCode == "SEW");

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rates = await ctx.RateCardEntries.AsNoTracking()
            .Where(r => r.ContractId == contractId).ToListAsync();
        rates.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateContract_WithPerPieceRateMissingItem_Returns400_WithErrorCode()
    {
        var client = await AuthedAsync();
        var (partnerId, _, _) = await SeedFixturesAsync();

        var resp = await client.PostAsJsonAsync("/api/Finance/contracts", new
        {
            number = $"CT-BAD-{Guid.NewGuid():N}".Substring(0, 12),
            partnerId,
            validFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            paymentTermsDays = 30,
            currency = "EUR",
            rateCard = new[]
            {
                new {
                    rateType = (int)RateType.PerPiece,
                    itemId = (Guid?)null,
                    operationCode = (string?)null,
                    ratePerUnit = 1.0m,
                    currency = "EUR",
                    validFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    validTo = (DateTime?)null,
                    notes = (string?)null
                }
            }
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<FailureEnvelope>();
        body!.ErrorCode.Should().Be("contract.rate_missing_item");
    }

    [Fact]
    public async Task GenerateFromPo_UsesContractRate_AndIssueSequentialNumber()
    {
        var client = await AuthedAsync();
        var (partnerId, itemId, uomId) = await SeedFixturesAsync();

        // 1) Contract with a PerPiece rate covering today.
        var contractNumber = $"CT-GEN-{Guid.NewGuid():N}".Substring(0, 12);
        var createContract = await client.PostAsJsonAsync("/api/Finance/contracts", new
        {
            number = contractNumber,
            partnerId,
            validFrom = DateTime.UtcNow.Date.AddYears(-1),
            validTo = (DateTime?)DateTime.UtcNow.Date.AddYears(1),
            paymentTermsDays = 15,
            currency = "EUR",
            rateCard = new[]
            {
                new {
                    rateType = (int)RateType.PerPiece,
                    itemId = (Guid?)itemId,
                    operationCode = (string?)null,
                    ratePerUnit = 1.75m,
                    currency = "EUR",
                    validFrom = DateTime.UtcNow.Date.AddMonths(-6),
                    validTo = (DateTime?)null,
                    notes = (string?)null
                }
            }
        });
        createContract.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2) Seed a completed PO with CustomerPartnerId = partner.
        Guid poId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = ctx.Tenants.First();
            var po = new ProductionOrder
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                OrderNumber = $"P12-PO-{Guid.NewGuid():N}".Substring(0, 14),
                ItemId = itemId,
                UoMId = uomId,
                OrderQuantity = 100m,
                ProducedQuantity = 80m,
                Status = ProductionOrderStatus.Completed,
                PlannedStartDate = DateTime.UtcNow.AddDays(-14),
                PlannedEndDate = DateTime.UtcNow.AddDays(-2),
                ActualEndDate = DateTime.UtcNow.AddDays(-2),
                CustomerPartnerId = partnerId,
            };
            ctx.ProductionOrders.Add(po);
            await ctx.SaveChangesAsync();
            poId = po.Id;
        }

        // 3) Generate invoice from PO — rate should resolve to 1.75 × 80 = 140.
        var gen = await client.PostAsJsonAsync("/api/Finance/invoices/generate-from-po",
            new { productionOrderId = poId, contractId = (Guid?)null, overrideUnitPrice = (decimal?)null, issueDate = (DateTime?)null });
        gen.StatusCode.Should().Be(HttpStatusCode.OK);
        var genBody = await gen.Content.ReadFromJsonAsync<Envelope<Guid>>();
        var invoiceId = genBody!.Data;

        var draft = await client.GetFromJsonAsync<Envelope<InvoiceRow>>($"/api/Finance/invoices/{invoiceId}");
        draft!.Data!.Status.Should().Be((int)InvoiceStatus.Draft);
        draft.Data.TotalAmount.Should().Be(140.00m);
        draft.Data.Number.Should().StartWith("DRAFT-");
        draft.Data.Lines.Should().HaveCount(1);
        draft.Data.Lines[0].Quantity.Should().Be(80m);
        draft.Data.Lines[0].UnitPrice.Should().Be(1.75m);
        draft.Data.Lines[0].RelatedProductionOrderId.Should().Be(poId);

        // 4) Issue — number must become INV-{year}-0001 or next in sequence.
        var issue = await client.PostAsync($"/api/Finance/invoices/{invoiceId}/issue", null);
        issue.StatusCode.Should().Be(HttpStatusCode.OK);
        var issueBody = await issue.Content.ReadFromJsonAsync<Envelope<string>>();
        issueBody!.Data.Should().MatchRegex(@"^INV-\d{4}-\d{4}$");

        var issued = await client.GetFromJsonAsync<Envelope<InvoiceRow>>($"/api/Finance/invoices/{invoiceId}");
        issued!.Data!.Status.Should().Be((int)InvoiceStatus.Issued);
        issued.Data.Number.Should().Be(issueBody.Data);
    }

    [Fact]
    public async Task GenerateFromPo_NoContractAndNoOverride_Returns400()
    {
        var client = await AuthedAsync();
        var (partnerId, itemId, uomId) = await SeedFixturesAsync();

        Guid poId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = ctx.Tenants.First();

            // Use a fresh partner with NO contract so the lookup returns null.
            var isolated = new Partner
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Code = $"P12-NC-{Guid.NewGuid():N}".Substring(0, 10),
                Name = "No-contract customer",
                Type = PartnerType.Customer,
                IsActive = true,
            };
            ctx.Partners.Add(isolated);

            var po = new ProductionOrder
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                OrderNumber = $"P12-NCPO-{Guid.NewGuid():N}".Substring(0, 14),
                ItemId = itemId,
                UoMId = uomId,
                OrderQuantity = 10m,
                ProducedQuantity = 10m,
                Status = ProductionOrderStatus.Completed,
                PlannedStartDate = DateTime.UtcNow.AddDays(-7),
                PlannedEndDate = DateTime.UtcNow.AddDays(-1),
                ActualEndDate = DateTime.UtcNow.AddDays(-1),
                CustomerPartnerId = isolated.Id,
            };
            ctx.ProductionOrders.Add(po);
            await ctx.SaveChangesAsync();
            poId = po.Id;
        }

        var resp = await client.PostAsJsonAsync("/api/Finance/invoices/generate-from-po",
            new { productionOrderId = poId, contractId = (Guid?)null, overrideUnitPrice = (decimal?)null, issueDate = (DateTime?)null });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<FailureEnvelope>();
        body!.ErrorCode.Should().Be("invoice.no_contract");
    }

    [Fact]
    public async Task IssueInvoice_WithoutLines_Returns400()
    {
        var client = await AuthedAsync();
        var (partnerId, _, _) = await SeedFixturesAsync();

        var create = await client.PostAsJsonAsync("/api/Finance/invoices", new
        {
            partnerId,
            contractId = (Guid?)null,
            issueDate = DateTime.UtcNow,
            dueDate = (DateTime?)null,
            currency = "EUR",
            notes = "empty",
            lines = (object[]?)null,
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await create.Content.ReadFromJsonAsync<Envelope<Guid>>();
        var invoiceId = body!.Data;

        var issue = await client.PostAsync($"/api/Finance/invoices/{invoiceId}/issue", null);
        issue.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var err = await issue.Content.ReadFromJsonAsync<FailureEnvelope>();
        err!.ErrorCode.Should().Be("invoice.no_lines");
    }

    [Fact]
    public async Task CancelPaidInvoice_Returns400()
    {
        var client = await AuthedAsync();
        var (partnerId, itemId, uomId) = await SeedFixturesAsync();

        var createInvoice = await client.PostAsJsonAsync("/api/Finance/invoices", new
        {
            partnerId,
            contractId = (Guid?)null,
            issueDate = DateTime.UtcNow,
            dueDate = (DateTime?)null,
            currency = "EUR",
            notes = "paid-test",
            lines = new[]
            {
                new {
                    description = "manual",
                    itemId = (Guid?)itemId,
                    relatedProductionOrderId = (Guid?)null,
                    relatedShipmentId = (Guid?)null,
                    quantity = 5m,
                    unitPrice = 10m,
                }
            }
        });
        createInvoice.StatusCode.Should().Be(HttpStatusCode.OK);
        var invoiceId = (await createInvoice.Content.ReadFromJsonAsync<Envelope<Guid>>())!.Data;

        (await client.PostAsync($"/api/Finance/invoices/{invoiceId}/issue", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"/api/Finance/invoices/{invoiceId}/mark-paid", new { paidAt = (DateTime?)null }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var cancel = await client.PostAsJsonAsync($"/api/Finance/invoices/{invoiceId}/cancel", new { reason = "too late" });
        cancel.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var err = await cancel.Content.ReadFromJsonAsync<FailureEnvelope>();
        err!.ErrorCode.Should().Be("invoice.paid_immutable");
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record Envelope<T>(bool IsSuccess, T? Data, string? ErrorMessage, string? ErrorCode);
    private sealed record FailureEnvelope(bool IsSuccess, string? ErrorMessage, string? ErrorCode);
    private sealed record RateRow(Guid Id, Guid ContractId, int RateType, Guid? ItemId,
        string? ItemCode, string? ItemName, string? OperationCode, decimal RatePerUnit,
        string Currency, DateTime ValidFrom, DateTime? ValidTo, string? Notes);
    private sealed record ContractRow(Guid Id, string Number, Guid PartnerId, string PartnerName,
        DateTime ValidFrom, DateTime? ValidTo, int PaymentTermsDays, string Currency,
        bool IsActive, string? Notes, List<RateRow> RateCard);
    private sealed record InvoiceLineRow(Guid Id, int LineNumber, string Description, Guid? ItemId,
        string? ItemCode, Guid? RelatedProductionOrderId, string? RelatedProductionOrderNumber,
        Guid? RelatedShipmentId, decimal Quantity, decimal UnitPrice, decimal LineTotal);
    private sealed record InvoiceRow(Guid Id, string Number, Guid PartnerId, string PartnerName,
        Guid? ContractId, string? ContractNumber, DateTime IssueDate, DateTime DueDate,
        string Currency, decimal SubTotal, decimal TotalAmount, int Status,
        DateTime? IssuedAt, DateTime? PaidAt, string? Notes, List<InvoiceLineRow> Lines);
}
