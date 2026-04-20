using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Finance;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Finance;

// ────────────────────────────── DTOs ──────────────────────────────

public sealed record RateCardEntryDto(
    Guid Id,
    Guid ContractId,
    RateType RateType,
    Guid? ItemId,
    string? ItemCode,
    string? ItemName,
    string? OperationCode,
    decimal RatePerUnit,
    string Currency,
    DateTime ValidFrom,
    DateTime? ValidTo,
    string? Notes);

public sealed record ClientContractDto(
    Guid Id,
    string Number,
    Guid PartnerId,
    string PartnerName,
    DateTime ValidFrom,
    DateTime? ValidTo,
    int PaymentTermsDays,
    string Currency,
    bool IsActive,
    string? Notes,
    IReadOnlyList<RateCardEntryDto> RateCard);

public sealed record InvoiceLineDto(
    Guid Id,
    int LineNumber,
    string Description,
    Guid? ItemId,
    string? ItemCode,
    Guid? RelatedProductionOrderId,
    string? RelatedProductionOrderNumber,
    Guid? RelatedShipmentId,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record InvoiceDto(
    Guid Id,
    string Number,
    Guid PartnerId,
    string PartnerName,
    Guid? ContractId,
    string? ContractNumber,
    DateTime IssueDate,
    DateTime DueDate,
    string Currency,
    decimal SubTotal,
    decimal TotalAmount,
    InvoiceStatus Status,
    DateTime? IssuedAt,
    DateTime? PaidAt,
    string? Notes,
    IReadOnlyList<InvoiceLineDto> Lines);

// ────────────────────────────── P12.3 — contracts ──────────────────────────────

public sealed record RateCardEntryInput(
    RateType RateType,
    Guid? ItemId,
    string? OperationCode,
    decimal RatePerUnit,
    string? Currency,
    DateTime ValidFrom,
    DateTime? ValidTo,
    string? Notes);

public sealed record CreateContractCommand(
    string Number,
    Guid PartnerId,
    DateTime ValidFrom,
    DateTime? ValidTo,
    int PaymentTermsDays,
    string Currency,
    string? Notes,
    IReadOnlyList<RateCardEntryInput>? RateCard)
    : IRequest<Result<Guid>>;

public sealed class CreateContractHandler : IRequestHandler<CreateContractCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    public CreateContractHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<Guid>> Handle(CreateContractCommand request, CancellationToken ct)
    {
        var number = (request.Number ?? string.Empty).Trim();
        if (number.Length == 0)
            return Result<Guid>.Failure("contract.number_required", "Contract number is required.");
        if (!await _context.Partners.AnyAsync(p => p.Id == request.PartnerId, ct))
            return Result<Guid>.Failure("contract.partner_not_found", "Partner not found.");
        if (request.ValidTo is { } vt && vt < request.ValidFrom)
            return Result<Guid>.Failure("contract.range_invalid", "ValidTo cannot be before ValidFrom.");
        var currency = NormaliseCurrency(request.Currency);

        var existing = await _context.ClientContracts
            .AnyAsync(c => c.Number == number, ct);
        if (existing)
            return Result<Guid>.Failure("contract.duplicate_number", "Contract number already in use.");

        var contract = new ClientContract
        {
            Id = Guid.NewGuid(),
            Number = number,
            PartnerId = request.PartnerId,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            PaymentTermsDays = request.PaymentTermsDays <= 0 ? 30 : request.PaymentTermsDays,
            Currency = currency,
            IsActive = true,
            Notes = request.Notes?.Trim(),
        };
        _context.ClientContracts.Add(contract);

        if (request.RateCard is { Count: > 0 } rows)
        {
            foreach (var row in rows)
            {
                var err = ValidateRateInput(row);
                if (err is not null) return Result<Guid>.Failure(err.Value.Code, err.Value.Message);
                _context.RateCardEntries.Add(new RateCardEntry
                {
                    Id = Guid.NewGuid(),
                    ContractId = contract.Id,
                    RateType = row.RateType,
                    ItemId = row.ItemId,
                    OperationCode = row.OperationCode?.Trim(),
                    RatePerUnit = row.RatePerUnit,
                    Currency = NormaliseCurrency(row.Currency ?? currency),
                    ValidFrom = row.ValidFrom == default ? contract.ValidFrom : row.ValidFrom,
                    ValidTo = row.ValidTo,
                    Notes = row.Notes?.Trim(),
                });
            }
        }

        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(contract.Id);
    }

    internal static (string Code, string Message)? ValidateRateInput(RateCardEntryInput row)
    {
        if (row.RatePerUnit < 0)
            return ("contract.rate_negative", "Rate cannot be negative.");
        if (row.RateType == RateType.PerPiece && row.ItemId is null)
            return ("contract.rate_missing_item", "PerPiece rate requires ItemId.");
        if (row.RateType == RateType.PerMinute && string.IsNullOrWhiteSpace(row.OperationCode))
            return ("contract.rate_missing_operation", "PerMinute rate requires OperationCode.");
        if (row.ValidTo is { } vt && vt < row.ValidFrom && row.ValidFrom != default)
            return ("contract.rate_range_invalid", "Rate ValidTo cannot be before ValidFrom.");
        return null;
    }

    internal static string NormaliseCurrency(string? v)
    {
        var c = (v ?? "EUR").Trim().ToUpperInvariant();
        return c.Length == 3 ? c : "EUR";
    }
}

