using System.Text.RegularExpressions;
using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Production;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Common.Importing.Executors;

/// <summary>
/// P6.22 — commit target for KW12-style Matriks rows. Groups rows by
/// <c>workOrderNumber</c>; first row of each group defines the order header;
/// every row contributes one <see cref="ProductionOrderMaterial"/>.
///
/// Atomic: all orders + materials go into the DbContext, single SaveChanges
/// at the pipeline level. Dedup against already-imported WOs (tenant-scoped
/// global filter applies).
/// </summary>
public class ProductionOrdersImportExecutor : IImportTargetExecutor
{
    public string TargetName => "ProductionOrders";

    public async Task<(bool Ok, int Created, string? Error)> ExecuteAsync(
        IReadOnlyList<ResolvedImportRow> rows,
        IReadOnlyDictionary<string, object?> headerDefaults,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return (true, 0, null);

        var warehouseId = headerDefaults.TryGetValue("warehouseCode", out var wh) && wh is Guid wg ? wg : Guid.Empty;
        var productUomId = headerDefaults.TryGetValue("productUomCode", out var uom) && uom is Guid ug ? ug : Guid.Empty;
        if (warehouseId == Guid.Empty) return (false, 0, "warehouseCode (header) is required.");
        if (productUomId == Guid.Empty) return (false, 0, "productUomCode (header) is required.");

        var statusName = headerDefaults.TryGetValue("status", out var st) ? st as string : "Draft";
        ProductionOrderStatus initialStatus = statusName?.Equals("Released", StringComparison.OrdinalIgnoreCase) == true
            ? ProductionOrderStatus.Released
            : ProductionOrderStatus.Draft;

        // Group rows by WorkOrder number.
        var groups = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.GetOrDefault<string>("workOrderNumber")))
            .GroupBy(r => r.GetOrDefault<string>("workOrderNumber")!, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Dedup against existing POs in the same tenant — imports are not
        // allowed to re-create already-landed WOs. User clears them first.
        var incomingNumbers = groups.Select(g => g.Key).ToList();
        var existingNumbers = await context.ProductionOrders
            .Where(po => incomingNumbers.Contains(po.OrderNumber))
            .Select(po => po.OrderNumber)
            .ToListAsync(cancellationToken);
        if (existingNumbers.Count > 0)
            return (false, 0, $"Production orders already exist: {string.Join(", ", existingNumbers.Take(5))}{(existingNumbers.Count > 5 ? "…" : "")}.");

        // KW12 main PA + sub parsing: "PA2602067-0001" → main="PA2602067", sub="0001".
        // Classify groups by MainOrderNumber so we can create a parent PO once
        // per main PA + attach variant sub-orders as children.
        var parsedGroups = groups
            .Select(g => new { Group = g, Parts = SplitMainSub(g.Key) })
            .ToList();
        var mainCache = new Dictionary<string, ProductionOrder>(StringComparer.OrdinalIgnoreCase);

        int ordersCreated = 0;
        int linesCreated = 0;
        foreach (var pg in parsedGroups)
        {
            var rowList = pg.Group.ToList();
            var head = rowList[0];
            var mainNumber = pg.Parts.Main;
            var subNumber = pg.Parts.Sub;

            var productItemId = head.GetOrDefault<Guid>("productCode");
            if (productItemId == Guid.Empty)
                return (false, ordersCreated, $"Row {head.RowIndex}: productCode is required for PO {pg.Group.Key}.");
            var orderQty = head.GetOrDefault<decimal>("orderQuantity");
            if (orderQty <= 0)
                return (false, ordersCreated, $"Row {head.RowIndex}: orderQuantity must be positive for PO {pg.Group.Key}.");

            var plannedStart = head.Fields.TryGetValue("plannedStart", out var psv) && psv is DateTime psd ? psd : DateTime.UtcNow;
            var weekNumber = head.Fields.TryGetValue("weekNumber", out var wnv) && wnv is int wni ? (int?)wni : null;
            var customerPartnerId = head.Fields.TryGetValue("customerPartnerCode", out var cp) && cp is Guid cpg ? (Guid?)cpg : null;

            // If this is a SUB (has a parent PA), find or create the parent PO
            // row. The parent's ItemId = the BASE item of the variant FG (via
            // Item.ParentItemId); falls back to the variant's own ItemId when
            // we can't resolve a base (still functional — parent = placeholder).
            ProductionOrder? parent = null;
            if (subNumber is not null && !string.IsNullOrWhiteSpace(mainNumber))
            {
                if (!mainCache.TryGetValue(mainNumber, out parent))
                {
                    // Existing parent in DB? Use it.
                    parent = await context.ProductionOrders
                        .FirstOrDefaultAsync(po => po.OrderNumber == mainNumber, cancellationToken);
                    if (parent is null)
                    {
                        var baseFgItemId = await context.Items
                            .Where(i => i.Id == productItemId)
                            .Select(i => i.ParentItemId)
                            .FirstOrDefaultAsync(cancellationToken)
                            ?? productItemId;
                        parent = new ProductionOrder
                        {
                            Id = Guid.NewGuid(),
                            OrderNumber = mainNumber,
                            ItemId = baseFgItemId,
                            OrderQuantity = 0m,                   // filled once all children are processed
                            UoMId = productUomId,
                            Status = initialStatus,
                            PlannedStartDate = plannedStart,
                            PlannedEndDate = plannedStart.AddDays(7),
                            CustomerPartnerId = customerPartnerId,
                            CustomerOrderNumber = head.GetOrDefault<string>("customerOrderNumber"),
                            WeekNumber = weekNumber,
                            MainOrderNumber = mainNumber,
                            SubOrderNumber = null,
                            ParentOrderId = null
                        };
                        await context.ProductionOrders.AddAsync(parent, cancellationToken);
                        ordersCreated++;
                    }
                    mainCache[mainNumber] = parent!;
                }
            }

            var po = new ProductionOrder
            {
                Id = Guid.NewGuid(),
                OrderNumber = pg.Group.Key,
                ItemId = productItemId,
                OrderQuantity = orderQty,
                UoMId = productUomId,
                Status = initialStatus,
                PlannedStartDate = plannedStart,
                PlannedEndDate = plannedStart.AddDays(7),
                CustomerPartnerId = customerPartnerId,
                CustomerOrderNumber = head.GetOrDefault<string>("customerOrderNumber"),
                WeekNumber = weekNumber,
                MainOrderNumber = mainNumber ?? pg.Group.Key,
                SubOrderNumber = subNumber,
                ParentOrderId = parent?.Id
            };
            await context.ProductionOrders.AddAsync(po, cancellationToken);
            ordersCreated++;
            if (parent is not null)
                parent.OrderQuantity += orderQty;

            int lineNo = 1;
            foreach (var row in rowList)
            {
                var matItemId = row.GetOrDefault<Guid>("materialItemCode");
                var matUomId = row.GetOrDefault<Guid>("materialUomCode");
                var matQty = row.GetOrDefault<decimal>("materialQuantity");
                if (matItemId == Guid.Empty || matUomId == Guid.Empty || matQty <= 0)
                    return (false, ordersCreated,
                        $"Row {row.RowIndex}: materialItemCode, materialUomCode and positive materialQuantity are required.");

                po.Materials.Add(new ProductionOrderMaterial
                {
                    Id = Guid.NewGuid(),
                    ProductionOrderId = po.Id,
                    LineNumber = lineNo++,
                    ItemId = matItemId,
                    UoMId = matUomId,
                    RequiredQuantity = matQty,
                    IssuedQuantity = 0m,
                    ReservedQuantity = 0m,
                    PreAssignedMRN = row.GetOrDefault<string>("materialPreAssignedMRN"),
                    PreAssignedBatchNumber = row.GetOrDefault<string>("materialPreAssignedBatch"),
                    EfficiencyFactor = row.Fields.TryGetValue("efficiencyFactor", out var ev) && ev is decimal ed ? ed : null
                });
                linesCreated++;
            }
        }

        return (true, ordersCreated + linesCreated, null);
    }

    /// <summary>
    /// Split "PA2602067-0001" into ("PA2602067", "0001"). When the code has
    /// no dash suffix, returns (code, null) — the PO is treated as both its
    /// own main and has no variants.
    /// </summary>
    internal static (string Main, string? Sub) SplitMainSub(string orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber)) return (orderNumber, null);
        var m = Regex.Match(orderNumber.Trim(), @"^(?<main>.+?)-(?<sub>[0-9A-Za-z]+)$");
        if (!m.Success) return (orderNumber.Trim(), null);
        return (m.Groups["main"].Value, m.Groups["sub"].Value);
    }
}
