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

namespace LON.Application.Customs.Commands.CreateExportDeclaration;

public record CreateExportDeclarationCommand : ICommand<Result<Guid>>
{
    public string DeclarationNumber { get; init; } = string.Empty;
    /// <summary>Optional. Leave empty to auto-generate a dev placeholder MRN.</summary>
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

    public List<ExportLineDto> Lines { get; init; } = new();
}

public record ExportLineDto
{
    public Guid ItemId { get; init; }
    public string? TariffCode { get; init; }
    /// <summary>Quantity of FG leaving the warehouse (customs-declared).</summary>
    public decimal Quantity { get; init; }
    public Guid UoMId { get; init; }
    public decimal CustomsValue { get; init; }
    public string? CountryOfOrigin { get; init; }
    public decimal? NetWeight { get; init; }
    public decimal? GrossWeight { get; init; }
    public string? CalculationMethod { get; init; }

    /// <summary>FG batch being exported. Required — the handler needs to locate the FG inventory row.</summary>
    public string BatchNumber { get; init; } = string.Empty;
    /// <summary>Optional FG pick location; if null, the handler searches any location with qty.</summary>
    public Guid? LocationId { get; init; }

    /// <summary>MRN being discharged by this line (the IM 4200 that originally imported the raw material).</summary>
    public string SourceMRN { get; init; } = string.Empty;
    /// <summary>
    /// Declared-unit quantity credited against the source MRN. Must satisfy
    /// <c>DischargedQuantity + value &lt;= UsedQuantity &lt;= TotalQuantity</c>.
    /// Independent of the FG <see cref="Quantity"/> because raw→FG conversion is BOM-specific.
    /// </summary>
    public decimal DischargeQuantity { get; init; }
}