public sealed record UpdateContractCommand(
    Guid Id,
    DateTime? ValidTo,
    int PaymentTermsDays,
    bool IsActive,
    string? Notes) : IRequest<Result>;

public sealed class UpdateContractHandler : IRequestHandler<UpdateContractCommand, Result>
{
    private readonly IApplicationDbContext _context;
    public UpdateContractHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result> Handle(UpdateContractCommand request, CancellationToken ct)
    {
        var contract = await _context.ClientContracts.FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (contract is null) return Result.Failure("contract.not_found", "Contract not found.");
        if (request.ValidTo is { } vt && vt < contract.ValidFrom)
            return Result.Failure("contract.range_invalid", "ValidTo cannot be before ValidFrom.");

        contract.ValidTo = request.ValidTo;
        contract.PaymentTermsDays = request.PaymentTermsDays <= 0 ? contract.PaymentTermsDays : request.PaymentTermsDays;
        contract.IsActive = request.IsActive;
        contract.Notes = request.Notes?.Trim();
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record UpsertRateCardEntryCommand(
    Guid ContractId,
    Guid? EntryId,
    RateType RateType,
    Guid? ItemId,
    string? OperationCode,
    decimal RatePerUnit,
    string? Currency,
    DateTime ValidFrom,
    DateTime? ValidTo,
    string? Notes) : IRequest<Result<Guid>>;

public sealed class UpsertRateCardEntryHandler : IRequestHandler<UpsertRateCardEntryCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    public UpsertRateCardEntryHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<Guid>> Handle(UpsertRateCardEntryCommand request, CancellationToken ct)
    {
        var contract = await _context.ClientContracts.FirstOrDefaultAsync(c => c.Id == request.ContractId, ct);
        if (contract is null) return Result<Guid>.Failure("contract.not_found", "Contract not found.");

        var err = CreateContractHandler.ValidateRateInput(new RateCardEntryInput(
            request.RateType, request.ItemId, request.OperationCode, request.RatePerUnit,
            request.Currency, request.ValidFrom, request.ValidTo, request.Notes));
        if (err is not null) return Result<Guid>.Failure(err.Value.Code, err.Value.Message);

        RateCardEntry entry;
        if (request.EntryId is { } id)
        {
            var existing = await _context.RateCardEntries.FirstOrDefaultAsync(r => r.Id == id && r.ContractId == contract.Id, ct);
            if (existing is null) return Result<Guid>.Failure("contract.rate_not_found", "Rate entry not found.");
            existing.RateType = request.RateType;
            existing.ItemId = request.ItemId;
            existing.OperationCode = request.OperationCode?.Trim();
            existing.RatePerUnit = request.RatePerUnit;
            existing.Currency = CreateContractHandler.NormaliseCurrency(request.Currency ?? contract.Currency);
            existing.ValidFrom = request.ValidFrom;
            existing.ValidTo = request.ValidTo;
            existing.Notes = request.Notes?.Trim();
            entry = existing;
        }
        else
        {
            entry = new RateCardEntry
            {
                Id = Guid.NewGuid(),
                ContractId = contract.Id,
                RateType = request.RateType,
                ItemId = request.ItemId,
                OperationCode = request.OperationCode?.Trim(),
                RatePerUnit = request.RatePerUnit,
                Currency = CreateContractHandler.NormaliseCurrency(request.Currency ?? contract.Currency),
                ValidFrom = request.ValidFrom,
                ValidTo = request.ValidTo,
                Notes = request.Notes?.Trim(),
            };
            _context.RateCardEntries.Add(entry);
        }

        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(entry.Id);
    }
}

public sealed record DeleteRateCardEntryCommand(Guid ContractId, Guid EntryId) : IRequest<Result>;

public sealed class DeleteRateCardEntryHandler : IRequestHandler<DeleteRateCardEntryCommand, Result>
{
    private readonly IApplicationDbContext _context;
    public DeleteRateCardEntryHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result> Handle(DeleteRateCardEntryCommand request, CancellationToken ct)
    {
        var entry = await _context.RateCardEntries.FirstOrDefaultAsync(r => r.Id == request.EntryId && r.ContractId == request.ContractId, ct);
        if (entry is null) return Result.Failure("contract.rate_not_found", "Rate entry not found.");
        entry.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record GetContractsQuery(Guid? PartnerId, bool? ActiveOnly)
    : IRequest<Result<IReadOnlyList<ClientContractDto>>>;

public sealed class GetContractsHandler
    : IRequestHandler<GetContractsQuery, Result<IReadOnlyList<ClientContractDto>>>
{
    private readonly IApplicationDbContext _context;
    public GetContractsHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<IReadOnlyList<ClientContractDto>>> Handle(GetContractsQuery request, CancellationToken ct)
    {
        var q = _context.ClientContracts.AsNoTracking();
        if (request.PartnerId.HasValue) q = q.Where(c => c.PartnerId == request.PartnerId.Value);
        if (request.ActiveOnly == true)
        {
            var now = DateTime.UtcNow;
            q = q.Where(c => c.IsActive && c.ValidFrom <= now && (c.ValidTo == null || c.ValidTo >= now));
        }

        var contracts = await q
            .OrderByDescending(c => c.ValidFrom)
            .Select(c => new
            {
                c.Id,
                c.Number,
                c.PartnerId,
                PartnerName = c.Partner.Name,
                c.ValidFrom,
                c.ValidTo,
                c.PaymentTermsDays,
                c.Currency,
                c.IsActive,
                c.Notes,
            })
            .ToListAsync(ct);

        var ids = contracts.Select(c => c.Id).ToList();
        var rates = await _context.RateCardEntries.AsNoTracking()
            .Where(r => ids.Contains(r.ContractId))
            .Select(r => new RateCardEntryDto(
                r.Id, r.ContractId, r.RateType, r.ItemId,
                r.Item != null ? r.Item.Code : null,
                r.Item != null ? r.Item.Name : null,
                r.OperationCode, r.RatePerUnit, r.Currency,
                r.ValidFrom, r.ValidTo, r.Notes))
            .ToListAsync(ct);
        var ratesByContract = rates.GroupBy(r => r.ContractId).ToDictionary(g => g.Key, g => (IReadOnlyList<RateCardEntryDto>)g.ToList());

        var result = contracts.Select(c => new ClientContractDto(
            c.Id, c.Number, c.PartnerId, c.PartnerName, c.ValidFrom, c.ValidTo,
            c.PaymentTermsDays, c.Currency, c.IsActive, c.Notes,
            ratesByContract.TryGetValue(c.Id, out var r) ? r : Array.Empty<RateCardEntryDto>()))
            .ToList();

        return Result<IReadOnlyList<ClientContractDto>>.Success(result);
    }
}

public sealed record GetContractByIdQuery(Guid Id) : IRequest<Result<ClientContractDto>>;

public sealed class GetContractByIdHandler : IRequestHandler<GetContractByIdQuery, Result<ClientContractDto>>
{
    private readonly IApplicationDbContext _context;
    public GetContractByIdHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<ClientContractDto>> Handle(GetContractByIdQuery request, CancellationToken ct)
    {
        var c = await _context.ClientContracts.AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(x => new
            {
                x.Id, x.Number, x.PartnerId, PartnerName = x.Partner.Name,
                x.ValidFrom, x.ValidTo, x.PaymentTermsDays, x.Currency, x.IsActive, x.Notes,
            })
            .FirstOrDefaultAsync(ct);
        if (c is null) return Result<ClientContractDto>.Failure("contract.not_found", "Contract not found.");

        var rates = await _context.RateCardEntries.AsNoTracking()
            .Where(r => r.ContractId == request.Id)
            .OrderBy(r => r.RateType).ThenBy(r => r.ValidFrom)
            .Select(r => new RateCardEntryDto(
                r.Id, r.ContractId, r.RateType, r.ItemId,
                r.Item != null ? r.Item.Code : null,
                r.Item != null ? r.Item.Name : null,
                r.OperationCode, r.RatePerUnit, r.Currency,
                r.ValidFrom, r.ValidTo, r.Notes))
            .ToListAsync(ct);

        return Result<ClientContractDto>.Success(new ClientContractDto(
            c.Id, c.Number, c.PartnerId, c.PartnerName, c.ValidFrom, c.ValidTo,
            c.PaymentTermsDays, c.Currency, c.IsActive, c.Notes, rates));
    }
}

// ────────────────────────────── P12.2 — invoices ──────────────────────────────

public sealed record InvoiceLineInput(
    string Description,
    Guid? ItemId,
    Guid? RelatedProductionOrderId,
    Guid? RelatedShipmentId,
    decimal Quantity,
    decimal UnitPrice);

public sealed record CreateInvoiceCommand(
    Guid PartnerId,
    Guid? ContractId,
    DateTime? IssueDate,
    DateTime? DueDate,
    string? Currency,
    string? Notes,
    IReadOnlyList<InvoiceLineInput>? Lines) : IRequest<Result<Guid>>;

public sealed class CreateInvoiceHandler : IRequestHandler<CreateInvoiceCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    public CreateInvoiceHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<Guid>> Handle(CreateInvoiceCommand request, CancellationToken ct)
    {
        if (!await _context.Partners.AnyAsync(p => p.Id == request.PartnerId, ct))
            return Result<Guid>.Failure("invoice.partner_not_found", "Partner not found.");

        ClientContract? contract = null;
        if (request.ContractId is { } cid)
        {
            contract = await _context.ClientContracts.FirstOrDefaultAsync(c => c.Id == cid, ct);
            if (contract is null) return Result<Guid>.Failure("invoice.contract_not_found", "Contract not found.");
            if (contract.PartnerId != request.PartnerId)
                return Result<Guid>.Failure("invoice.contract_partner_mismatch", "Contract partner differs from invoice partner.");
        }

        var currency = CreateContractHandler.NormaliseCurrency(request.Currency ?? contract?.Currency ?? "EUR");
        var issue = request.IssueDate?.Date ?? DateTime.UtcNow.Date;
        var due = request.DueDate?.Date ?? issue.AddDays(contract?.PaymentTermsDays ?? 30);
        if (due < issue) return Result<Guid>.Failure("invoice.due_before_issue", "Due date cannot be before issue date.");

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            Number = ProvisionalDraftNumber(),
            PartnerId = request.PartnerId,
            ContractId = request.ContractId,
            IssueDate = issue,
            DueDate = due,
            Currency = currency,
            Status = InvoiceStatus.Draft,
            Notes = request.Notes?.Trim(),
        };

        var lineNumber = 1;
        decimal subTotal = 0m;
        if (request.Lines is { Count: > 0 } lines)
        {
            foreach (var l in lines)
            {
                var err = ValidateLine(l);
                if (err is not null) return Result<Guid>.Failure(err.Value.Code, err.Value.Message);
                var total = Math.Round(l.Quantity * l.UnitPrice, 4);
                subTotal += total;
                _context.InvoiceLines.Add(new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoice.Id,
                    LineNumber = lineNumber++,
                    Description = (l.Description ?? string.Empty).Trim(),
                    ItemId = l.ItemId,
                    RelatedProductionOrderId = l.RelatedProductionOrderId,
                    RelatedShipmentId = l.RelatedShipmentId,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    LineTotal = total,
                });
            }
        }
        invoice.SubTotal = subTotal;
        invoice.TotalAmount = subTotal;
        _context.Invoices.Add(invoice);

        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(invoice.Id);
    }

