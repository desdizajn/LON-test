using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

public class AnalyticsController : BaseController
{
    private readonly ApplicationDbContext _context;

    public AnalyticsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var dashboard = new
        {
            Inventory = new
            {
                TotalItems = await _context.Items.CountAsync(),
                TotalLocations = await _context.Locations.CountAsync(),
                TotalBalance = await _context.InventoryBalances.SumAsync(i => (decimal?)i.Quantity) ?? 0m,
                BlockedQty = await _context.InventoryBalances
                    .Where(i => i.QualityStatus == LON.Domain.Enums.QualityStatus.Blocked)
                    .SumAsync(i => (decimal?)i.Quantity) ?? 0m
            },
            Production = new
            {
                ActiveOrders = await _context.ProductionOrders
                    .CountAsync(p => p.Status == LON.Domain.Enums.ProductionOrderStatus.InProgress ||
                                     p.Status == LON.Domain.Enums.ProductionOrderStatus.Released),
                CompletedToday = await _context.ProductionOrders
                    .CountAsync(p => p.Status == LON.Domain.Enums.ProductionOrderStatus.Completed &&
                                     p.ActualEndDate.HasValue &&
                                     p.ActualEndDate.Value.Date == DateTime.UtcNow.Date),
                WIP = await _context.ProductionOrders
                    .Where(p => p.Status == LON.Domain.Enums.ProductionOrderStatus.InProgress)
                    .SumAsync(p => (decimal?)(p.OrderQuantity - p.ProducedQuantity)) ?? 0m
            },
            Customs = new
            {
                PendingDeclarations = await _context.CustomsDeclarations
                    .CountAsync(d => !d.IsCleared),
                ActiveMRNs = await _context.MRNRegistries
                    .CountAsync(m => m.IsActive && (m.TotalQuantity - m.UsedQuantity) > 0),
                ExpiringMRNs = await _context.MRNRegistries
                    .CountAsync(m => m.IsActive &&
                                     m.ExpiryDate.HasValue &&
                                     m.ExpiryDate.Value <= DateTime.UtcNow.AddDays(30))
            },
            Guarantees = new
            {
                TotalAccounts = await _context.GuaranteeAccounts.CountAsync(a => a.IsActive),
                ActiveGuarantees = await _context.GuaranteeLedgerEntries
                    .CountAsync(l => l.EntryType == LON.Domain.Enums.GuaranteeEntryType.Debit && !l.IsReleased),
                TotalExposure = await _context.GuaranteeLedgerEntries
                    .Where(l => l.EntryType == LON.Domain.Enums.GuaranteeEntryType.Debit && !l.IsReleased)
                    .SumAsync(l => (decimal?)l.Amount) ?? 0m,
                ExpiringGuarantees = await _context.GuaranteeLedgerEntries
                    .CountAsync(l => l.EntryType == LON.Domain.Enums.GuaranteeEntryType.Debit &&
                                     !l.IsReleased &&
                                     l.ExpectedReleaseDate.HasValue &&
                                     l.ExpectedReleaseDate.Value <= DateTime.UtcNow.AddDays(30))
            }
        };

