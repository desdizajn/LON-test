using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Finance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Finance.SupplierInvoices;

/// <summary>
/// Derived status used in read projections. "Overdue" is NOT a persisted
/// value — it's a function of (Status=Open, DueDate &lt; today). Callers
/// filtering by `Overdue` get the synthetic projection.
/// </summary>
public enum SupplierInvoiceProjectedStatus
{
    Open = 1,
    Paid = 2,
    Cancelled = 3,
    Overdue = 4,
}

public sealed record SupplierInvoiceDto(
    Guid Id,
    Guid TenantId,
    string Number,
    Guid SupplierPartnerId,
    string? SupplierCode,
    string? SupplierName,
    DateTime InvoiceDate,
    DateTime DueDate,
    decimal Amount,
    string Currency,
    SupplierInvoiceProjectedStatus Status,
    DateTime? PaidDate,
    string? Notes,
    DateTime CreatedAt,
    DateTime? ModifiedAt)
{
    public static SupplierInvoiceDto From(SupplierInvoice e, DateTime today)
    {
        SupplierInvoiceProjectedStatus projected = e.Status switch
        {
            SupplierInvoiceStatus.Paid => SupplierInvoiceProjectedStatus.Paid,
            SupplierInvoiceStatus.Cancelled => SupplierInvoiceProjectedStatus.Cancelled,
            _ => e.DueDate.Date < today.Date
                ? SupplierInvoiceProjectedStatus.Overdue
                : SupplierInvoiceProjectedStatus.Open,
        };
        return new(
            e.Id, e.TenantId, e.Number, e.SupplierPartnerId,
            e.SupplierPartner?.Code, e.SupplierPartner?.Name,
            e.InvoiceDate, e.DueDate, e.Amount, e.Currency, projected,
            e.PaidDate, e.Notes, e.CreatedAt, e.ModifiedAt);
    }
}

public sealed record CreateSupplierInvoiceCommand : ICommand<Result<SupplierInvoiceDto>>
{
    public string Number { get; init; } = string.Empty;
    public Guid SupplierPartnerId { get; init; }
    public DateTime InvoiceDate { get; init; }
    public DateTime DueDate { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "EUR";
    public string? Notes { get; init; }
}

public class CreateSupplierInvoiceCommandHandler
    : ICommandHandler<CreateSupplierInvoiceCommand, Result<SupplierInvoiceDto>>
{
    private readonly IApplicationDbContext _ctx;
    public CreateSupplierInvoiceCommandHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<Result<SupplierInvoiceDto>> Handle(CreateSupplierInvoiceCommand r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Number))
            return Result<SupplierInvoiceDto>.Failure("Number is required.");
        if (r.SupplierPartnerId == Guid.Empty)
            return Result<SupplierInvoiceDto>.Failure("SupplierPartnerId is required.");
        if (r.Amount <= 0)
            return Result<SupplierInvoiceDto>.Failure("Amount must be positive.");

        var supplier = await _ctx.Partners.FirstOrDefaultAsync(p => p.Id == r.SupplierPartnerId, ct);
        if (supplier is null)
            return Result<SupplierInvoiceDto>.Failure($"Supplier '{r.SupplierPartnerId}' not found.");

        var e = new SupplierInvoice
        {
            Id = Guid.NewGuid(),
            Number = r.Number.Trim(),
            SupplierPartnerId = r.SupplierPartnerId,
            InvoiceDate = r.InvoiceDate.Date,
            DueDate = r.DueDate.Date,
            Amount = r.Amount,
            Currency = string.IsNullOrWhiteSpace(r.Currency) ? "EUR" : r.Currency.ToUpperInvariant(),
            Status = SupplierInvoiceStatus.Open,
            Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes,
        };
        _ctx.SupplierInvoices.Add(e);
        await _ctx.SaveChangesAsync(ct);
        e.SupplierPartner = supplier;
        return Result<SupplierInvoiceDto>.Success(SupplierInvoiceDto.From(e, DateTime.UtcNow));
    }
}

public sealed record UpdateSupplierInvoiceCommand : ICommand<Result<SupplierInvoiceDto>>
{
    public Guid Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public Guid SupplierPartnerId { get; init; }
    public DateTime InvoiceDate { get; init; }
    public DateTime DueDate { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "EUR";
    public SupplierInvoiceStatus Status { get; init; }
    public DateTime? PaidDate { get; init; }
    public string? Notes { get; init; }
}

public class UpdateSupplierInvoiceCommandHandler
    : ICommandHandler<UpdateSupplierInvoiceCommand, Result<SupplierInvoiceDto>>
{
    private readonly IApplicationDbContext _ctx;
    public UpdateSupplierInvoiceCommandHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<Result<SupplierInvoiceDto>> Handle(UpdateSupplierInvoiceCommand r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Number))
            return Result<SupplierInvoiceDto>.Failure("Number is required.");
        var e = await _ctx.SupplierInvoices
            .Include(x => x.SupplierPartner)
            .FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (e is null) return Result<SupplierInvoiceDto>.Failure($"SupplierInvoice '{r.Id}' not found.");

