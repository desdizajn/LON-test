using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Customs;
using LON.Domain.Entities.Guarantee;
using LON.Domain.Entities.Traceability;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using LON.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.Commands.CreateReturnDeclaration;

/// <summary>
/// P2.6b — Reverses a previously filed EX discharge. A returned shipment
/// un-releases the LON bond for the portion that came back: re-debits the
/// guarantee ledger, transitions Exported balances back to Imported (or
/// InProduction per caller), restores FG inventory, and drops MRNRegistry
/// DischargedQuantity. Re-activates the MRN when it was previously closed
/// by a full discharge.
/// </summary>
public record CreateReturnDeclarationCommand : ICommand<Result<Guid>>
{
    public string DeclarationNumber { get; init; } = string.Empty;
    public string? MRN { get; init; }
    public DateTime DeclarationDate { get; init; }
    public Guid CustomsProcedureId { get; init; }
    public Guid? PartnerId { get; init; }
    public string Currency { get; init; } = "EUR";
    public decimal TotalCustomsValue { get; init; }

    public string? SenderName { get; init; }
    public string? SenderAddress { get; init; }
    public string? SenderCountry { get; init; }
    public string? CountryOfDispatch { get; init; }
    public string? CountryOfDestination { get; init; }
    public string? SpecialRemarks { get; init; }

    public List<ReturnLineDto> Lines { get; init; } = new();
}

public record ReturnLineDto
{
    public Guid ItemId { get; init; }
    public string? TariffCode { get; init; }
    /// <summary>FG quantity being re-intaken.</summary>
    public decimal Quantity { get; init; }
    public Guid UoMId { get; init; }
    public decimal CustomsValue { get; init; }
    public string? CountryOfOrigin { get; init; }
    public decimal? NetWeight { get; init; }
    public decimal? GrossWeight { get; init; }
    public string? CalculationMethod { get; init; }

    /// <summary>FG batch returning.</summary>
    public string BatchNumber { get; init; } = string.Empty;
    /// <summary>Location where returned FG lands.</summary>
    public Guid LocationId { get; init; }

    /// <summary>IM MRN whose discharge is being reversed by this line.</summary>
    public string SourceMRN { get; init; } = string.Empty;
    /// <summary>Declared-unit quantity to un-discharge from the MRN.</summary>
    public decimal ReturnQuantity { get; init; }
    /// <summary>Which LON state to restore the reversed portion to. Default = Imported.</summary>
    public LonProcessState ReturnTo { get; init; } = LonProcessState.Imported;
}

