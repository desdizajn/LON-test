using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Customs.Validation;
using LON.Domain.Entities.Customs;
using LON.Domain.Entities.Guarantee;
using LON.Domain.Enums;
using LON.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LON.Application.Customs.Commands.CreateCustomsDeclaration;

public record CreateCustomsDeclarationCommand : ICommand<Result<Guid>>
{
    public string DeclarationNumber { get; init; } = string.Empty;

    /// <summary>
    /// Movement Reference Number. Optional: if empty, the handler generates a
    /// dev-mode placeholder (format `<YY>MK<8-hex>A1`). In production this
    /// field should carry the MRN returned by the customs portal.
    /// </summary>
    public string? MRN { get; init; }

    public DateTime DeclarationDate { get; init; }
    public Guid CustomsProcedureId { get; init; }
    public Guid? PartnerId { get; init; }

    /// <summary>
    /// LON authorization (Одобрение) under which this declaration is filed.
    /// Required for procedure codes 4200, 5100 and other LON-suspension types.
    /// </summary>
    public Guid? LONAuthorizationId { get; init; }

    public decimal TotalCustomsValue { get; init; }
    public string Currency { get; init; } = "EUR";
    public DateTime? DueDate { get; init; }

    /// <summary>
    /// Box 37 previous-procedure code. Defaults to "00" for fresh imports
    /// (no previous procedure). For re-export from LON, pass "51" (51 00
    /// → 31 51 sequence). XML generation (Phase 4.2) will split Box 37
    /// into the current/previous pair.
    /// </summary>
    public string? PreviousProcedureCode { get; init; }

    // ---- I2 landing costs (legacy DodadiTrosociPoFakturaU5) ----
    /// <summary>Trosoci — total landing/shipping/handling costs for this invoice.</summary>
    public decimal? LandingCosts { get; init; }
    /// <summary>Rabat — total discount given by supplier.</summary>
    public decimal? Discount { get; init; }

    // ---- SAD boxes propagated to the entity so rule engine sees them ----
    /// <summary>Box 02 — Sender/Exporter name.</summary>
    public string? SenderName { get; init; }
    /// <summary>Box 02 — Sender address.</summary>
    public string? SenderAddress { get; init; }
    /// <summary>Box 02 — Sender country (ISO 3166-1 alpha-2).</summary>
    public string? SenderCountry { get; init; }
    /// <summary>Box 15 — Country of dispatch (ISO 3166-1 alpha-2).</summary>
    public string? CountryOfDispatch { get; init; }
    /// <summary>Box 17 — Country of destination (ISO 3166-1 alpha-2).</summary>
    public string? CountryOfDestination { get; init; }
    /// <summary>Box 44 — Special remarks / attached documents.</summary>
    public string? SpecialRemarks { get; init; }

    /// <summary>Optional pre-set for testing; defaults to Draft or Registered
    /// depending on whether an MRN ends up being present after handling.</summary>
    public DeclarationStatus? Status { get; init; }

    public List<DeclarationLineDto> Lines { get; init; } = new();
}

public record DeclarationLineDto
{
    public Guid ItemId { get; init; }
    public string? TariffCode { get; init; }
    public decimal Quantity { get; init; }
    public Guid UoMId { get; init; }
    public decimal CustomsValue { get; init; }
    public string? CountryOfOrigin { get; init; }
    public decimal DutyRate { get; init; }
    public decimal VATRate { get; init; }
    // ---- I5: SAD box required fields ----
    /// <summary>Box 30 — Location of goods (warehouse identifier / address).</summary>
    public string? LocationOfGoods { get; init; }
    /// <summary>Box 35 — Gross weight in kilograms (packaging + product).</summary>
    public decimal? GrossWeight { get; init; }
    /// <summary>Box 38 — Net weight in kilograms (product only).</summary>
    public decimal? NetWeight { get; init; }
    /// <summary>Box 41 — Additional unit of measure quantity (when TARIC demands it).</summary>
    public decimal? AdditionalUnit { get; init; }
    /// <summary>Box 47 — Calculation method (A = ad valorem, S = specific, ...).</summary>
    public string? CalculationMethod { get; init; }
}

