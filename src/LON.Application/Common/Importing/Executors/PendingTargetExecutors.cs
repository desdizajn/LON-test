using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Customs;
using LON.Domain.Entities.Production;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Common.Importing.Executors;

/// <summary>
/// P6.35 — commits one <see cref="BOM"/> per distinct parent FG found in the
/// import rows plus one <see cref="BOMLine"/> per row. Grouping key is the
/// row's <c>parentItemCode</c> (Either-scope, so header defaults flow into
/// every row when the file carries a single BOM).
///
/// Versioning: if a BOM for the same (TenantId, ItemId) already exists
/// (undeleted), a new BOM with `Version = max(existing) + 1` is written and
/// the old one stays active until the caller flips it off. Lets Matriks-style
/// files land without clobbering hand-edited BOMs.
///
/// ScrapPct / position / baseQuantity / bomCode are optional; sensible
/// defaults (0, row order, 1, auto-generated) are applied when absent.
/// </summary>
public class BOMsImportExecutor : IImportTargetExecutor
{
    public string TargetName => "BOMs";

    public async Task<(bool Ok, int Created, string? Error)> ExecuteAsync(
        IReadOnlyList<ResolvedImportRow> rows,
        IReadOnlyDictionary<string, object?> headerDefaults,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return (true, 0, null);

        var created = 0;
        var grouped = rows.GroupBy(r => r.GetOrDefault<Guid>("parentItemCode")).ToList();

        foreach (var group in grouped)
        {
            var parentItemId = group.Key;
            if (parentItemId == Guid.Empty)
            {
                var firstRow = group.First();
                return (false, created, $"Row {firstRow.RowIndex}: parentItemCode is required (no header default supplied either).");
            }

            // Version-bump: highest existing version for (TenantId, ItemId) + 1.
            // IgnoreQueryFilters so soft-deleted BOMs still count towards the
            // numeric sequence (composite unique is filtered on IsDeleted=0 but
            // monotonic numbering across undeletes is friendlier).
            var existingMaxVersion = await context.BOMs
                .IgnoreQueryFilters()
                .Where(b => b.ItemId == parentItemId)
                .Select(b => (int?)b.Version)
                .MaxAsync(cancellationToken) ?? 0;

            var headRow = group.First();
            var bomCode = headRow.GetOrDefault<string>("bomCode");
            var baseQuantity = headRow.GetOrDefault<decimal?>("baseQuantity") ?? 1m;

            var bom = new BOM
            {
                Id = Guid.NewGuid(),
                Code = string.IsNullOrWhiteSpace(bomCode)
                    ? $"BOM-{parentItemId:N}".Substring(0, 28) + $"-V{existingMaxVersion + 1}"
                    : bomCode!,
                ItemId = parentItemId,
                Version = existingMaxVersion + 1,
                ValidFrom = DateTime.UtcNow.Date,
                ValidTo = null,
                IsActive = true,
                BaseQuantity = baseQuantity
            };
            await context.BOMs.AddAsync(bom, cancellationToken);
            created++;

            int lineNumber = 1;
            foreach (var row in group.OrderBy(r => r.GetOrDefault<int?>("position") ?? int.MaxValue).ThenBy(r => r.RowIndex))
            {
                var componentItemId = row.GetOrDefault<Guid>("componentItemCode");
                var componentUomId = row.GetOrDefault<Guid>("componentUomCode");
                var componentQty = row.GetOrDefault<decimal>("componentQuantity");

                if (componentItemId == Guid.Empty || componentUomId == Guid.Empty || componentQty <= 0m)
                    return (false, created,
                        $"Row {row.RowIndex}: componentItemCode, componentUomCode and positive componentQuantity are required.");

                var line = new BOMLine
                {
                    Id = Guid.NewGuid(),
                    BOMId = bom.Id,
                    LineNumber = row.GetOrDefault<int?>("position") ?? lineNumber,
                    ItemId = componentItemId,
                    Quantity = componentQty,
                    UoMId = componentUomId,
                    ScrapPercentage = row.GetOrDefault<decimal?>("scrapPct") ?? 0m
                };
                bom.Lines.Add(line);
                lineNumber++;
                created++;
            }
        }

        return (true, created, null);
    }
}

/// <summary>
/// P5.1.7 — Customs declarations import executor. Lands a DRAFT declaration
/// from a partner-supplied file (CSV / XLSX / JSON / XML). Not fully
/// business-validated — the declaration stays in Status=Draft, bypassing
/// MRN registration, guarantee-auto-debit, and the declaration rule
/// engine. The user reviews and promotes via the regular Declarations UI.
///
/// Header defaults populate declaration-level Box fields; row fields
/// populate each <see cref="CustomsDeclarationLine"/>.
/// </summary>
public class CustomsDeclarationsImportExecutor : IImportTargetExecutor
{
    public string TargetName => "CustomsDeclarations";

