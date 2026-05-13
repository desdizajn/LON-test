using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Guarantee.Commands.CreateGuaranteeBalanceSnapshot;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.ClientOrders;

/// <summary>
/// Phase 17 §E9 — finalise the razdolzuvanje for a ClientOrder.
///
/// Sequence (single SaveChanges transaction across the two cmds):
///   1. Recompute the report (IM duty vs. credited duty via
///      <see cref="GetRazdolzuvanjeForClientOrderQuery"/>).
///   2. Trigger <see cref="CreateGuaranteeBalanceSnapshotCommand"/> for the
///      caller-supplied snapshot date so a GuaranteeBalanceSnapshot row
///      lands for every active guarantee account (legacy
///      <c>tblSostojbaNaGarancija</c> parity). The shared snapshot is keyed
///      by SnapshotDate; we tag the Notes with the ClientOrder context so
///      the audit trail points back at this trigger.
///   3. If the report is reconciled AND every IM line carries
///      RazdolzenaDaNe=true, flip ClientOrder.Status → Closed. Otherwise
///      leave status alone; the user fixes the variance and re-runs.
///
/// Idempotent: re-running on an already-Closed order is a no-op for the
/// status flip (snapshots still get re-created — same semantics as the
/// monthly snapshot job).
/// </summary>
public sealed record TakeRazdolzuvanjeSnapshotCommand : ICommand<Result<TakeRazdolzuvanjeSnapshotResult>>
{
    public Guid ClientOrderId { get; init; }
    /// <summary>Defaults to UtcNow when null.</summary>
    public DateTime? SnapshotDate { get; init; }
    public string? Notes { get; init; }
}

public sealed record TakeRazdolzuvanjeSnapshotResult(
    int SnapshotRowsCreated,
    bool ClosedClientOrder,
    bool IsReconciled,
    bool AllLinesFlagged,
    decimal Variance);

public sealed class TakeRazdolzuvanjeSnapshotCommandHandler
    : ICommandHandler<TakeRazdolzuvanjeSnapshotCommand, Result<TakeRazdolzuvanjeSnapshotResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public TakeRazdolzuvanjeSnapshotCommandHandler(
        IApplicationDbContext context, IMediator mediator, ICurrentUserService currentUser)
    {
        _context = context;
        _mediator = mediator;
        _currentUser = currentUser;
    }

    public async Task<Result<TakeRazdolzuvanjeSnapshotResult>> Handle(
        TakeRazdolzuvanjeSnapshotCommand request, CancellationToken ct)
    {
        var order = await _context.ClientOrders
            .FirstOrDefaultAsync(o => o.Id == request.ClientOrderId, ct);
        if (order is null)
            return Result<TakeRazdolzuvanjeSnapshotResult>.Failure(
                $"ClientOrder '{request.ClientOrderId}' not found.");
        if (order.IsDeleted)
            return Result<TakeRazdolzuvanjeSnapshotResult>.Failure(
                "Cannot snapshot a deleted ClientOrder.");
        if (order.Status == ClientOrderStatus.Cancelled)
            return Result<TakeRazdolzuvanjeSnapshotResult>.Failure(
                "Cannot snapshot a cancelled ClientOrder.");

        // Step 1 — fresh aggregation.
        var report = await _mediator.Send(
            new GetRazdolzuvanjeForClientOrderQuery(request.ClientOrderId), ct);

        // Step 2 — global snapshot. The shared monthly snapshot job uses the
        // same command; we annotate the Notes so the audit trail captures the
        // CO trigger context.
        var snapDate = request.SnapshotDate ?? DateTime.UtcNow;
        var snapNotes = (request.Notes is null ? string.Empty : request.Notes + " · ")
                        + $"Razdolzuvanje trigger — ClientOrder {order.OrderNumber}"
                        + (report.IsReconciled ? " (reconciled)" : $" (variance {report.Variance:F2})");
        var snapshotResult = await _mediator.Send(
            new CreateGuaranteeBalanceSnapshotCommand
            {
                SnapshotDate = snapDate,
                Notes = snapNotes,
            }, ct);
        if (!snapshotResult.IsSuccess)
            return Result<TakeRazdolzuvanjeSnapshotResult>.Failure(
                snapshotResult.ErrorMessage ?? "Snapshot creation failed.");

        // Step 3 — auto-close when ready.
        bool closed = false;
        if (report.IsReconciled && report.AllLinesFlagged)
        {
            if (order.Status != ClientOrderStatus.Closed)
            {
                order.Status = ClientOrderStatus.Closed;
                order.ModifiedAt = DateTime.UtcNow;
                order.ModifiedBy = _currentUser?.AuditName ?? "System";
                closed = true;
                await _context.SaveChangesAsync(ct);
            }
        }

        return Result<TakeRazdolzuvanjeSnapshotResult>.Success(new TakeRazdolzuvanjeSnapshotResult(
            SnapshotRowsCreated: snapshotResult.Data,
            ClosedClientOrder: closed,
            IsReconciled: report.IsReconciled,
            AllLinesFlagged: report.AllLinesFlagged,
            Variance: report.Variance));
    }
}