    internal static (string Code, string Message)? ValidateLine(InvoiceLineInput l)
    {
        if (string.IsNullOrWhiteSpace(l.Description))
            return ("invoice.line_description_required", "Line description required.");
        if (l.Quantity <= 0) return ("invoice.line_quantity_invalid", "Line quantity must be positive.");
        if (l.UnitPrice < 0) return ("invoice.line_price_negative", "Line unit price cannot be negative.");
        return null;
    }

    internal static string ProvisionalDraftNumber()
        => $"DRAFT-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant()}";
}

public sealed record AddInvoiceLineCommand(Guid InvoiceId, InvoiceLineInput Line)
    : IRequest<Result<Guid>>;

public sealed class AddInvoiceLineHandler : IRequestHandler<AddInvoiceLineCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    public AddInvoiceLineHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<Guid>> Handle(AddInvoiceLineCommand request, CancellationToken ct)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct);
        if (invoice is null) return Result<Guid>.Failure("invoice.not_found", "Invoice not found.");
        if (invoice.Status != InvoiceStatus.Draft)
            return Result<Guid>.Failure("invoice.not_draft", "Only draft invoices can be edited.");

        var err = CreateInvoiceHandler.ValidateLine(request.Line);
        if (err is not null) return Result<Guid>.Failure(err.Value.Code, err.Value.Message);

        var lastLine = await _context.InvoiceLines.Where(l => l.InvoiceId == invoice.Id)
            .OrderByDescending(l => l.LineNumber).Select(l => l.LineNumber).FirstOrDefaultAsync(ct);
        var total = Math.Round(request.Line.Quantity * request.Line.UnitPrice, 4);
        var line = new InvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            LineNumber = lastLine + 1,
            Description = (request.Line.Description ?? string.Empty).Trim(),
            ItemId = request.Line.ItemId,
            RelatedProductionOrderId = request.Line.RelatedProductionOrderId,
            RelatedShipmentId = request.Line.RelatedShipmentId,
            Quantity = request.Line.Quantity,
            UnitPrice = request.Line.UnitPrice,
            LineTotal = total,
        };
        _context.InvoiceLines.Add(line);
        invoice.SubTotal += total;
        invoice.TotalAmount += total;

        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(line.Id);
    }
}