public class CreateCustomsDeclarationCommandHandler : ICommandHandler<CreateCustomsDeclarationCommand, Result<Guid>>
{
    /// <summary>Procedure codes that mandate a LON authorization.</summary>
    private static readonly HashSet<string> LonProcedureCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "4200", // release for free circulation + entry for inward processing (suspension)
        "5100", // inward processing (suspension) — separate declaration
    };

    private readonly IApplicationDbContext _context;
    private readonly IDeclarationRuleEngine _ruleEngine;
    private readonly ILogger<CreateCustomsDeclarationCommandHandler> _logger;

    public CreateCustomsDeclarationCommandHandler(
        IApplicationDbContext context,
        IDeclarationRuleEngine ruleEngine,
        ILogger<CreateCustomsDeclarationCommandHandler> logger)
    {
        _context = context;
        _ruleEngine = ruleEngine;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateCustomsDeclarationCommand request, CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0)
            return Result<Guid>.Failure("Declaration must contain at least one line.");

        var procedure = await _context.CustomsProcedures
            .FirstOrDefaultAsync(p => p.Id == request.CustomsProcedureId, cancellationToken);
        if (procedure is null)
            return Result<Guid>.Failure($"Customs procedure '{request.CustomsProcedureId}' does not exist.");
        if (!procedure.IsActive)
            return Result<Guid>.Failure($"Customs procedure '{procedure.Code}' is not active.");

        // Enforce LON authorization requirement by Box 37 procedure code.
        LONAuthorization? auth = null;
        if (LonProcedureCodes.Contains(procedure.Code))
        {
            if (request.LONAuthorizationId is null || request.LONAuthorizationId == Guid.Empty)
                return Result<Guid>.Failure(
                    $"LONAuthorizationId is required for procedure '{procedure.Code}'. " +
                    "File a LON authorization before submitting an IM 4200 declaration.");

            auth = await _context.LONAuthorizations
                .FirstOrDefaultAsync(a => a.Id == request.LONAuthorizationId.Value, cancellationToken);
            if (auth is null)
                return Result<Guid>.Failure($"LONAuthorization '{request.LONAuthorizationId.Value}' does not exist or is not accessible under the current tenant.");
            if (!string.Equals(auth.Status, "Active", StringComparison.OrdinalIgnoreCase))
                return Result<Guid>.Failure($"LONAuthorization '{auth.AuthorizationNumber}' is not active (status={auth.Status}).");
            if (auth.ExpiryDate.HasValue && auth.ExpiryDate.Value.Date < request.DeclarationDate.Date)
                return Result<Guid>.Failure($"LONAuthorization '{auth.AuthorizationNumber}' expired on {auth.ExpiryDate:yyyy-MM-dd}; declaration date is {request.DeclarationDate:yyyy-MM-dd}.");
            if (auth.IssueDate.Date > request.DeclarationDate.Date)
                return Result<Guid>.Failure($"LONAuthorization '{auth.AuthorizationNumber}' is not yet issued (IssueDate={auth.IssueDate:yyyy-MM-dd}).");
        }

        // MRN: auto-generate a dev-mode placeholder if empty. Real customs MRN
        // is 18 chars (`YY` + `CC` + 13-char serial + check digit). Our
        // placeholder is shorter (YYMK<8-hex>A1, 14 chars) and is explicitly
        // labeled as dev-mode — in production the user pastes the real MRN
        // returned by the customs portal.
        var mrn = string.IsNullOrWhiteSpace(request.MRN)
            ? GeneratePlaceholderMRN(request.DeclarationDate)
            : request.MRN.Trim().ToUpperInvariant();

        // B1: MRN uniqueness must be GLOBAL, not tenant-scoped. Customs issues
        // MRNs globally (no two tenants can share the same MRN); bypass the
        // EF query filter so the check sees rows across all tenants.
        var mrnCollision = await _context.CustomsDeclarations
            .IgnoreQueryFilters()
            .AnyAsync(d => d.MRN == mrn && !d.IsDeleted, cancellationToken);
        if (mrnCollision)
            return Result<Guid>.Failure($"MRN '{mrn}' is already registered.");

        var mrnRegistryCollision = await _context.MRNRegistries
            .IgnoreQueryFilters()
            .AnyAsync(r => r.MRN == mrn && !r.IsDeleted, cancellationToken);
        if (mrnRegistryCollision)
            return Result<Guid>.Failure($"MRN '{mrn}' is already present in the MRN registry.");

        var declaration = new CustomsDeclaration
        {
            Id = Guid.NewGuid(),
            DeclarationNumber = request.DeclarationNumber,
            MRN = mrn,
            DeclarationDate = request.DeclarationDate,
            CustomsProcedureId = request.CustomsProcedureId,
            PartnerId = request.PartnerId,
            LONAuthorizationId = auth?.Id,
            TotalCustomsValue = request.TotalCustomsValue,
            Currency = request.Currency,
            DueDate = request.DueDate,
            // B6: derive SAD Box 01 from procedure kind. Export procedures
            // (type 5) file an "EX" declaration; everything else is "IM".
            DeclarationType = procedure.Type == CustomsProcedureType.Export ? "EX" : "IM",
            ProcedureCode = procedure.Code,
            // I4: populate Box 37 previous-procedure pair. "00" = no previous
            // (fresh IM/EX); callers set explicitly for re-export (e.g., "51"
            // for 31 51 flow). XML emitter splits Box 37 at submission time.
            PreviousProcedureCode = string.IsNullOrWhiteSpace(request.PreviousProcedureCode)
                ? "00"
                : request.PreviousProcedureCode.Trim(),
            SenderName = request.SenderName,
            SenderAddress = request.SenderAddress,
            SenderCountry = request.SenderCountry,
            CountryOfDispatch = request.CountryOfDispatch,
            CountryOfDestination = request.CountryOfDestination,
            SpecialRemarks = request.SpecialRemarks,
            Status = request.Status ?? DeclarationStatus.Registered, // auto-MRN → Registered
            IsCleared = false
        };

        // I2: landing-cost pro-rata. Net landing = LandingCosts - Discount.
        // Pro-rated across lines by invoice-value weighting (legacy
        // DodadiTrosociPoFakturaU5; ELON_Research/04 §1). The adjustment
        // flows into the customs-value base for duty/VAT, so importing with
        // trosoci/rabat stays bit-compatible with ELON.
        var netLanding = (request.LandingCosts ?? 0m) - (request.Discount ?? 0m);
        var preAdjustInvoiceTotal = request.Lines.Sum(l => l.CustomsValue);

        // Line-level duty/VAT: per-line Carina = CustomsValue * DutyRate / 100;
        // Danok (VAT) base = CustomsValue + Carina. Matches legacy
        // PresmetajDavackiPoNaim.
        decimal totalDuty = 0;
        decimal totalVAT = 0;
        int lineNumber = 1;
        decimal totalQuantity = 0;

        foreach (var lineDto in request.Lines)
        {
            // Pro-rata landing-cost adjustment per-line (legacy
            // `Vrednost += Round(trosok * Vrednost/VrednostVK, 2)`).
            var landingAdjustment = (netLanding != 0m && preAdjustInvoiceTotal > 0m)
                ? Math.Round(netLanding * lineDto.CustomsValue / preAdjustInvoiceTotal,
                             2, MidpointRounding.AwayFromZero)
                : 0m;
            var adjustedCustomsValue = lineDto.CustomsValue + landingAdjustment;

            var dutyAmount = Math.Round(adjustedCustomsValue * lineDto.DutyRate / 100m, 2, MidpointRounding.AwayFromZero);
            var vatAmount = Math.Round((adjustedCustomsValue + dutyAmount) * lineDto.VATRate / 100m, 2, MidpointRounding.AwayFromZero);

            declaration.Lines.Add(new CustomsDeclarationLine
            {
                Id = Guid.NewGuid(),
                CustomsDeclarationId = declaration.Id,
                LineNumber = lineNumber++,
                ItemId = lineDto.ItemId,
                TariffCode = lineDto.TariffCode,
                Quantity = lineDto.Quantity,
                UoMId = lineDto.UoMId,
                CustomsValue = adjustedCustomsValue,
                CountryOfOrigin = lineDto.CountryOfOrigin,
                DutyRate = lineDto.DutyRate,
                DutyAmount = dutyAmount,
                VATRate = lineDto.VATRate,
                VATAmount = vatAmount,
                OtherCharges = 0,
                // I5: SAD per-line box fields. Null values remain null on the
                // entity; RequiredFieldsRule flags the critical ones (Box 38).
                GrossWeight = lineDto.GrossWeight,
                NetWeight = lineDto.NetWeight,
                CalculationMethod = lineDto.CalculationMethod
            });

            totalDuty += dutyAmount;
            totalVAT += vatAmount;
            totalQuantity += lineDto.Quantity;
        }

        declaration.TotalDuty = totalDuty;
        declaration.TotalVAT = totalVAT;
        declaration.TotalOtherCharges = 0;

        var validationResult = await _ruleEngine.ValidateAsync(declaration, cancellationToken);
        if (!validationResult.IsValid)
            return Result<Guid>.Failure(string.Join("\n", validationResult.GetErrorMessages()));

        declaration.AddDomainEvent(new CustomsDeclarationCreatedEvent
        {
            CustomsDeclarationId = declaration.Id,
            DeclarationNumber = declaration.DeclarationNumber,
            MRN = declaration.MRN,
            ProcedureCode = declaration.ProcedureCode,
            LONAuthorizationId = declaration.LONAuthorizationId,
            DeclarationDate = declaration.DeclarationDate,
            TotalCustomsValue = declaration.TotalCustomsValue,
            TotalDuty = declaration.TotalDuty,
            TotalVAT = declaration.TotalVAT,
            Currency = declaration.Currency
        });

        await _context.CustomsDeclarations.AddAsync(declaration, cancellationToken);

        // Register MRN so later phases (P2.3 receipt consumption, P2.6 export
        // credit) can track usage against the declared quantity.
        if (procedure.RequiresMRNTracking)
        {
            // B4: prefer the authorization's CompletionPeriodDays over the
            // procedure default. Правилник: completion deadline is set per
            // Одобрение, not per procedure.
            var completionDays = auth?.CompletionPeriodDays > 0
                ? auth.CompletionPeriodDays
                : procedure.DueDays;

            _context.MRNRegistries.Add(new MRNRegistry
            {
                Id = Guid.NewGuid(),
                MRN = mrn,
                CustomsDeclarationId = declaration.Id,
                RegistrationDate = request.DeclarationDate,
                TotalQuantity = totalQuantity,
                UsedQuantity = 0m,
                ExpiryDate = completionDays.HasValue
                    ? request.DeclarationDate.AddDays(completionDays.Value)
                    : null,
                IsActive = true
            });
        }

        // P2.2 + B3 + B5 — auto-debit the guarantee bond for procedures that
        // require one. Computed atomically with the declaration so no
        // "orphan" declarations exist without a bond reservation.
        // Compliance posture is deliberately stricter than legacy ELON
        // (Одобренија.ГаранцијаИзнос was advisory):
        //   - B3: the sum of OUTSTANDING debits tied to this authorization
        //         cannot exceed auth.GuaranteeAmount.
        //   - B5: the debit % is taken from auth.GuaranteePercentageOverride
        //         first, procedure.GuaranteePercentage as fallback.
        //   - Existing: account-level TotalLimit is still enforced.
        var effectiveGuaranteePct = auth?.GuaranteePercentageOverride
                                    ?? procedure.GuaranteePercentage;
        if (procedure.RequiresGuarantee && effectiveGuaranteePct > 0)
        {
            var debitResult = await TryDebitGuaranteeAsync(declaration, procedure, auth, effectiveGuaranteePct, cancellationToken);
            if (!debitResult.IsSuccess)
                return debitResult;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(declaration.Id);
    }

    /// <summary>
    /// Finds an active <see cref="GuaranteeAccount"/> in the declaration's
    /// currency (tenant auto-scoped by EF query filter), calculates the
    /// debit amount as <c>(TotalDuty + TotalVAT) × procedure.GuaranteePercentage / 100</c>,
    /// and queues a <see cref="GuaranteeLedgerEntry"/> Debit entry.
    /// Returns failure if no matching account or if the debit would breach
    /// the account's TotalLimit (current balance = Σ Debit − Σ Credit).
    /// </summary>
    private async Task<Result<Guid>> TryDebitGuaranteeAsync(
        CustomsDeclaration declaration,
        CustomsProcedure procedure,
        LONAuthorization? auth,
        decimal effectiveGuaranteePct,
        CancellationToken cancellationToken)
    {
        var account = await _context.GuaranteeAccounts
            .Where(a => a.Currency == declaration.Currency && a.IsActive && !a.IsDeleted)
            .OrderBy(a => a.AccountNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            return Result<Guid>.Failure(
                $"No active GuaranteeAccount in currency '{declaration.Currency}'. " +
                $"Open a guarantee bond in {declaration.Currency} before filing procedure {procedure.Code}.");
        }

        var fullLiability = declaration.TotalDuty + declaration.TotalVAT;
        var debitAmount = Math.Round(
            fullLiability * effectiveGuaranteePct / 100m,
            2, MidpointRounding.AwayFromZero);

        if (debitAmount <= 0m)
        {
            _logger.LogInformation(
                "Guarantee debit skipped for declaration {DeclarationNumber}: amount is zero (Duty={Duty}, VAT={VAT}, Pct={Pct}).",
                declaration.DeclarationNumber, declaration.TotalDuty, declaration.TotalVAT, effectiveGuaranteePct);
            return Result<Guid>.Success(Guid.Empty);
        }

        // Current balance = Σ Debit − Σ Credit (excluding soft-deleted rows).
        var currentBalance = await _context.GuaranteeLedgerEntries
            .Where(e => e.GuaranteeAccountId == account.Id && !e.IsDeleted)
            .SumAsync(e => e.EntryType == GuaranteeEntryType.Debit ? e.Amount : -e.Amount, cancellationToken);
        var availableLimit = account.TotalLimit - currentBalance;

        if (debitAmount > availableLimit)
        {
            return Result<Guid>.Failure(
                $"Guarantee '{account.AccountNumber}' ({account.Currency}) does not have enough available limit. " +
                $"Required: {debitAmount:0.00}, available: {availableLimit:0.00}, total: {account.TotalLimit:0.00}.");
        }

        // B3: per-authorization bond ceiling. Sum all OUTSTANDING (non-released,
        // non-soft-deleted) Debit entries tied to declarations filed under the
        // same LONAuthorization, minus Credits, and ensure the new debit still
        // fits under auth.GuaranteeAmount. Legacy ELON's Odobrenija.GarancijaIznos
        // was advisory (free scalar); here we enforce it.
        if (auth is not null && auth.GuaranteeAmount > 0)
        {
            // All declaration ids under this authorization.
            var authDeclIds = await _context.CustomsDeclarations
                .Where(d => d.LONAuthorizationId == auth.Id && !d.IsDeleted)
                .Select(d => d.Id)
                .ToListAsync(cancellationToken);
            authDeclIds.Add(declaration.Id); // include current (not yet saved)

            var authOutstanding = await _context.GuaranteeLedgerEntries
                .Where(e => !e.IsDeleted
                            && e.CustomsDeclarationId.HasValue
                            && authDeclIds.Contains(e.CustomsDeclarationId.Value))
                .SumAsync(
                    e => e.EntryType == GuaranteeEntryType.Debit ? e.Amount : -e.Amount,
                    cancellationToken);

            if (authOutstanding + debitAmount > auth.GuaranteeAmount)
            {
                return Result<Guid>.Failure(
                    $"LON authorization '{auth.AuthorizationNumber}' bond ceiling exceeded. " +
                    $"Current outstanding: {authOutstanding:0.00}, new debit: {debitAmount:0.00}, " +
                    $"authorized ceiling: {auth.GuaranteeAmount:0.00}. " +
                    $"Increase the authorization's GuaranteeAmount or close/credit existing bond commitments first.");
            }
        }

        var entry = new GuaranteeLedgerEntry
        {
            Id = Guid.NewGuid(),
            GuaranteeAccountId = account.Id,
            EntryDate = DateTime.UtcNow,
            EntryType = GuaranteeEntryType.Debit,
            Amount = debitAmount,
            Currency = declaration.Currency,
            Description = $"Auto-debit {procedure.Code} — {declaration.DeclarationNumber} ({effectiveGuaranteePct}% × (Duty+VAT))",
            ReferenceType = nameof(CustomsDeclaration),
            ReferenceId = declaration.Id,
            MRN = declaration.MRN,
            CustomsDeclarationId = declaration.Id,
            ExpectedReleaseDate = (auth?.CompletionPeriodDays ?? procedure.DueDays ?? 0) > 0
                ? declaration.DeclarationDate.AddDays(auth?.CompletionPeriodDays ?? procedure.DueDays ?? 0)
                : null,
            IsReleased = false
        };

        entry.AddDomainEvent(new GuaranteeDebitedEvent
        {
            GuaranteeAccountId = account.Id,
            Amount = debitAmount,
            MRN = declaration.MRN,
            CustomsDeclarationId = declaration.Id
        });

        _context.GuaranteeLedgerEntries.Add(entry);

        _logger.LogInformation(
            "Debited {Amount} {Currency} on guarantee account {AccountNumber} for declaration {DeclarationNumber} (MRN={MRN}).",
            debitAmount, declaration.Currency, account.AccountNumber, declaration.DeclarationNumber, declaration.MRN);

        return Result<Guid>.Success(entry.Id);
    }

    /// <summary>
    /// Dev-mode MRN: YYMK + 8 hex + "A1" (18 chars). Not customs-official —
    /// real MRN is returned by the customs portal and pasted in by the user.
    /// </summary>
    private static string GeneratePlaceholderMRN(DateTime declarationDate)
    {
        var yy = declarationDate.Year % 100;
        var hex = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
        return $"{yy:D2}MK{hex}A1";
    }
}
