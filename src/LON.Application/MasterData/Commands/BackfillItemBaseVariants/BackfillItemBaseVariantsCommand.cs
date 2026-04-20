using LON.Application.Common.Commands;
using LON.Application.Common.Importing.Executors;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.MasterData.Commands.BackfillItemBaseVariants;

/// <summary>
/// P6.30 — one-shot admin operation. Walks every tenant-scoped Item whose
/// BaseCode/ColorCode/SizeCode is still null (typical of rows migrated from
/// legacy ELON before the KW12 variant model existed) and applies the same
/// <see cref="ItemsImportExecutor.DecomposeCode"/> logic the import pipeline
/// uses. For codes that decompose to a distinct base, the base Item is
/// created in-place (or re-used when already there) and the variant is
/// linked via <see cref="Item.ParentItemId"/>.
///
/// Idempotent: rerunning is a no-op because the second pass finds the
/// populated fields already set. Tenant-scoped — only items visible through
/// the caller's JWT are touched.
/// </summary>
public sealed record BackfillItemBaseVariantsCommand(bool DryRun = false) : ICommand<Result<BackfillItemBaseVariantsResult>>;

public sealed record BackfillItemBaseVariantsResult(
    int ItemsScanned,
    int VariantsBackfilled,
    int BaseItemsCreated,
    int UntouchedBaseCodeAlreadyPresent,
    List<string> SampleChanges);

public sealed class BackfillItemBaseVariantsCommandHandler
    : ICommandHandler<BackfillItemBaseVariantsCommand, Result<BackfillItemBaseVariantsResult>>
{
    private readonly IApplicationDbContext _context;

    public BackfillItemBaseVariantsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BackfillItemBaseVariantsResult>> Handle(
        BackfillItemBaseVariantsCommand request, CancellationToken ct)
    {
        // Pull every row for the current tenant (global filter applies). Skip
        // items whose BaseCode is already populated — they've been through the
        // backfill (or the KW12 import already wrote them).
        var candidates = await _context.Items
            .Where(i => i.BaseCode == null && i.ColorCode == null && i.SizeCode == null)
            .ToListAsync(ct);

        int variantsBackfilled = 0;
        int baseCreated = 0;
        int untouched = 0;
        var sample = new List<string>();

        // In-memory cache of base Items we've either looked up or created
        // during this pass. Key = BaseCode.
        var baseCache = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in candidates)
        {
            var (baseCode, colorCode, sizeCode) = ItemsImportExecutor.DecomposeCode(item.Code, item.Type);

            // No variant information extractable — the item is already base-shaped.
            // Record BaseCode as its own code so future reports can group safely.
            if (string.IsNullOrWhiteSpace(baseCode)
                || (string.IsNullOrWhiteSpace(colorCode) && string.IsNullOrWhiteSpace(sizeCode)))
            {
                if (!request.DryRun)
                    item.BaseCode = baseCode ?? item.Code;
                untouched++;
                continue;
            }

            // Real variant. Resolve or create the base Item under the same tenant.
            Item? baseItem;
            if (!baseCache.TryGetValue(baseCode!, out baseItem))
            {
                baseItem = await _context.Items
                    .FirstOrDefaultAsync(i => i.Code == baseCode, ct);
                if (baseItem is null)
                {
                    if (!request.DryRun)
                    {
                        baseItem = new Item
                        {
                            Id = Guid.NewGuid(),
                            Code = baseCode!,
                            Name = item.Name, // legacy rows have no separate base name
                            Description = string.Empty,
                            Type = item.Type,
                            BaseUoMId = item.BaseUoMId,
                            BaseCode = baseCode,
                            ColorCode = null,
                            SizeCode = null,
                            ParentItemId = null
                        };
                        await _context.Items.AddAsync(baseItem, ct);
                    }
                    baseCreated++;
                }
                baseCache[baseCode!] = baseItem!;
            }

            if (!request.DryRun)
            {
                item.BaseCode = baseCode;
                item.ColorCode = colorCode;
                item.SizeCode = sizeCode;
                item.ParentItemId = baseItem?.Id;
            }

            if (sample.Count < 10)
            {
                sample.Add($"{item.Code} → base={baseCode} color={colorCode ?? "-"} size={sizeCode ?? "-"}");
            }
            variantsBackfilled++;
        }

        if (!request.DryRun)
        {
            await _context.SaveChangesAsync(ct);
        }

        return Result<BackfillItemBaseVariantsResult>.Success(
            new BackfillItemBaseVariantsResult(
                ItemsScanned: candidates.Count,
                VariantsBackfilled: variantsBackfilled,
                BaseItemsCreated: baseCreated,
                UntouchedBaseCodeAlreadyPresent: untouched,
                SampleChanges: sample));
    }
}