public sealed record RemoveInvoiceLineCommand(Guid InvoiceId, Guid LineId) : IRequest<Result>;

public sealed class RemoveInvoiceLineHandler : IRequestHandler<RemoveInvoiceLineCommand, Result>
{
    private readonly IApplicationDbContext _context;
    public RemoveInvoiceLineHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result> Handle(RemoveInvoiceLineCommand request, CancellationToken ct)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct);
        if (invoice is null) return Result.Failure("invoice.not_found", "Invoice not found.");
        if (invoice.Status != InvoiceStatus.Draft)
            return Result.Failure("invoice.not_draft", "Only draft invoices can be edited.");

        var line = await _context.InvoiceLines.FirstOrDefaultAsync(l => l.Id == request.LineId && l.InvoiceId == invoice.Id, ct);
        if (line is null) return Result.Failure("invoice.line_not_found", "Invoice line not found.");

        line.IsDeleted = true;
        invoice.SubTotal -= line.LineTotal;
        invoice.TotalAmount -= line.LineTotal;
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// P12.2 — generate invoice draft from a completed production order.
/// Rate resolution: (ContractId ?? ActiveContractForPartnerAtIssueDate) → find
/// PerPiece RateCardEntry where ItemId == PO.ItemId and (ValidFrom..ValidTo)
/// covers `IssueDate`. Quantity defaults to PO.ProducedQuantity. If no
/// contract/rate matches, returns a structured error so the UI can prompt the
/// user to fill a contract or override UnitPrice.
/// </summary>
public sealed record GenerateInvoiceFromPOCommand(
    Guid ProductionOrderId,
    Guid? ContractId,
    decimal? OverrideUnitPrice,
    DateTime? IssueDate) : IRequest<Result<Guid>>;

