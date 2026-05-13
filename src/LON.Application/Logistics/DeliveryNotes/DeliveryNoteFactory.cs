using LON.Application.Common.Interfaces;
using LON.Domain.Common;
using LON.Domain.Entities.Logistics;
using LON.Domain.Entities.Production;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Logistics.DeliveryNotes;

/// <summary>
/// Phase 17 §E7.6 (D5) — pure helper that materialises a DeliveryNote from
/// a parent business document. Called inline by command handlers (MaterialIssue,
/// Shipment, ProductionReceipt) so the DN lives in the same SaveChanges scope
/// as the parent commit — guarantees the legacy `Propratnica` paperwork is
/// never out of sync with the underlying movement.
///
/// Once §E11 domain-events ship, this can be replaced with an event handler
/// fired off `MaterialIssueCommittedEvent` etc. The factory stays useful as
/// the actual mutation point either way.
/// </summary>
public interface IDeliveryNoteFactory
{
    /// <summary>
    /// Build (but do NOT save) a <see cref="DeliveryNote"/> for a freshly-
    /// persisted <see cref="MaterialIssue"/> bundle. Caller owns the
    /// `SaveChangesAsync` call. Pulls a `DeliveryNote` sequence number from
    /// <see cref="INumberSequenceService"/>; if the sequence isn't yet
    /// provisioned for the tenant, falls back to a Guid-suffixed marker
    /// (`DN-YYYY-{Guid8}`) so the auto-gen never blocks the parent flow.
    /// </summary>
    Task<DeliveryNote> CreateProducerDispatchAsync(
        Guid tenantId,
        Guid producerPartnerId,
        IReadOnlyList<MaterialIssue> issues,
        DateTime dispatchDate,
        Guid fromLocationId,
        CancellationToken ct);
}

public sealed class DeliveryNoteFactory : IDeliveryNoteFactory
{
    private readonly IApplicationDbContext _context;
    private readonly INumberSequenceService _sequences;

    public DeliveryNoteFactory(IApplicationDbContext context, INumberSequenceService sequences)
    {
        _context = context;
        _sequences = sequences;
    }

    public async Task<DeliveryNote> CreateProducerDispatchAsync(
        Guid tenantId,
        Guid producerPartnerId,
        IReadOnlyList<MaterialIssue> issues,
        DateTime dispatchDate,
        Guid fromLocationId,
        CancellationToken ct)
    {
        if (issues.Count == 0)
            throw new InvalidOperationException("Cannot create a DeliveryNote with no source MaterialIssues.");

        var number = await ResolveNumberAsync(tenantId, dispatchDate.Year, ct);

        // Pre-load items + UoMs so we can render meaningful descriptions on
        // the cover sheet. We touch each one only once.
        var itemIds = issues.Select(i => i.ItemId).Distinct().ToList();
        var items = await _context.Items
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        var dn = new DeliveryNote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Number = number,
            DocumentType = DeliveryNoteType.ProducerDispatch,
            // 1:1 with the first MaterialIssue when the bulk-issue command
            // creates many — they share an IssueNumber, but the FK still points
            // at a row so the audit trail is unambiguous.
            RelatedDocumentId = issues[0].Id,
            DispatchDate = dispatchDate,
            FromLocationId = fromLocationId,
            ToPartnerId = producerPartnerId,
            Status = DeliveryNoteStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system:auto-gen",
        };

        foreach (var issue in issues)
        {
            items.TryGetValue(issue.ItemId, out var item);
            dn.Lines.Add(new DeliveryNoteLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DeliveryNoteId = dn.Id,
                ItemId = issue.ItemId,
                Description = item is null
                    ? "(item)"
                    : (string.IsNullOrEmpty(item.Name) ? item.Code : $"{item.Code} — {item.Name}"),
                Quantity = issue.Quantity,
                UoMId = issue.UoMId,
                BatchNumber = issue.BatchNumber,
                MRN = issue.MRN,
                Notes = issue.IssueNumber,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system:auto-gen",
            });
        }

        await _context.DeliveryNotes.AddAsync(dn, ct);
        foreach (var line in dn.Lines)
            await _context.DeliveryNoteLines.AddAsync(line, ct);

        return dn;
    }

    /// <summary>
    /// Pull `seq_DeliveryNote_{tenantId}` and format `DN-{year}-{seq:D6}`.
    /// Falls back to a Guid-suffixed identifier if the sequence isn't yet
    /// provisioned for the tenant (defensive — fresh tenants seeded post-
    /// migration won't have it).
    /// </summary>
    private async Task<string> ResolveNumberAsync(Guid tenantId, int year, CancellationToken ct)
    {
        try
        {
            var seq = await _sequences.NextAsync("DeliveryNote", tenantId, ct);
            return NumberFormatter.DeliveryNote(year, seq);
        }
        catch (Exception)
        {
            // Sequence missing → don't block the parent commit. Mark with `auto-`
            // prefix so reports can flag fallbacks for back-fill review.
            return $"DN-{year:D4}-auto-{Guid.NewGuid():N}".Substring(0, 22);
        }
    }
}
