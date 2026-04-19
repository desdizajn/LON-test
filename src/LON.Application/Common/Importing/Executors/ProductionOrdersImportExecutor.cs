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

        int ordersCreated = 0;
        int linesCreated = 0;
        foreach (var group in groups)
        {
            var rowList = group.ToList();
            var head = rowList[0];

            var productItemId = head.GetOrDefault<Guid>("productCode");
            if (productItemId == Guid.Empty)
                return (false, ordersCreated, $"Row {head.RowIndex}: productCode is required for PO {group.Key}.");
            var orderQty = head.GetOrDefault<decimal>("orderQuantity");
            if (orderQty <= 0)
                return (false, ordersCreated, $"Row {head.RowIndex}: orderQuantity must be positive for PO {group.Key}.");

            var plannedStart = head.Fields.TryGetValue("plannedStart", out var psv) && psv is DateTime psd ? psd : DateTime.UtcNow;
            var weekNumber = head.Fields.TryGetValue("weekNumber", out var wnv) && wnv is int wni ? (int?)wni : null;
            var customerPartnerId = head.Fields.TryGetValue("customerPartnerCode", out var cp) && cp is Guid cpg ? (Guid?)cpg : null;

            var po = new ProductionOrder
            {
                Id = Guid.NewGuid(),
                OrderNumber = group.Key,
                ItemId = productItemId,
                OrderQuantity = orderQty,
                UoMId = productUomId,
                Status = initialStatus,
                PlannedStartDate = plannedStart,
                // No PlannedEndDate in Matriks; default to +7 days so the
                // column is populated. User can tighten per order afterwards.
                PlannedEndDate = plannedStart.AddDays(7),
                CustomerPartnerId = customerPartnerId,
                CustomerOrderNumber = head.GetOrDefault<string>("customerOrderNumber"),
                WeekNumber = weekNumber
            };
            await context.ProductionOrders.AddAsync(po, cancellationToken);
            ordersCreated++;

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
}