public sealed class GenerateInvoiceFromPOHandler : IRequestHandler<GenerateInvoiceFromPOCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    public GenerateInvoiceFromPOHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<Guid>> Handle(GenerateInvoiceFromPOCommand request, CancellationToken ct)
    {
        var po = await _context.ProductionOrders.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductionOrderId, ct);
        if (po is null) return Result<Guid>.Failure("invoice.po_not_found", "Production order not found.");
        if (po.CustomerPartnerId is null)
            return Result<Guid>.Failure("invoice.po_no_customer", "Production order has no customer partner.");
        if (po.ProducedQuantity <= 0)
            return Result<Guid>.Failure("invoice.po_no_quantity", "Production order has no produced quantity to bill.");

        var partnerId = po.CustomerPartnerId.Value;
        var issue = request.IssueDate?.Date ?? DateTime.UtcNow.Date;

        ClientContract? contract = null;
        if (request.ContractId is { } cid)
        {
            contract = await _context.ClientContracts.FirstOrDefaultAsync(c => c.Id == cid, ct);
            if (contract is null) return Result<Guid>.Failure("invoice.contract_not_found", "Contract not found.");
            if (contract.PartnerId != partnerId)
                return Result<Guid>.Failure("invoice.contract_partner_mismatch", "Contract partner differs from PO customer.");
        }
        else
        {
            contract = await _context.ClientContracts
                .Where(c => c.PartnerId == partnerId && c.IsActive
                        && c.ValidFrom <= issue && (c.ValidTo == null || c.ValidTo >= issue))
                .OrderByDescending(c => c.ValidFrom)
                .FirstOrDefaultAsync(ct);
        }

        decimal unitPrice;
        string currency;
        string? rateSource = null;

        if (request.OverrideUnitPrice is { } ovr)
        {
            if (ovr < 0) return Result<Guid>.Failure("invoice.line_price_negative", "Unit price cannot be negative.");
            unitPrice = ovr;
            currency = contract?.Currency ?? "EUR";
        }
        else
        {
            if (contract is null)
                return Result<Guid>.Failure("invoice.no_contract",
                    "No active contract for this customer. Provide ContractId or OverrideUnitPrice.");

            var rate = await _context.RateCardEntries
                .Where(r => r.ContractId == contract.Id
                         && r.RateType == RateType.PerPiece
                         && r.ItemId == po.ItemId
                         && r.ValidFrom <= issue
                         && (r.ValidTo == null || r.ValidTo >= issue))
                .OrderByDescending(r => r.ValidFrom)
                .FirstOrDefaultAsync(ct);
            if (rate is null)
                return Result<Guid>.Failure("invoice.no_rate",
                    "No rate card entry covering this PO item on IssueDate. Provide OverrideUnitPrice.");
            unitPrice = rate.RatePerUnit;
            currency = rate.Currency;
            rateSource = rate.Id.ToString();
        }

        var item = await _context.Items.AsNoTracking()
            .Where(i => i.Id == po.ItemId)
            .Select(i => new { i.Code, i.Name })
            .FirstAsync(ct);

        var due = issue.AddDays(contract?.PaymentTermsDays ?? 30);
        var lineTotal = Math.Round(po.ProducedQuantity * unitPrice, 4);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            Number = CreateInvoiceHandler.ProvisionalDraftNumber(),
            PartnerId = partnerId,
            ContractId = contract?.Id,
            IssueDate = issue,
            DueDate = due,
            Currency = currency,
            Status = InvoiceStatus.Draft,
            SubTotal = lineTotal,
            TotalAmount = lineTotal,
            Notes = rateSource is null ? "Auto-generated from PO (override price)" : $"Auto-generated from PO (rate {rateSource})",
        };
        _context.Invoices.Add(invoice);
        _context.InvoiceLines.Add(new InvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            LineNumber = 1,
            Description = $"{po.OrderNumber} — {item.Code} {item.Name}".Trim(),
            ItemId = po.ItemId,
            RelatedProductionOrderId = po.Id,
            Quantity = po.ProducedQuantity,
            UnitPrice = unitPrice,
            LineTotal = lineTotal,
        });

        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(invoice.Id);
    }
}