public class CreateExportDeclarationCommandHandler
    : ICommandHandler<CreateExportDeclarationCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateExportDeclarationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(
        CreateExportDeclarationCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0)
            return Result<Guid>.Failure(ErrorCodes.ExportEmptyLines, "Export declaration must contain at least one line.");

        var procedure = await _context.CustomsProcedures
            .FirstOrDefaultAsync(p => p.Id == request.CustomsProcedureId, cancellationToken);
        if (procedure is null)
            return Result<Guid>.Failure(ErrorCodes.ProcedureNotFound, $"Customs procedure '{request.CustomsProcedureId}' does not exist.");
        if (!procedure.IsActive)
            return Result<Guid>.Failure(ErrorCodes.ProcedureInactive, $"Customs procedure '{procedure.Code}' is not active.");
        if (procedure.Type != CustomsProcedureType.Export)
            return Result<Guid>.Failure(
                ErrorCodes.ProcedureInactive,
                $"Procedure '{procedure.Code}' is not an export procedure (Type={procedure.Type}).");

        // Pre-resolve all source MRNs so we fail fast on bad input before any write.
        var distinctMrns = request.Lines
            .Select(l => (l.SourceMRN ?? string.Empty).Trim().ToUpperInvariant())
            .Where(m => !string.IsNullOrEmpty(m))
            .Distinct()
            .ToList();
        if (distinctMrns.Count != request.Lines.Select(l => l.SourceMRN).Distinct().Count()
            || distinctMrns.Count == 0)
        {
            return Result<Guid>.Failure(ErrorCodes.ExportMrnRequired, "Each export line must specify a non-empty SourceMRN.");
        }

        var registries = await _context.MRNRegistries
            .Where(r => distinctMrns.Contains(r.MRN) && !r.IsDeleted)
            .ToDictionaryAsync(r => r.MRN, cancellationToken);

        foreach (var mrn in distinctMrns)
        {
            if (!registries.TryGetValue(mrn, out var reg))
                return Result<Guid>.Failure(ErrorCodes.ExportMrnNotFound, $"SourceMRN '{mrn}' is not registered for this tenant.");

            var demand = request.Lines
                .Where(l => string.Equals(l.SourceMRN, mrn, StringComparison.OrdinalIgnoreCase))
                .Sum(l => l.DischargeQuantity);
            if (demand <= 0m)
                return Result<Guid>.Failure(ErrorCodes.ExportDischargeInvalid, $"SourceMRN '{mrn}': total DischargeQuantity must be positive.");

            var remainingUndischarged = reg.UsedQuantity - reg.DischargedQuantity;
            if (demand > remainingUndischarged)
                return Result<Guid>.Failure(
                    ErrorCodes.ExportOverDischarge,
                    $"SourceMRN '{mrn}': discharge {demand} exceeds outstanding undischarged qty " +
                    $"{remainingUndischarged} (Used={reg.UsedQuantity}, already discharged={reg.DischargedQuantity}).");
        }

        // MRN uniqueness for the EX declaration itself (global — customs issues MRNs globally).
        var exMrn = string.IsNullOrWhiteSpace(request.MRN)
            ? $"{request.DeclarationDate.Year % 100:D2}MK{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}E1"
            : request.MRN.Trim().ToUpperInvariant();
        var exMrnCollision = await _context.CustomsDeclarations
            .IgnoreQueryFilters()
            .AnyAsync(d => d.MRN == exMrn && !d.IsDeleted, cancellationToken);
        if (exMrnCollision)
            return Result<Guid>.Failure(ErrorCodes.MrnDuplicate, $"MRN '{exMrn}' is already registered.");

        // Build the EX CustomsDeclaration skeleton. Unlike IM, there's no
        // guarantee debit on the EX itself — guarantee activity is CREDITS tied
        // to the source MRNs (below).
        var declaration = new CustomsDeclaration
        {
            Id = Guid.NewGuid(),
            DeclarationNumber = request.DeclarationNumber,
            MRN = exMrn,
            DeclarationDate = request.DeclarationDate,
            CustomsProcedureId = procedure.Id,
            PartnerId = request.PartnerId,
            TotalCustomsValue = request.TotalCustomsValue,
            Currency = request.Currency,
            DeclarationType = "EX",
            ProcedureCode = procedure.Code,
            // Box 37 previous-procedure pair: for 3151 the previous is "51"
            // (inward processing suspension). Default retained when caller
            // omits; explicit override still respected.
            PreviousProcedureCode = procedure.Code.Length == 4 ? procedure.Code[..2] == "31" ? "51" : "00" : "00",
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
            // FG inventory decrement: find FG balance by Item + Batch (+ optional Location).
            // P6.21 — accept both OK and legacy None so exports can discharge
            // balances created before the unset-qualityStatus coercion landed.
            var fgQuery = _context.InventoryBalances
                .Where(b => b.ItemId == lineDto.ItemId
                            && b.BatchNumber == lineDto.BatchNumber
                            && b.UoMId == lineDto.UoMId
                            && b.Quantity > 0m
                            && (b.QualityStatus == QualityStatus.OK
                                || b.QualityStatus == QualityStatus.None));
            if (lineDto.LocationId.HasValue)
                fgQuery = fgQuery.Where(b => b.LocationId == lineDto.LocationId.Value);

            var fg = await fgQuery.OrderBy(b => b.CreatedAt).FirstOrDefaultAsync(cancellationToken);
            if (fg is null)
                return Result<Guid>.Failure(
                    $"Line {lineNumber}: no FG inventory for Item+Batch '{lineDto.BatchNumber}' " +
                    $"with enough OK-quality stock.");
            if (fg.Quantity < lineDto.Quantity)
                return Result<Guid>.Failure(
                    $"Line {lineNumber}: FG batch '{lineDto.BatchNumber}' has {fg.Quantity}, " +
                    $"demand {lineDto.Quantity}.");

            fg.SubtractQuantity(lineDto.Quantity);

            // Discharge raw-material state. We transition a portion of the source-
            // MRN's Imported/InProduction balances to Exported state. Prefer
            // InProduction first (conceptually it's the WIP that just got shipped
            // out as FG), then spill over to Imported if WIP runs short.
            var normMrn = lineDto.SourceMRN.Trim().ToUpperInvariant();
            var transitionResult = await TransitionToExportedAsync(
                normMrn, lineDto.DischargeQuantity, request.DeclarationDate, cancellationToken);
            if (!transitionResult.IsSuccess)
                return Result<Guid>.Failure($"Line {lineNumber}: {transitionResult.ErrorMessage}");

            // Customs declaration line — cost math reduced: EX has no duty/VAT
            // calculation (goods leaving the territory), so we store declared
            // values straight through.
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
                UsedQuantityFromPrevious = lineDto.DischargeQuantity
            });

            // FG side InventoryMovement (Type=Shipment because the FG goods are
            // leaving the warehouse — InventoryMovement's MovementType doesn't
            // have a dedicated "Export" slot; Shipment matches the ledger intent).
            _context.InventoryMovements.Add(new InventoryMovement
            {
                Id = Guid.NewGuid(),
                MovementNumber = $"MOV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}",
                MovementDate = request.DeclarationDate,
                Type = MovementType.Shipment,
                ItemId = lineDto.ItemId,
                BatchNumber = lineDto.BatchNumber,
                MRN = exMrn,
                FromLocationId = fg.LocationId,
                ToLocationId = null,
                Quantity = lineDto.Quantity,
                UoMId = lineDto.UoMId,
                ReferenceNumber = declaration.DeclarationNumber,
                ReferenceId = declaration.Id
            });

            // TraceLink: source IM CustomsDeclaration -> this EX CustomsDeclaration.
            // Enables `/reports/lon-balance` style reports to walk backward from an
            // EX row to the IM that it discharged.
            var imDeclId = registries[normMrn].CustomsDeclarationId;
            _context.TraceLinks.Add(new TraceLink
            {
                Id = Guid.NewGuid(),
                SourceType = "CustomsDeclaration",
                SourceId = imDeclId ?? Guid.Empty,
                SourceBatchNumber = lineDto.BatchNumber,
                SourceMRN = normMrn,
                TargetType = "CustomsDeclaration",
                TargetId = declaration.Id,
                TargetBatchNumber = lineDto.BatchNumber,
                TargetMRN = exMrn,
                ItemId = lineDto.ItemId,
                Quantity = lineDto.DischargeQuantity,
                LinkDate = request.DeclarationDate
            });

            // Credit the guarantee proportionally. Find the original Debit entry
            // for this MRN and credit `debit × (dischargeQty / TotalQty)`.
            var creditResult = await CreditGuaranteeAsync(
                registries[normMrn], lineDto.DischargeQuantity, declaration, cancellationToken);
            if (!creditResult.IsSuccess)
                return Result<Guid>.Failure($"Line {lineNumber}: {creditResult.ErrorMessage}");

            // Bump DischargedQuantity on the registry. Mark IsActive=false when
            // fully discharged (the MRN is closed from customs' perspective).
            var reg = registries[normMrn];
            reg.DischargedQuantity += lineDto.DischargeQuantity;
            if (reg.DischargedQuantity >= reg.TotalQuantity)
                reg.IsActive = false;
        }

        // MRN registry for the EX declaration itself (lets later EX amendments /
        // cancellations look it up consistently with IM registry rows).
        if (procedure.RequiresMRNTracking)
        {
            var totalDischarged = request.Lines.Sum(l => l.DischargeQuantity);
            _context.MRNRegistries.Add(new MRNRegistry
            {
                Id = Guid.NewGuid(),
                MRN = exMrn,
                CustomsDeclarationId = declaration.Id,
                RegistrationDate = request.DeclarationDate,
                TotalQuantity = totalDischarged,
                UsedQuantity = totalDischarged,
                DischargedQuantity = totalDischarged,
                ExpiryDate = null,
                IsActive = false // EX MRN is "closed" on creation — export is a terminal state
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

    private async Task<TransitionResult> TransitionToExportedAsync(
        string mrn,
        decimal dischargeQty,
        DateTime when,
        CancellationToken ct)
    {
        // Walk InProduction first (FEFO by CreatedAt), then Imported. On each
        // source, shrink its qty by min(available, remaining) and grow a sibling
        // Exported row keyed on the same Item/Location/Batch/MRN/UoM.
        decimal remaining = dischargeQty;
        var pool = await _context.InventoryBalances
            .Where(b => b.MRN == mrn
                        && b.Quantity > 0m
                        && (b.LonProcessState == LonProcessState.InProduction
                            || b.LonProcessState == LonProcessState.Imported))
            .OrderBy(b => b.LonProcessState == LonProcessState.InProduction ? 0 : 1)
            .ThenBy(b => b.CreatedAt)
            .ToListAsync(ct);

        foreach (var src in pool)
        {
            if (remaining <= 0m) break;
            var take = Math.Min(src.Quantity, remaining);
            src.SubtractQuantity(take);
            await UpsertExportedBalanceAsync(src, take, ct);
            remaining -= take;
        }

        if (remaining > 0m)
        {
            return TransitionResult.Fail(
                $"MRN '{mrn}': insufficient LON inventory to discharge {dischargeQty}. " +
                $"Short by {remaining}. Import/production flow likely inconsistent.");
        }
        return TransitionResult.Ok();
    }

    private async Task UpsertExportedBalanceAsync(
        InventoryBalance source,
        decimal qty,
        CancellationToken ct)
    {
        // Check the DbSet.Local cache first — when a single command transitions
        // both InProduction and Imported portions against the same MRN,
        // FirstOrDefaultAsync issues a DB read that won't see the Exported row
        // we just added (it's Added but not yet SaveChanges-ed). Without the
        // local probe we'd end up with two Exported rows for the same key.
        var tracked = _context.InventoryBalances.Local.FirstOrDefault(b =>
            b.ItemId == source.ItemId
            && b.LocationId == source.LocationId
            && b.BatchNumber == source.BatchNumber
            && b.MRN == source.MRN
            && b.UoMId == source.UoMId
            && b.QualityStatus == source.QualityStatus
            && b.LonProcessState == LonProcessState.Exported);
        if (tracked is not null)
        {
            tracked.AddQuantity(qty);
            return;
        }

        var target = await _context.InventoryBalances.FirstOrDefaultAsync(b =>
                b.ItemId == source.ItemId
                && b.LocationId == source.LocationId
                && b.BatchNumber == source.BatchNumber
                && b.MRN == source.MRN
                && b.UoMId == source.UoMId
                && b.QualityStatus == source.QualityStatus
                && b.LonProcessState == LonProcessState.Exported,
            ct);

        if (target is null)
        {
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
                LonProcessState = LonProcessState.Exported
            });
        }
        else
        {
            target.AddQuantity(qty);
        }
    }

    private sealed class CreditResult
    {
        public bool IsSuccess => ErrorMessage is null;
        public string? ErrorMessage { get; init; }
        public static CreditResult Ok() => new();
        public static CreditResult Fail(string msg) => new() { ErrorMessage = msg };
    }

    private async Task<CreditResult> CreditGuaranteeAsync(
        MRNRegistry reg,
        decimal dischargeQty,
        CustomsDeclaration exDeclaration,
        CancellationToken ct)
    {
        if (reg.CustomsDeclarationId is null)
            return CreditResult.Ok(); // non-guaranteed MRN — nothing to release

        var debit = await _context.GuaranteeLedgerEntries
            .Where(e => e.CustomsDeclarationId == reg.CustomsDeclarationId
                        && e.EntryType == GuaranteeEntryType.Debit
                        && !e.IsDeleted)
            .OrderBy(e => e.EntryDate)
            .FirstOrDefaultAsync(ct);
        if (debit is null)
            return CreditResult.Ok(); // no debit → no credit (procedure didn't require guarantee)

        // Pro-rata credit: creditAmount = debit.Amount * (dischargeQty / reg.TotalQuantity).
        // On full discharge we also zero out any rounding remainder so the ledger
        // settles exactly.
        decimal creditAmount;
        var willBeFullyDischarged = reg.DischargedQuantity + dischargeQty >= reg.TotalQuantity;
        if (willBeFullyDischarged)
        {
            // Settle to ledger zero for this MRN: take the full outstanding.
            var outstanding = await _context.GuaranteeLedgerEntries
                .Where(e => e.CustomsDeclarationId == reg.CustomsDeclarationId && !e.IsDeleted)
                .SumAsync(
                    e => e.EntryType == GuaranteeEntryType.Debit ? e.Amount : -e.Amount,
                    ct);
            creditAmount = outstanding;
        }
        else
        {
            creditAmount = Math.Round(
                debit.Amount * dischargeQty / reg.TotalQuantity,
                2, MidpointRounding.AwayFromZero);
        }

        if (creditAmount <= 0m) return CreditResult.Ok();

        var credit = new GuaranteeLedgerEntry
        {
            Id = Guid.NewGuid(),
            GuaranteeAccountId = debit.GuaranteeAccountId,
            EntryDate = DateTime.UtcNow,
            EntryType = GuaranteeEntryType.Credit,
            Amount = creditAmount,
            Currency = debit.Currency,
            Description =
                $"EX discharge {exDeclaration.DeclarationNumber} — " +
                $"MRN {reg.MRN} qty {dischargeQty}/{reg.TotalQuantity}",
            ReferenceType = nameof(CustomsDeclaration),
            ReferenceId = exDeclaration.Id,
            MRN = reg.MRN,
            CustomsDeclarationId = reg.CustomsDeclarationId,
            ActualReleaseDate = willBeFullyDischarged ? exDeclaration.DeclarationDate : null,
            IsReleased = willBeFullyDischarged
        };

        credit.AddDomainEvent(new GuaranteeCreditedEvent
        {
            GuaranteeAccountId = debit.GuaranteeAccountId,
            Amount = creditAmount,
            MRN = reg.MRN,
            CustomsDeclarationId = reg.CustomsDeclarationId
        });

        _context.GuaranteeLedgerEntries.Add(credit);
        return CreditResult.Ok();
    }
}
