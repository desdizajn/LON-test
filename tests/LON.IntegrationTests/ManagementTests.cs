using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.Customs;
using LON.Domain.Entities.Finance;
using LON.Domain.Entities.MasterData;
using LON.Domain.Entities.Production;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P13.1 / P13.3 / P13.5 regression guard — management KPI aggregates.
/// </summary>
public class ManagementTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;
    public ManagementTests(LonApiFactory factory) => _factory = factory;

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

    [Fact]
    public async Task OnTime_BucketsShipmentByPlannedEndDate()
    {
        var client = await AuthedAsync();

        Guid customerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = ctx.Tenants.First();
            var uom = ctx.UnitsOfMeasure.First();
            var warehouse = ctx.Warehouses.First();
            var location = ctx.Locations.First(l => l.WarehouseId == warehouse.Id);

            var customer = new Partner
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Code = $"P13-CUST-{Guid.NewGuid():N}".Substring(0, 12),
                Name = "P13 customer",
                Type = PartnerType.Customer,
                IsActive = true,
            };
            ctx.Partners.Add(customer);

            var item = new Item
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Code = $"P13-ITEM-{Guid.NewGuid():N}".Substring(0, 12),
                Name = "P13 finished good",
                Type = ItemType.FinishedGood,
                BaseUoMId = uom.Id,
            };
            ctx.Items.Add(item);

            // One on-time PO + shipment, one 10d-late PO + shipment.
            var onTimePo = new ProductionOrder
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                OrderNumber = $"P13-OT-{Guid.NewGuid():N}".Substring(0, 14),
                ItemId = item.Id,
                UoMId = uom.Id,
                OrderQuantity = 10m,
                ProducedQuantity = 10m,
                Status = ProductionOrderStatus.Completed,
                PlannedStartDate = DateTime.UtcNow.AddDays(-14),
                PlannedEndDate = DateTime.UtcNow.AddDays(-3),
                CustomerPartnerId = customer.Id,
            };
            var latePo = new ProductionOrder
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                OrderNumber = $"P13-LATE-{Guid.NewGuid():N}".Substring(0, 14),
                ItemId = item.Id,
                UoMId = uom.Id,
                OrderQuantity = 10m,
                ProducedQuantity = 10m,
                Status = ProductionOrderStatus.Completed,
                PlannedStartDate = DateTime.UtcNow.AddDays(-21),
                PlannedEndDate = DateTime.UtcNow.AddDays(-11), // promise 11d ago
                CustomerPartnerId = customer.Id,
            };
            ctx.ProductionOrders.AddRange(onTimePo, latePo);

            var batchOnTime = $"B-OT-{Guid.NewGuid():N}".Substring(0, 12);
            var batchLate = $"B-LATE-{Guid.NewGuid():N}".Substring(0, 12);

            ctx.ProductionReceipts.AddRange(
                new ProductionReceipt
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    ReceiptNumber = $"PR-{Guid.NewGuid():N}".Substring(0, 14),
                    ProductionOrderId = onTimePo.Id,
                    ItemId = item.Id,
                    BatchNumber = batchOnTime,
                    Quantity = 10m,
                    UoMId = uom.Id,
                    LocationId = location.Id,
                    ReceiptDate = DateTime.UtcNow.AddDays(-3),
                    QualityStatus = QualityStatus.OK,
                },
                new ProductionReceipt
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    ReceiptNumber = $"PR-{Guid.NewGuid():N}".Substring(0, 14),
                    ProductionOrderId = latePo.Id,
                    ItemId = item.Id,
                    BatchNumber = batchLate,
                    Quantity = 10m,
                    UoMId = uom.Id,
                    LocationId = location.Id,
                    ReceiptDate = DateTime.UtcNow.AddDays(-1),
                    QualityStatus = QualityStatus.OK,
                });

            var onTimeShipment = new Shipment
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                ShipmentNumber = $"SH-OT-{Guid.NewGuid():N}".Substring(0, 14),
                CustomerId = customer.Id,
                ShipmentDate = DateTime.UtcNow.AddDays(-3), // same day as planned end → on-time
                Status = ShipmentStatus.Shipped,
            };
            var lateShipment = new Shipment
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                ShipmentNumber = $"SH-LATE-{Guid.NewGuid():N}".Substring(0, 14),
                CustomerId = customer.Id,
                ShipmentDate = DateTime.UtcNow.AddDays(-1), // planned was -11, now -1 → 10d late
                Status = ShipmentStatus.Shipped,
            };
            ctx.Shipments.AddRange(onTimeShipment, lateShipment);

            ctx.ShipmentLines.AddRange(
                new ShipmentLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    ShipmentId = onTimeShipment.Id,
                    LineNumber = 1,
                    ItemId = item.Id,
                    BatchNumber = batchOnTime,
                    Quantity = 10m,
                    UoMId = uom.Id,
                },
                new ShipmentLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    ShipmentId = lateShipment.Id,
                    LineNumber = 1,
                    ItemId = item.Id,
                    BatchNumber = batchLate,
                    Quantity = 10m,
                    UoMId = uom.Id,
                });
            await ctx.SaveChangesAsync();
            customerId = customer.Id;
        }

        var resp = await client.GetFromJsonAsync<Envelope<OnTimeReportRow>>(
            $"/api/Management/on-time?from={DateTime.UtcNow.AddDays(-30):yyyy-MM-dd}&to={DateTime.UtcNow:yyyy-MM-dd}");
        resp!.Data.Should().NotBeNull();
        var customerRow = resp.Data!.ByCustomer.FirstOrDefault(r => r.CustomerId == customerId);
        customerRow.Should().NotBeNull();
        customerRow!.TotalShipments.Should().BeGreaterThanOrEqualTo(2);
        customerRow.OnTime.Should().BeGreaterThanOrEqualTo(1);
        customerRow.LateOver7.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ByCustomer_AggregatesPOsShipmentsAndInvoices()
    {
        var client = await AuthedAsync();

        Guid customerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = ctx.Tenants.First();
            var uom = ctx.UnitsOfMeasure.First();

            var customer = new Partner
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Code = $"P13-BY-{Guid.NewGuid():N}".Substring(0, 12),
                Name = "P13 bycustomer",
                Type = PartnerType.Customer,
                IsActive = true,
            };
            ctx.Partners.Add(customer);

            var item = new Item
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Code = $"P13-BY-FG-{Guid.NewGuid():N}".Substring(0, 14),
                Name = "P13 by-customer FG",
                Type = ItemType.FinishedGood,
                BaseUoMId = uom.Id,
            };
            ctx.Items.Add(item);

            var po = new ProductionOrder
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                OrderNumber = $"P13-BY-PO-{Guid.NewGuid():N}".Substring(0, 14),
                ItemId = item.Id,
                UoMId = uom.Id,
                OrderQuantity = 100m,
                ProducedQuantity = 70m,
                Status = ProductionOrderStatus.Completed,
                PlannedStartDate = DateTime.UtcNow.AddDays(-20),
                PlannedEndDate = DateTime.UtcNow.AddDays(-5),
                ActualEndDate = DateTime.UtcNow.AddDays(-4),
                CustomerPartnerId = customer.Id,
            };
            ctx.ProductionOrders.Add(po);

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Number = $"INV-BY-{Guid.NewGuid():N}".Substring(0, 14),
                PartnerId = customer.Id,
                IssueDate = DateTime.UtcNow.AddDays(-2),
                DueDate = DateTime.UtcNow.AddDays(28),
                Currency = "EUR",
                Status = InvoiceStatus.Issued,
                SubTotal = 123.45m,
                TotalAmount = 123.45m,
            };
            ctx.Invoices.Add(invoice);
            await ctx.SaveChangesAsync();
            customerId = customer.Id;
        }

        var resp = await client.GetFromJsonAsync<Envelope<ByCustomerRow>>(
            $"/api/Management/by-customer?from={DateTime.UtcNow.AddMonths(-1):yyyy-MM-dd}&to={DateTime.UtcNow:yyyy-MM-dd}");
        resp!.Data.Should().NotBeNull();
        var row = resp.Data!.Rows.FirstOrDefault(r => r.CustomerId == customerId);
        row.Should().NotBeNull();
        row!.CompletedPOs.Should().BeGreaterThanOrEqualTo(1);
        row.ProducedQuantity.Should().BeGreaterThanOrEqualTo(70m);
        row.InvoicesIssued.Should().BeGreaterThanOrEqualTo(1);
        row.InvoicedOutstanding.Should().BeGreaterThanOrEqualTo(123.45m);
    }

    [Fact]
    public async Task Alerts_IncludeMrnExpiringAndOverdueInvoice()
    {
        var client = await AuthedAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = ctx.Tenants.First();

            var mrn = $"26MK{Guid.NewGuid():N}".Substring(0, 18).ToUpperInvariant();
            ctx.MRNRegistries.Add(new MRNRegistry
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                MRN = mrn,
                RegistrationDate = DateTime.UtcNow.AddDays(-200),
                TotalQuantity = 100m,
                UsedQuantity = 50m,
                DischargedQuantity = 10m,
                ExpiryDate = DateTime.UtcNow.AddDays(3), // expires in 3d → Critical
                IsActive = true,
            });

            var partner = ctx.Partners.First();
            ctx.Invoices.Add(new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Number = $"INV-OVER-{Guid.NewGuid():N}".Substring(0, 14),
                PartnerId = partner.Id,
                IssueDate = DateTime.UtcNow.AddDays(-60),
                DueDate = DateTime.UtcNow.AddDays(-30), // 30d overdue → Critical
                Currency = "EUR",
                Status = InvoiceStatus.Issued,
                SubTotal = 999m,
                TotalAmount = 999m,
            });
            await ctx.SaveChangesAsync();
        }

        var resp = await client.GetFromJsonAsync<Envelope<AlertsFeedRow>>("/api/Management/alerts");
        resp!.Data!.Rows.Should().NotBeEmpty();
        resp.Data.Rows.Should().Contain(r => r.Category == (int)AlertCategory.MrnExpiring);
        resp.Data.Rows.Should().Contain(r => r.Category == (int)AlertCategory.OverdueInvoice);
        resp.Data.Rows.First(r => r.Category == (int)AlertCategory.OverdueInvoice)
            .Severity.Should().BeGreaterThanOrEqualTo((int)AlertSeverity.Warning);
    }

    private enum AlertSeverity { Info = 1, Warning = 2, Critical = 3 }
    private enum AlertCategory { MrnExpiring = 1, OverdueInvoice = 2, MaterialShortage = 3, AtRiskProductionOrder = 4, LonAuthorizationExpiring = 5 }

    private sealed record LoginResponse(string AccessToken);
    private sealed record Envelope<T>(bool IsSuccess, T? Data, string? ErrorMessage, string? ErrorCode);

    private sealed record OnTimeCustomerRow(
        Guid? CustomerId, string CustomerName, int TotalShipments,
        int OnTime, int Late1To7, int LateOver7, int Unknown, double OnTimePercentage);
    private sealed record OnTimeShipmentItem(
        Guid ShipmentId, string ShipmentNumber, DateTime ShipmentDate,
        Guid? CustomerId, string? CustomerCode, string? CustomerName,
        DateTime? PlannedEndDate, int? DaysLate, int Bucket);
    private sealed record OnTimeReportRow(
        DateTime From, DateTime To,
        List<OnTimeShipmentItem> Shipments,
        List<OnTimeCustomerRow> ByCustomer,
        OnTimeCustomerRow Overall);

    private sealed record CustomerSummary(
        Guid CustomerId, string CustomerCode, string CustomerName,
        int OpenPOs, int CompletedPOs, decimal ProducedQuantity,
        int ShipmentCount, decimal ShippedQuantity,
        int InvoicesIssued, decimal InvoicedOutstanding, decimal InvoicedPaid,
        string Currency);
    private sealed record ByCustomerRow(DateTime From, DateTime To, List<CustomerSummary> Rows);

    private sealed record AlertItem(
        int Category, int Severity, string Title, string Detail,
        string? LinkPath, DateTime? RelatedDate, decimal? Amount, string? Currency);
    private sealed record AlertsFeedRow(DateTime GeneratedAt, List<AlertItem> Rows);
}