public sealed record IssueInvoiceCommand(Guid InvoiceId) : IRequest<Result<string>>;

public sealed class IssueInvoiceHandler : IRequestHandler<IssueInvoiceCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    public IssueInvoiceHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<string>> Handle(IssueInvoiceCommand request, CancellationToken ct)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct);
        if (invoice is null) return Result<string>.Failure("invoice.not_found", "Invoice not found.");
        if (invoice.Status != InvoiceStatus.Draft)
            return Result<string>.Failure("invoice.not_draft", "Only draft invoices can be issued.");
        var hasLines = await _context.InvoiceLines.AnyAsync(l => l.InvoiceId == invoice.Id, ct);
        if (!hasLines) return Result<string>.Failure("invoice.no_lines", "Invoice must have at least one line.");

        var year = invoice.IssueDate.Year;
        var prefix = $"INV-{year}-";
        var lastSeq = await _context.Invoices
            .Where(i => i.Id != invoice.Id && i.Number.StartsWith(prefix) && i.Status != InvoiceStatus.Cancelled)
            .Select(i => i.Number)
            .ToListAsync(ct);
        var maxSeq = lastSeq.Select(n =>
            int.TryParse(n.Substring(prefix.Length), out var s) ? s : 0)
            .DefaultIfEmpty(0).Max();
        invoice.Number = $"{prefix}{(maxSeq + 1):D4}";
        invoice.Status = InvoiceStatus.Issued;
        invoice.IssuedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return Result<string>.Success(invoice.Number);
    }
}