        e.Number = r.Number.Trim();
        e.SupplierPartnerId = r.SupplierPartnerId;
        e.InvoiceDate = r.InvoiceDate.Date;
        e.DueDate = r.DueDate.Date;
        e.Amount = r.Amount;
        e.Currency = string.IsNullOrWhiteSpace(r.Currency) ? e.Currency : r.Currency.ToUpperInvariant();
        e.Status = r.Status;
        e.PaidDate = r.Status == SupplierInvoiceStatus.Paid ? (r.PaidDate ?? DateTime.UtcNow) : null;
        e.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes;

        await _ctx.SaveChangesAsync(ct);
        return Result<SupplierInvoiceDto>.Success(SupplierInvoiceDto.From(e, DateTime.UtcNow));
    }
}

public sealed record DeleteSupplierInvoiceCommand(Guid Id) : ICommand<Result<bool>>;

public class DeleteSupplierInvoiceCommandHandler
    : ICommandHandler<DeleteSupplierInvoiceCommand, Result<bool>>
{
    private readonly IApplicationDbContext _ctx;
    public DeleteSupplierInvoiceCommandHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<Result<bool>> Handle(DeleteSupplierInvoiceCommand r, CancellationToken ct)
    {
        var e = await _ctx.SupplierInvoices.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (e is null) return Result<bool>.Failure($"SupplierInvoice '{r.Id}' not found.");
        e.IsDeleted = true;
        await _ctx.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed record GetSupplierInvoicesQuery(SupplierInvoiceProjectedStatus? Status)
    : IRequest<Result<IReadOnlyList<SupplierInvoiceDto>>>;

public class GetSupplierInvoicesQueryHandler
    : IRequestHandler<GetSupplierInvoicesQuery, Result<IReadOnlyList<SupplierInvoiceDto>>>
{
    private readonly IApplicationDbContext _ctx;
    public GetSupplierInvoicesQueryHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<Result<IReadOnlyList<SupplierInvoiceDto>>> Handle(GetSupplierInvoicesQuery r, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        // Filter by persisted status (Open|Paid|Cancelled). Overdue is a
        // projected filter applied after From() runs.
        IQueryable<SupplierInvoice> q = _ctx.SupplierInvoices.Include(x => x.SupplierPartner);
        if (r.Status is SupplierInvoiceProjectedStatus.Open or SupplierInvoiceProjectedStatus.Overdue)
            q = q.Where(x => x.Status == SupplierInvoiceStatus.Open);
        else if (r.Status == SupplierInvoiceProjectedStatus.Paid)
            q = q.Where(x => x.Status == SupplierInvoiceStatus.Paid);
        else if (r.Status == SupplierInvoiceProjectedStatus.Cancelled)
            q = q.Where(x => x.Status == SupplierInvoiceStatus.Cancelled);

        var rows = await q.OrderByDescending(x => x.InvoiceDate).ToListAsync(ct);
        IEnumerable<SupplierInvoiceDto> projected = rows.Select(x => SupplierInvoiceDto.From(x, today));

        if (r.Status == SupplierInvoiceProjectedStatus.Overdue)
            projected = projected.Where(p => p.Status == SupplierInvoiceProjectedStatus.Overdue);
        else if (r.Status == SupplierInvoiceProjectedStatus.Open)
            projected = projected.Where(p => p.Status == SupplierInvoiceProjectedStatus.Open);

        return Result<IReadOnlyList<SupplierInvoiceDto>>.Success(projected.ToList());
    }
}

public sealed record GetSupplierInvoiceByIdQuery(Guid Id)
    : IRequest<Result<SupplierInvoiceDto>>;

public class GetSupplierInvoiceByIdQueryHandler
    : IRequestHandler<GetSupplierInvoiceByIdQuery, Result<SupplierInvoiceDto>>
{
    private readonly IApplicationDbContext _ctx;
    public GetSupplierInvoiceByIdQueryHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<Result<SupplierInvoiceDto>> Handle(GetSupplierInvoiceByIdQuery r, CancellationToken ct)
    {
        var e = await _ctx.SupplierInvoices.Include(x => x.SupplierPartner)
            .FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (e is null) return Result<SupplierInvoiceDto>.Failure($"SupplierInvoice '{r.Id}' not found.");
        return Result<SupplierInvoiceDto>.Success(SupplierInvoiceDto.From(e, DateTime.UtcNow));
    }
}
