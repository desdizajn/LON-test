using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Customs;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Common.Importing.Executors;

/// <summary>
/// Placeholder executor for a target whose commit pipeline isn't wired up
/// yet. Dry-run still exercises the resolver + validator against this
/// target's schema; only the final save fails with a clear "not implemented"
/// message so nothing silently gets created.
/// </summary>
public class BOMsImportExecutor : IImportTargetExecutor
{
    public string TargetName => "BOMs";
    public Task<(bool Ok, int Created, string? Error)> ExecuteAsync(
        IReadOnlyList<ResolvedImportRow> rows,
        IReadOnlyDictionary<string, object?> headerDefaults,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
        => Task.FromResult<(bool, int, string?)>(
            (false, 0, "BOMs import commit is not yet implemented; dry-run is supported."));
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