public sealed record MarkInvoicePaidCommand(Guid InvoiceId, DateTime? PaidAt) : IRequest<Result>;

public sealed class MarkInvoicePaidHandler : IRequestHandler<MarkInvoicePaidCommand, Result>
{
    private readonly IApplicationDbContext _context;
    public MarkInvoicePaidHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result> Handle(MarkInvoicePaidCommand request, CancellationToken ct)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct);
        if (invoice is null) return Result.Failure("invoice.not_found", "Invoice not found.");
        if (invoice.Status != InvoiceStatus.Issued)
            return Result.Failure("invoice.not_issued", "Only issued invoices can be marked paid.");
        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = request.PaidAt ?? DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record CancelInvoiceCommand(Guid InvoiceId, string? Reason) : IRequest<Result>;

public sealed class CancelInvoiceHandler : IRequestHandler<CancelInvoiceCommand, Result>
{
    private readonly IApplicationDbContext _context;
    public CancelInvoiceHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result> Handle(CancelInvoiceCommand request, CancellationToken ct)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct);
        if (invoice is null) return Result.Failure("invoice.not_found", "Invoice not found.");
        if (invoice.Status == InvoiceStatus.Cancelled)
            return Result.Failure("invoice.already_cancelled", "Invoice already cancelled.");
        if (invoice.Status == InvoiceStatus.Paid)
            return Result.Failure("invoice.paid_immutable", "Paid invoices cannot be cancelled.");
        invoice.Status = InvoiceStatus.Cancelled;
        var note = string.IsNullOrWhiteSpace(request.Reason) ? null : $"Cancelled: {request.Reason.Trim()}";
        invoice.Notes = string.IsNullOrEmpty(invoice.Notes) ? note : $"{invoice.Notes}\n{note}";
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record GetInvoicesQuery(Guid? PartnerId, InvoiceStatus? Status, DateTime? From, DateTime? To)
    : IRequest<Result<IReadOnlyList<InvoiceDto>>>;

