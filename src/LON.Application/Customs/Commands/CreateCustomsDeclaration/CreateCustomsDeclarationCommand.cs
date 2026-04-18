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

        // MRN: auto-generate a dev-mode placeholder if empty. 18 chars per
        // MK customs convention: <YY>MK<8-hex><check>. Production path will
        // pass the real MRN returned by the customs portal.
        var mrn = string.IsNullOrWhiteSpace(request.MRN)
            ? GeneratePlaceholderMRN(request.DeclarationDate)
            : request.MRN.Trim().ToUpperInvariant();

        // Enforce MRN uniqueness per tenant (prevents replays).
        var mrnCollision = await _context.CustomsDeclarations
            .AnyAsync(d => d.MRN == mrn, cancellationToken);
        if (mrnCollision)
            return Result<Guid>.Failure($"MRN '{mrn}' is already registered.");

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
            DeclarationType = "IM",
            ProcedureCode = procedure.Code,
            SenderName = request.SenderName,
            SenderAddress = request.SenderAddress,
            SenderCountry = request.SenderCountry,
            CountryOfDispatch = request.CountryOfDispatch,
            CountryOfDestination = request.CountryOfDestination,
            SpecialRemarks = request.SpecialRemarks,
            Status = request.Status ?? DeclarationStatus.Registered, // auto-MRN → Registered
            IsCleared = false
        };

        // Line-level duty/VAT: per-line Carina = CustomsValue * DutyRate / 100;
        // Danok (VAT) base = CustomsValue + Carina. Matches legacy
        // PresmetajDavackiPoNaim (see ELON_Research/04).
        decimal totalDuty = 0;
        decimal totalVAT = 0;
        int lineNumber = 1;
        decimal totalQuantity = 0;

        foreach (var lineDto in request.Lines)
        {
            var dutyAmount = Math.Round(lineDto.CustomsValue * lineDto.DutyRate / 100m, 2, MidpointRounding.AwayFromZero);
            var vatAmount = Math.Round((lineDto.CustomsValue + dutyAmount) * lineDto.VATRate / 100m, 2, MidpointRounding.AwayFromZero);

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
                DutyRate = lineDto.DutyRate,
                DutyAmount = dutyAmount,
                VATRate = lineDto.VATRate,
                VATAmount = vatAmount,
                OtherCharges = 0
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
            _context.MRNRegistries.Add(new MRNRegistry
            {
                Id = Guid.NewGuid(),
                MRN = mrn,
                CustomsDeclarationId = declaration.Id,
                RegistrationDate = request.DeclarationDate,
                TotalQuantity = totalQuantity,
                UsedQuantity = 0m,
                ExpiryDate = procedure.DueDays.HasValue
                    ? request.DeclarationDate.AddDays(procedure.DueDays.Value)
                    : null,
                IsActive = true
            });
        }

        // P2.2 — auto-debit the guarantee bond for procedures that require one.
        // Computed atomically with the declaration so no "orphan" declarations
        // exist without a bond reservation. HARD fail if no matching account
        // or if the debit would exceed the available limit — compliance posture
        // deliberately stricter than legacy ELON (Одобренија.ГаранцијаИзнос
        // was advisory; here we enforce the ceiling).
        if (procedure.RequiresGuarantee && procedure.GuaranteePercentage > 0)
        {
            var debitResult = await TryDebitGuaranteeAsync(declaration, procedure, cancellationToken);
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
            fullLiability * procedure.GuaranteePercentage / 100m,
            2, MidpointRounding.AwayFromZero);

        if (debitAmount <= 0m)
        {
            _logger.LogInformation(
                "Guarantee debit skipped for declaration {DeclarationNumber}: amount is zero (Duty={Duty}, VAT={VAT}, Pct={Pct}).",
                declaration.DeclarationNumber, declaration.TotalDuty, declaration.TotalVAT, procedure.GuaranteePercentage);
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

        var entry = new GuaranteeLedgerEntry
        {
            Id = Guid.NewGuid(),
            GuaranteeAccountId = account.Id,
            EntryDate = DateTime.UtcNow,
            EntryType = GuaranteeEntryType.Debit,
            Amount = debitAmount,
            Currency = declaration.Currency,
            Description = $"Auto-debit {procedure.Code} — {declaration.DeclarationNumber} ({procedure.GuaranteePercentage}% × (Duty+VAT))",
            ReferenceType = nameof(CustomsDeclaration),
            ReferenceId = declaration.Id,
            MRN = declaration.MRN,
            CustomsDeclarationId = declaration.Id,
            ExpectedReleaseDate = procedure.DueDays.HasValue
                ? declaration.DeclarationDate.AddDays(procedure.DueDays.Value)
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