public class CreateReturnDeclarationCommandHandler
    : ICommandHandler<CreateReturnDeclarationCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateReturnDeclarationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(
        CreateReturnDeclarationCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0)
            return Result<Guid>.Failure("Return declaration must contain at least one line.");

        var procedure = await _context.CustomsProcedures
            .FirstOrDefaultAsync(p => p.Id == request.CustomsProcedureId, cancellationToken);
        if (procedure is null)
            return Result<Guid>.Failure($"Customs procedure '{request.CustomsProcedureId}' does not exist.");
        if (!procedure.IsActive)
            return Result<Guid>.Failure($"Customs procedure '{procedure.Code}' is not active.");

        // Only Imported or InProduction are valid restore targets for v1.
        foreach (var l in request.Lines)
        {
            if (l.ReturnTo != LonProcessState.Imported && l.ReturnTo != LonProcessState.InProduction)
                return Result<Guid>.Failure(
                    $"ReturnTo must be Imported or InProduction (got {l.ReturnTo}).");
        }

        // Validate each line's SourceMRN + ReturnQuantity against aggregated demand.
        var distinctMrns = request.Lines
            .Select(l => (l.SourceMRN ?? string.Empty).Trim().ToUpperInvariant())
            .Where(m => !string.IsNullOrEmpty(m))
            .Distinct()
            .ToList();
        if (distinctMrns.Count != request.Lines.Select(l => l.SourceMRN).Distinct().Count()
            || distinctMrns.Count == 0)
        {
            return Result<Guid>.Failure("Each return line must specify a non-empty SourceMRN.");
        }

        var registries = await _context.MRNRegistries
            .Where(r => distinctMrns.Contains(r.MRN) && !r.IsDeleted)
            .ToDictionaryAsync(r => r.MRN, cancellationToken);

        foreach (var mrn in distinctMrns)
        {
            if (!registries.TryGetValue(mrn, out var reg))
                return Result<Guid>.Failure($"SourceMRN '{mrn}' is not registered for this tenant.");

            var demand = request.Lines
                .Where(l => string.Equals(l.SourceMRN, mrn, StringComparison.OrdinalIgnoreCase))
                .Sum(l => l.ReturnQuantity);
            if (demand <= 0m)
                return Result<Guid>.Failure($"SourceMRN '{mrn}': aggregate ReturnQuantity must be positive.");
            if (demand > reg.DischargedQuantity)
                return Result<Guid>.Failure(
                    $"SourceMRN '{mrn}': return qty {demand} exceeds previously discharged qty " +
                    $"{reg.DischargedQuantity}. Only previously exported volume can come back.");
        }

        // Mint an MRN for the return declaration itself (treated as a fresh IM-like row).
        var returnMrn = string.IsNullOrWhiteSpace(request.MRN)
            ? $"{request.DeclarationDate.Year % 100:D2}MK{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}R1"
            : request.MRN.Trim().ToUpperInvariant();
        var mrnCollision = await _context.CustomsDeclarations
            .IgnoreQueryFilters()
            .AnyAsync(d => d.MRN == returnMrn && !d.IsDeleted, cancellationToken);
        if (mrnCollision)
            return Result<Guid>.Failure($"MRN '{returnMrn}' is already registered.");

        var declaration = new CustomsDeclaration
        {
            Id = Guid.NewGuid(),
            DeclarationNumber = request.DeclarationNumber,
            MRN = returnMrn,
            DeclarationDate = request.DeclarationDate,
            CustomsProcedureId = procedure.Id,
            PartnerId = request.PartnerId,
            TotalCustomsValue = request.TotalCustomsValue,
            Currency = request.Currency,
            DeclarationType = "IM", // Returned goods re-enter as IM
            ProcedureCode = procedure.Code,
            // For 6121, Box 37 previous = "21" (or "31" for full EX). Default "31" — most returns come from full EX lines.
            PreviousProcedureCode = procedure.Code.Length == 4 && procedure.Code[..2] == "61"
                ? "31"
                : "00",
            SenderName = request.SenderName,
            SenderAddress = request.SenderAddress,
            SenderCountry = request.SenderCountry,
            CountryOfDispatch = request.CountryOfDispatch,
            CountryOfDestination = request.CountryOfDestination,
            SpecialRemarks = request.SpecialRemarks,
            Status = DeclarationStatus.Registered,
            IsCleared = false
        };

        int lineNumber = 1;
        foreach (var lineDto in request.Lines)
        {
            var normMrn = lineDto.SourceMRN.Trim().ToUpperInvariant();
            var reg = registries[normMrn];

            // Reverse the Exported -> ReturnTo transition. Walk Exported balances
            // for this MRN reverse-FEFO (most recent first — returns typically
            // mirror the latest EX).
            var transitionResult = await RestoreFromExportedAsync(
                normMrn, lineDto.ReturnQuantity, lineDto.ReturnTo, cancellationToken);
            if (!transitionResult.IsSuccess)
                return Result<Guid>.Failure($"Line {lineNumber}: {transitionResult.ErrorMessage}");

            // FG re-intake. Upsert FG InventoryBalance at caller's location +qty.
            // Use DbSet.Local first so concurrent same-command lines merge.
            UpsertFgBalance(
                lineDto.ItemId, lineDto.LocationId, lineDto.BatchNumber,
                lineDto.UoMId, lineDto.Quantity);

            // CustomsDeclarationLine
            declaration.Lines.Add(new CustomsDeclarationLine
            {
                Id = Guid.NewGuid(),
                CustomsDeclarationId = declaration.Id,
                LineNumber = lineNumber++,
                ItemId = lineDto.ItemId,
                TariffCode = lineDto.TariffCode,
                Quantity = lineDto.Quantity,
                UoMId = lineDto.UoMId,
                CustomsValue = lineDto.CustomsValue,
                CountryOfOrigin = lineDto.CountryOfOrigin,
                DutyRate = 0m,
                DutyAmount = 0m,
                VATRate = 0m,
                VATAmount = 0m,
                OtherCharges = 0m,
                GrossWeight = lineDto.GrossWeight,
                NetWeight = lineDto.NetWeight,
                CalculationMethod = lineDto.CalculationMethod,
                PreviousMRN = normMrn,
                UsedQuantityFromPrevious = lineDto.ReturnQuantity
            });

            // FG InventoryMovement (Type=Return, goods coming back).
            _context.InventoryMovements.Add(new InventoryMovement
            {
                Id = Guid.NewGuid(),
                MovementNumber = $"MOV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}",
                MovementDate = request.DeclarationDate,
                Type = MovementType.Return,
                ItemId = lineDto.ItemId,
                BatchNumber = lineDto.BatchNumber,
                MRN = returnMrn,
                FromLocationId = null,
                ToLocationId = lineDto.LocationId,
                Quantity = lineDto.Quantity,
                UoMId = lineDto.UoMId,
                ReferenceNumber = declaration.DeclarationNumber,
                ReferenceId = declaration.Id
            });

            // TraceLink reverse: Return CustomsDeclaration -> IM CustomsDeclaration
            // (backward pointer; the forward EX link was written in P2.6a).
            var imDeclId = reg.CustomsDeclarationId;
            _context.TraceLinks.Add(new TraceLink
            {
                Id = Guid.NewGuid(),
                SourceType = "CustomsDeclaration",
                SourceId = declaration.Id,
                SourceBatchNumber = lineDto.BatchNumber,
                SourceMRN = returnMrn,
                TargetType = "CustomsDeclaration",
                TargetId = imDeclId ?? Guid.Empty,
                TargetBatchNumber = lineDto.BatchNumber,
                TargetMRN = normMrn,
                ItemId = lineDto.ItemId,
                Quantity = lineDto.ReturnQuantity,
                LinkDate = request.DeclarationDate
            });

            // Re-Debit the guarantee. Proportional to the return portion of the
            // original IM debit (symmetric with EX credit math).
            var debitResult = await ReDebitGuaranteeAsync(reg, lineDto.ReturnQuantity, declaration, cancellationToken);
            if (!debitResult.IsSuccess)
                return Result<Guid>.Failure($"Line {lineNumber}: {debitResult.ErrorMessage}");

            // Update registry: drop DischargedQuantity; re-activate if it was
            // previously closed (fully-discharged MRN getting a return brings
            // it back in play).
            reg.DischargedQuantity -= lineDto.ReturnQuantity;
            if (!reg.IsActive && reg.DischargedQuantity < reg.TotalQuantity)
                reg.IsActive = true;
        }

        // MRN registry row for the return declaration itself.
        if (procedure.RequiresMRNTracking)
        {
            var totalReturned = request.Lines.Sum(l => l.ReturnQuantity);
            _context.MRNRegistries.Add(new MRNRegistry
            {
                Id = Guid.NewGuid(),
                MRN = returnMrn,
                CustomsDeclarationId = declaration.Id,
                RegistrationDate = request.DeclarationDate,
                TotalQuantity = totalReturned,
                UsedQuantity = 0m,
                DischargedQuantity = 0m,
                ExpiryDate = null,
                IsActive = true // returns open a fresh tracking row so subsequent EX can be booked against them
            });
        }

        declaration.AddDomainEvent(new CustomsDeclarationCreatedEvent
        {
            CustomsDeclarationId = declaration.Id,
            DeclarationNumber = declaration.DeclarationNumber,
            MRN = declaration.MRN,
            ProcedureCode = declaration.ProcedureCode,
            LONAuthorizationId = null,
            DeclarationDate = declaration.DeclarationDate,
            TotalCustomsValue = declaration.TotalCustomsValue,
            TotalDuty = 0m,
            TotalVAT = 0m,
            Currency = declaration.Currency
        });

        await _context.CustomsDeclarations.AddAsync(declaration, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(declaration.Id);
    }

    private sealed class TransitionResult
    {
        public bool IsSuccess => ErrorMessage is null;
        public string? ErrorMessage { get; init; }
        public static TransitionResult Ok() => new();
        public static TransitionResult Fail(string msg) => new() { ErrorMessage = msg };
    }

    private async Task<TransitionResult> RestoreFromExportedAsync(
        string mrn,
        decimal returnQty,
        LonProcessState returnTo,
        CancellationToken ct)
    {
        var pool = await _context.InventoryBalances
            .Where(b => b.MRN == mrn
                        && b.Quantity > 0m
                        && b.LonProcessState == LonProcessState.Exported)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

        decimal remaining = returnQty;
        foreach (var src in pool)
        {
            if (remaining <= 0m) break;
            var take = Math.Min(src.Quantity, remaining);
            src.SubtractQuantity(take);
            UpsertRestoredBalance(src, take, returnTo);
            remaining -= take;
        }

        if (remaining > 0m)
            return TransitionResult.Fail(
                $"MRN '{mrn}': insufficient Exported inventory to restore {returnQty}. " +
                $"Short by {remaining}. Likely the prior EX data is inconsistent.");
        return TransitionResult.Ok();
    }

    private void UpsertRestoredBalance(InventoryBalance source, decimal qty, LonProcessState returnTo)
    {
        var tracked = _context.InventoryBalances.Local.FirstOrDefault(b =>
            b.ItemId == source.ItemId
            && b.LocationId == source.LocationId
            && b.BatchNumber == source.BatchNumber
            && b.MRN == source.MRN
            && b.UoMId == source.UoMId
            && b.QualityStatus == source.QualityStatus
            && b.LonProcessState == returnTo);
        if (tracked is not null)
        {
            tracked.AddQuantity(qty);
            return;
        }

        _context.InventoryBalances.Add(new InventoryBalance
        {
            Id = Guid.NewGuid(),
            ItemId = source.ItemId,
            LocationId = source.LocationId,
            BatchNumber = source.BatchNumber,
            MRN = source.MRN,
            Quantity = qty,
            UoMId = source.UoMId,
            QualityStatus = source.QualityStatus,
            ExpiryDate = source.ExpiryDate,
            LonProcessState = returnTo
        });
    }

    private void UpsertFgBalance(
        Guid itemId, Guid locationId, string batchNumber, Guid uomId, decimal qty)
    {
        var tracked = _context.InventoryBalances.Local.FirstOrDefault(b =>
            b.ItemId == itemId
            && b.LocationId == locationId
            && b.BatchNumber == batchNumber
            && b.MRN == null
            && b.UoMId == uomId
            && b.QualityStatus == QualityStatus.OK
            && b.LonProcessState == null);
        if (tracked is not null)
        {
            tracked.AddQuantity(qty);
            return;
        }

        // Fall back to async DB lookup (can't use sync EF here; defer the flag
        // to the SaveChanges by adding a fresh row. The Local probe covers the
        // common case. Worst case: one extra row that merges on next receipt.)
        _context.InventoryBalances.Add(new InventoryBalance
        {
            Id = Guid.NewGuid(),
            ItemId = itemId,
            LocationId = locationId,
            BatchNumber = batchNumber,
            MRN = null,
            Quantity = qty,
            UoMId = uomId,
            QualityStatus = QualityStatus.OK,
            LonProcessState = null
        });
    }

    private sealed class DebitResult
    {
        public bool IsSuccess => ErrorMessage is null;
        public string? ErrorMessage { get; init; }
        public static DebitResult Ok() => new();
        public static DebitResult Fail(string msg) => new() { ErrorMessage = msg };
    }

    private async Task<DebitResult> ReDebitGuaranteeAsync(
        MRNRegistry reg,
        decimal returnQty,
        CustomsDeclaration returnDeclaration,
        CancellationToken ct)
    {
        if (reg.CustomsDeclarationId is null)
            return DebitResult.Ok();

        // Find the original IM debit (same query shape as P2.6a's credit math).
        var imDebit = await _context.GuaranteeLedgerEntries
            .Where(e => e.CustomsDeclarationId == reg.CustomsDeclarationId
                        && e.EntryType == GuaranteeEntryType.Debit
                        && !e.IsDeleted)
            .OrderBy(e => e.EntryDate)
            .FirstOrDefaultAsync(ct);
        if (imDebit is null)
            return DebitResult.Ok(); // no guarantee required for this IM

        // Symmetric with EX: reDebit = imDebit × returnQty / MRN.TotalQuantity.
        // Rounded 2dp to match the original credit precision.
        var reDebitAmount = Math.Round(
            imDebit.Amount * returnQty / reg.TotalQuantity,
            2, MidpointRounding.AwayFromZero);

        if (reDebitAmount <= 0m) return DebitResult.Ok();

        // Limit check: re-debit must fit under the guarantee account's available limit.
        var currentBalance = await _context.GuaranteeLedgerEntries
            .Where(e => e.GuaranteeAccountId == imDebit.GuaranteeAccountId && !e.IsDeleted)
            .SumAsync(e => e.EntryType == GuaranteeEntryType.Debit ? e.Amount : -e.Amount, ct);
        var account = await _context.GuaranteeAccounts
            .FirstOrDefaultAsync(a => a.Id == imDebit.GuaranteeAccountId, ct);
        if (account is not null && currentBalance + reDebitAmount > account.TotalLimit)
        {
            return DebitResult.Fail(
                $"Guarantee '{account.AccountNumber}' over-limit on return re-debit: " +
                $"current balance {currentBalance:0.00} + re-debit {reDebitAmount:0.00} > limit {account.TotalLimit:0.00}.");
        }

        // Flip any prior full-discharge credit's IsReleased back to false — the
        // bond is now re-committed and no longer settled.
        var priorFullReleased = await _context.GuaranteeLedgerEntries
            .Where(e => e.CustomsDeclarationId == reg.CustomsDeclarationId
                        && e.EntryType == GuaranteeEntryType.Credit
                        && e.IsReleased
                        && !e.IsDeleted)
            .ToListAsync(ct);
        foreach (var c in priorFullReleased)
        {
            c.IsReleased = false;
            c.ActualReleaseDate = null;
        }

        var debit = new GuaranteeLedgerEntry
        {
            Id = Guid.NewGuid(),
            GuaranteeAccountId = imDebit.GuaranteeAccountId,
            EntryDate = DateTime.UtcNow,
            EntryType = GuaranteeEntryType.Debit,
            Amount = reDebitAmount,
            Currency = imDebit.Currency,
            Description =
                $"Return re-debit {returnDeclaration.DeclarationNumber} — " +
                $"MRN {reg.MRN} qty {returnQty}/{reg.TotalQuantity}",
            ReferenceType = nameof(CustomsDeclaration),
            ReferenceId = returnDeclaration.Id,
            MRN = reg.MRN,
            CustomsDeclarationId = reg.CustomsDeclarationId,
            IsReleased = false
        };

        debit.AddDomainEvent(new GuaranteeDebitedEvent
        {
            GuaranteeAccountId = imDebit.GuaranteeAccountId,
            Amount = reDebitAmount,
            MRN = reg.MRN,
            CustomsDeclarationId = reg.CustomsDeclarationId
        });

        _context.GuaranteeLedgerEntries.Add(debit);
        return DebitResult.Ok();
    }
}