public sealed class GetInvoicesHandler : IRequestHandler<GetInvoicesQuery, Result<IReadOnlyList<InvoiceDto>>>
{
    private readonly IApplicationDbContext _context;
    public GetInvoicesHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<IReadOnlyList<InvoiceDto>>> Handle(GetInvoicesQuery request, CancellationToken ct)
    {
        var q = _context.Invoices.AsNoTracking();
        if (request.PartnerId.HasValue) q = q.Where(i => i.PartnerId == request.PartnerId.Value);
        if (request.Status.HasValue) q = q.Where(i => i.Status == request.Status.Value);
        if (request.From.HasValue) q = q.Where(i => i.IssueDate >= request.From.Value.Date);
        if (request.To.HasValue) q = q.Where(i => i.IssueDate <= request.To.Value.Date);

        var rows = await q
            .OrderByDescending(i => i.IssueDate).ThenByDescending(i => i.Number)
            .Select(i => new InvoiceDto(
                i.Id, i.Number, i.PartnerId, i.Partner.Name, i.ContractId,
                i.Contract != null ? i.Contract.Number : null,
                i.IssueDate, i.DueDate, i.Currency, i.SubTotal, i.TotalAmount, i.Status,
                i.IssuedAt, i.PaidAt, i.Notes,
                new List<InvoiceLineDto>()))
            .ToListAsync(ct);

        return Result<IReadOnlyList<InvoiceDto>>.Success(rows);
    }
}

public sealed record GetInvoiceByIdQuery(Guid Id) : IRequest<Result<InvoiceDto>>;

public sealed class GetInvoiceByIdHandler : IRequestHandler<GetInvoiceByIdQuery, Result<InvoiceDto>>
{
    private readonly IApplicationDbContext _context;
    public GetInvoiceByIdHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<InvoiceDto>> Handle(GetInvoiceByIdQuery request, CancellationToken ct)
    {
        var i = await _context.Invoices.AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(x => new
            {
                x.Id, x.Number, x.PartnerId, PartnerName = x.Partner.Name, x.ContractId,
                ContractNumber = x.Contract != null ? x.Contract.Number : null,
                x.IssueDate, x.DueDate, x.Currency, x.SubTotal, x.TotalAmount, x.Status,
                x.IssuedAt, x.PaidAt, x.Notes,
            })
            .FirstOrDefaultAsync(ct);
        if (i is null) return Result<InvoiceDto>.Failure("invoice.not_found", "Invoice not found.");

        var lines = await _context.InvoiceLines.AsNoTracking()
            .Where(l => l.InvoiceId == request.Id)
            .OrderBy(l => l.LineNumber)
            .Select(l => new InvoiceLineDto(
                l.Id, l.LineNumber, l.Description, l.ItemId,
                l.Item != null ? l.Item.Code : null,
                l.RelatedProductionOrderId,
                l.RelatedProductionOrder != null ? l.RelatedProductionOrder.OrderNumber : null,
                l.RelatedShipmentId, l.Quantity, l.UnitPrice, l.LineTotal))
            .ToListAsync(ct);

        return Result<InvoiceDto>.Success(new InvoiceDto(
            i.Id, i.Number, i.PartnerId, i.PartnerName, i.ContractId, i.ContractNumber,
            i.IssueDate, i.DueDate, i.Currency, i.SubTotal, i.TotalAmount, i.Status,
            i.IssuedAt, i.PaidAt, i.Notes, lines));
    }
}