        return Ok(dashboard);
    }

    [HttpGet("production-kpi")]
    public async Task<IActionResult> GetProductionKPIs([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        fromDate ??= DateTime.UtcNow.AddDays(-30);
        toDate ??= DateTime.UtcNow;

        var orders = await _context.ProductionOrders
            .Where(p => p.CreatedAt >= fromDate && p.CreatedAt <= toDate)
            .ToListAsync();

        var kpis = new
        {
            TotalOrders = orders.Count,
            CompletedOrders = orders.Count(o => o.Status == LON.Domain.Enums.ProductionOrderStatus.Completed ||
                                                 o.Status == LON.Domain.Enums.ProductionOrderStatus.Closed),
            TotalProduced = orders.Sum(o => o.ProducedQuantity),
            TotalScrap = orders.Sum(o => o.ScrapQuantity),
            ScrapRate = orders.Sum(o => o.ProducedQuantity) > 0
                ? orders.Sum(o => o.ScrapQuantity) / orders.Sum(o => o.ProducedQuantity) * 100
                : 0,
            AvgLeadTime = orders
                .Where(o => o.ActualEndDate.HasValue && o.ActualStartDate.HasValue)
                .Average(o => (o.ActualEndDate!.Value - o.ActualStartDate!.Value).TotalDays)
        };

        return Ok(kpis);
    }

    [HttpGet("wms-kpi")]
    public async Task<IActionResult> GetWMSKPIs([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        fromDate ??= DateTime.UtcNow.AddDays(-30);
        toDate ??= DateTime.UtcNow;

        var receipts = await _context.Receipts
            .Where(r => r.ReceiptDate >= fromDate && r.ReceiptDate <= toDate)
            .Include(r => r.Lines)
            .ToListAsync();

        var shipments = await _context.Shipments
            .Where(s => s.ShipmentDate >= fromDate && s.ShipmentDate <= toDate)
            .Include(s => s.Lines)
            .ToListAsync();

        var movements = await _context.InventoryMovements
            .Where(m => m.MovementDate >= fromDate && m.MovementDate <= toDate)
            .CountAsync();

        var kpis = new
        {
            TotalReceipts = receipts.Count,
            TotalReceiptLines = receipts.Sum(r => r.Lines.Count),
            TotalShipments = shipments.Count,
            TotalShipmentLines = shipments.Sum(s => s.Lines.Count),
            TotalMovements = movements,
            AvgReceiptLinesPerReceipt = receipts.Count > 0 ? (double)receipts.Sum(r => r.Lines.Count) / receipts.Count : 0,
            BlockedItems = await _context.InventoryBalances
                .Where(i => i.QualityStatus == LON.Domain.Enums.QualityStatus.Blocked)
                .CountAsync()
        };

        return Ok(kpis);
    }

    [HttpGet("guarantee-exposure")]
    public async Task<IActionResult> GetGuaranteeExposure()
    {
        var accounts = await _context.GuaranteeAccounts
            .Where(a => a.IsActive)
            .ToListAsync();

        var exposure = new List<object>();

        foreach (var account in accounts)
        {
            var entries = await _context.GuaranteeLedgerEntries
                .Where(l => l.GuaranteeAccountId == account.Id && !l.IsDeleted)
                .ToListAsync();

            var balance = entries.Sum(e => e.EntryType == LON.Domain.Enums.GuaranteeEntryType.Debit ? e.Amount : -e.Amount);
            var activeDebits = entries.Where(e => e.EntryType == LON.Domain.Enums.GuaranteeEntryType.Debit && !e.IsReleased).ToList();

            exposure.Add(new
            {
                AccountNumber = account.AccountNumber,
                AccountName = account.AccountName,
                Currency = account.Currency,
                TotalLimit = account.TotalLimit,
                CurrentBalance = balance,
                AvailableLimit = account.TotalLimit - balance,
                Utilization = account.TotalLimit > 0 ? balance / account.TotalLimit * 100 : 0,
                ActiveCount = activeDebits.Count,
                ActiveAmount = activeDebits.Sum(e => e.Amount)
            });
        }

        return Ok(exposure);
    }

    [HttpGet("customs-summary")]
    public async Task<IActionResult> GetCustomsSummary([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        fromDate ??= DateTime.UtcNow.AddDays(-30);
        toDate ??= DateTime.UtcNow;

        var declarations = await _context.CustomsDeclarations
            .Where(d => d.DeclarationDate >= fromDate && d.DeclarationDate <= toDate)
            .Include(d => d.CustomsProcedure)
            .ToListAsync();

        var summary = new
        {
            TotalDeclarations = declarations.Count,
            ClearedDeclarations = declarations.Count(d => d.IsCleared),
            PendingDeclarations = declarations.Count(d => !d.IsCleared),
            TotalCustomsValue = declarations.Sum(d => d.TotalCustomsValue),
            TotalDuty = declarations.Sum(d => d.TotalDuty),
            TotalVAT = declarations.Sum(d => d.TotalVAT),
            ByProcedure = declarations
                .GroupBy(d => d.CustomsProcedure.Name)
                .Select(g => new
                {
                    Procedure = g.Key,
                    Count = g.Count(),
                    TotalValue = g.Sum(d => d.TotalCustomsValue)
                })
        };

        return Ok(summary);
    }

    [HttpGet("inventory-by-location")]
    public async Task<IActionResult> GetInventoryByLocation()
    {
        var inventory = await _context.InventoryBalances
            .Include(i => i.Location)
            .ThenInclude(l => l.Warehouse)
            .Include(i => i.Item)
            .GroupBy(i => new { 
                WarehouseName = i.Location.Warehouse.Name, 
                LocationName = i.Location.Name, 
                LocationType = i.Location.Type 
            })
            .Select(g => new
            {
                Warehouse = g.Key.WarehouseName,
                Location = g.Key.LocationName,
                LocationType = g.Key.LocationType.ToString(),
                ItemCount = g.Count(),
                TotalQuantity = g.Sum(i => i.Quantity)
            })
            .ToListAsync();

        return Ok(inventory);
    }

    [HttpGet("mrn-usage")]
    public async Task<IActionResult> GetMRNUsage()
    {
        var mrns = await _context.MRNRegistries
            .Where(m => m.IsActive)
            .Select(m => new
            {
                m.MRN,
                m.RegistrationDate,
                m.TotalQuantity,
                m.UsedQuantity,
                m.RemainingQuantity,
                UsagePercentage = m.TotalQuantity > 0 ? m.UsedQuantity / m.TotalQuantity * 100 : 0,
                m.ExpiryDate,
                DaysToExpiry = m.ExpiryDate.HasValue ? (m.ExpiryDate.Value - DateTime.UtcNow).Days : (int?)null
            })
            .ToListAsync();

        return Ok(mrns);
    }
}