    public async Task<(bool Ok, int Created, string? Error)> ExecuteAsync(
        IReadOnlyList<ResolvedImportRow> rows,
        IReadOnlyDictionary<string, object?> headerDefaults,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return (true, 0, null);
        var head = rows[0];

        var declarationNumber = head.GetOrDefault<string>("declarationNumber");
        if (string.IsNullOrWhiteSpace(declarationNumber))
            return (false, 0, "declarationNumber is required (header).");

        // Resolve CustomsProcedure by Code (global reference data, not scoped).
        var procedureCode = head.GetOrDefault<string>("procedureCode");
        if (string.IsNullOrWhiteSpace(procedureCode))
            return (false, 0, "procedureCode is required (header).");
        var procedure = await context.CustomsProcedures
            .FirstOrDefaultAsync(p => p.Code == procedureCode, cancellationToken);
        if (procedure is null)
            return (false, 0, $"CustomsProcedure with code '{procedureCode}' was not found. Seed it first or pick a known code.");

        var declarationType = head.GetOrDefault<string>("declarationType") ?? "IM";
        var declarationDate = head.GetOrDefault<DateTime>("declarationDate");
        if (declarationDate == default)
            return (false, 0, "declarationDate is required (header).");

        // Separate MRN when supplied (so wizard can push both a local
        // DeclarationNumber and the partner's MRN); fall back to reusing the
        // declaration number for drafts.
        var mrn = head.GetOrDefault<string>("mrn");
        if (string.IsNullOrWhiteSpace(mrn)) mrn = declarationNumber;

        // G9 — pre-check BOTH uniqueness constraints before SaveChanges.
        // Dry-run was returning committable=true and then the DB unique index
        // on (TenantId, MRN) threw at commit. Check both so the executor can
        // report a readable error.
        if (await context.CustomsDeclarations.AnyAsync(d => d.DeclarationNumber == declarationNumber, cancellationToken))
            return (false, 0, $"Declaration '{declarationNumber}' already exists (DeclarationNumber).");
        if (await context.CustomsDeclarations.AnyAsync(d => d.MRN == mrn, cancellationToken))
            return (false, 0, $"Declaration with MRN '{mrn}' already exists.");

        var declaration = new CustomsDeclaration
        {
            Id = Guid.NewGuid(),
            DeclarationNumber = declarationNumber!,
            MRN = mrn!,
            DeclarationDate = declarationDate,
            DeclarationType = declarationType,
            CustomsProcedureId = procedure.Id,
            ProcedureCode = procedureCode!,
            PreviousProcedureCode = head.GetOrDefault<string>("previousProcedureCode"),
            PartnerId = head.GetOrDefault<Guid?>("partnerCode"),
            LONAuthorizationId = head.GetOrDefault<Guid?>("lonAuthorizationCode"),
            Currency = head.GetOrDefault<string>("currencyCode") ?? "EUR",
            ExchangeRate = head.GetOrDefault<decimal?>("exchangeRate"),
            Status = DeclarationStatus.Draft,
            IsCleared = false
        };
        await context.CustomsDeclarations.AddAsync(declaration, cancellationToken);

        int lineNo = 1;
        foreach (var row in rows)
        {
            var itemId = row.GetOrDefault<Guid>("itemCode");
            var uomId = row.GetOrDefault<Guid>("uomCode");
            var qty = row.GetOrDefault<decimal>("quantity");
            if (itemId == Guid.Empty || uomId == Guid.Empty || qty == 0m)
                return (false, lineNo - 1, $"Row {row.RowIndex}: itemCode, uomCode and quantity are required.");

            var line = new CustomsDeclarationLine
            {
                Id = Guid.NewGuid(),
                CustomsDeclarationId = declaration.Id,
                LineNumber = lineNo++,
                ItemId = itemId,
                UoMId = uomId,
                Quantity = qty,
                TariffCode = row.GetOrDefault<string>("tariffCode"),
                CountryOfOrigin = row.GetOrDefault<string>("originCountry"),
                IsPreferentialOrigin = row.Fields.TryGetValue("isPreferentialOrigin", out var pref) ? pref as bool? : null,
                NetWeight = row.GetOrDefault<decimal?>("netWeight"),
                GrossWeight = row.GetOrDefault<decimal?>("grossWeight"),
                ItemPrice = row.GetOrDefault<decimal>("invoiceValue"),
                VATRate = row.GetOrDefault<decimal>("vatRate")
            };
            declaration.Lines.Add(line);
        }

        return (true, 1 + declaration.Lines.Count, null);
    }
}

public class ImportTargetExecutorRegistry : IImportTargetExecutorRegistry
{
    private readonly IReadOnlyDictionary<string, IImportTargetExecutor> _byName;

    public ImportTargetExecutorRegistry(IEnumerable<IImportTargetExecutor> executors)
    {
        _byName = executors.ToDictionary(e => e.TargetName, StringComparer.OrdinalIgnoreCase);
    }

    public IImportTargetExecutor? Find(string targetName)
        => string.IsNullOrWhiteSpace(targetName) ? null
           : _byName.TryGetValue(targetName, out var e) ? e : null;
}
