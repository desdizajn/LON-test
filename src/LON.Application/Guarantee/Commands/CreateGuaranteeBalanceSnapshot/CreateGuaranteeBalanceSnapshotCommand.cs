using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Guarantee;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Guarantee.Commands.CreateGuaranteeBalanceSnapshot;

/// <summary>
/// P15.5 — record the current ledger-derived balance of every active
/// <see cref="GuaranteeAccount"/> as a <see cref="GuaranteeBalanceSnapshot"/>
/// on the supplied date. If a snapshot already exists for an (account,
/// date) pair it is soft-deleted and replaced — idempotent re-runs keep
/// the latest-computed state.
///
/// <para>Intended triggers:</para>
/// <list type="bullet">
///   <item>Scheduled worker at midnight on the last day of each calendar
///         month (wired up via Quartz/Hangfire outside this command — see
///         P15.5.1 follow-up).</item>
///   <item>Admin-only on-demand endpoint <c>POST /api/Guarantee/snapshots/run</c>
///         for audit/ad-hoc reports.</item>
/// </list>
/// </summary>
public record CreateGuaranteeBalanceSnapshotCommand : ICommand<Result<int>>
{
    /// <summary>Date the snapshot is FOR (i.e., "balance as of ..."). UTC.</summary>
    public DateTime SnapshotDate { get; init; }

    /// <summary>Optional free-text note attached to every row created in this run.</summary>
    public string? Notes { get; init; }
}

public class CreateGuaranteeBalanceSnapshotCommandHandler
    : ICommandHandler<CreateGuaranteeBalanceSnapshotCommand, Result<int>>
{
    private readonly IApplicationDbContext _context;

    public CreateGuaranteeBalanceSnapshotCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<int>> Handle(CreateGuaranteeBalanceSnapshotCommand request, CancellationToken ct)
    {
        if (request.SnapshotDate == default)
            return Result<int>.Failure("SnapshotDate is required.");

        var cutoff = request.SnapshotDate.Date.AddDays(1); // inclusive — any entry ON the date counts.

        var accounts = await _context.GuaranteeAccounts
            .Where(a => a.IsActive && !a.IsDeleted)
            .ToListAsync(ct);

        int created = 0;
        foreach (var account in accounts)
        {
            var ledger = await _context.GuaranteeLedgerEntries
                .Where(e => e.GuaranteeAccountId == account.Id
                             && !e.IsDeleted
                             && e.EntryDate < cutoff)
                .Select(e => new { e.EntryType, e.Amount, e.IsReleased, e.ActualReleaseDate })
                .ToListAsync(ct);

            // Debits that were still outstanding on the snapshot date:
            // either never released, or released AFTER the date.
            var debit = ledger
                .Where(e => e.EntryType == GuaranteeEntryType.Debit
                             && (!e.IsReleased || (e.ActualReleaseDate.HasValue && e.ActualReleaseDate.Value >= cutoff)))
                .Sum(e => e.Amount);

            var credit = ledger
                .Where(e => e.EntryType == GuaranteeEntryType.Credit)
                .Sum(e => e.Amount);

            var net = debit - credit;
            var available = account.TotalLimit - net;
            var activeDebitCount = ledger.Count(e => e.EntryType == GuaranteeEntryType.Debit
                             && (!e.IsReleased || (e.ActualReleaseDate.HasValue && e.ActualReleaseDate.Value >= cutoff)));

            // Idempotent replace: soft-delete existing snapshot for (account, date) then insert fresh.
            var existing = await _context.GuaranteeBalanceSnapshots
                .Where(s => s.GuaranteeAccountId == account.Id
                             && s.SnapshotDate == request.SnapshotDate.Date
                             && !s.IsDeleted)
                .ToListAsync(ct);
            foreach (var e in existing) e.IsDeleted = true;

            await _context.GuaranteeBalanceSnapshots.AddAsync(new GuaranteeBalanceSnapshot
            {
                Id = Guid.NewGuid(),
                GuaranteeAccountId = account.Id,
                SnapshotDate = request.SnapshotDate.Date,
                Currency = account.Currency,
                TotalLimit = account.TotalLimit,
                DebitedAmount = debit,
                CreditedAmount = credit,
                NetBalance = net,
                AvailableLimit = available,
                ActiveDebitCount = activeDebitCount,
                Notes = request.Notes
            }, ct);
            created++;
        }

        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(created);
    }
}
