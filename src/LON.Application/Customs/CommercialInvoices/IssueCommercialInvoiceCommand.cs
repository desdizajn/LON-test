using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Customs;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.CommercialInvoices;

/// <summary>
/// Phase 17 §E8.5 (D4) — flip Status: Draft → Issued. Locks the invoice
/// (subsequent UPDATEs are rejected). Caller renders PDF separately via the
/// dedicated `/pdf` endpoint.
/// </summary>
public record IssueCommercialInvoiceCommand(Guid Id) : ICommand<Result<CommercialInvoiceDto>>;

public sealed class IssueCommercialInvoiceCommandHandler
    : ICommandHandler<IssueCommercialInvoiceCommand, Result<CommercialInvoiceDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public IssueCommercialInvoiceCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<CommercialInvoiceDto>> Handle(IssueCommercialInvoiceCommand request, CancellationToken ct)
    {
        var ci = await _context.CommercialInvoices
            .Include(c => c.Lines)
                .ThenInclude(l => l.Item)
            .Include(c => c.Lines)
                .ThenInclude(l => l.UoM)
            .Include(c => c.ConsigneePartner)
            .Include(c => c.ConsignorPartner)
            .Include(c => c.ClientOrder)
            .Include(c => c.Shipment)
            .Include(c => c.CustomsDeclaration)
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (ci is null)
            return Result<CommercialInvoiceDto>.Failure($"CommercialInvoice '{request.Id}' not found.");
        if (ci.Status != CommercialInvoiceStatus.Draft)
            return Result<CommercialInvoiceDto>.Failure(
                $"Only Draft commercial invoices can be issued; this one is {ci.Status}.");
        if (ci.Lines.Count == 0)
            return Result<CommercialInvoiceDto>.Failure("Cannot issue an invoice without lines.");

        ci.Status = CommercialInvoiceStatus.Issued;
        ci.IssuedAt = DateTime.UtcNow;
        ci.IssuedBy = _currentUser?.AuditName ?? "System";

        await _context.SaveChangesAsync(ct);
        return Result<CommercialInvoiceDto>.Success(CommercialInvoiceMapper.Map(ci));
    }
}

/// <summary>
/// Phase 17 §E8.5 (D4) — Cancel an invoice. Allowed from Draft OR Issued.
/// Records reason for audit. Once Cancelled, no further transitions.
/// </summary>
public record CancelCommercialInvoiceCommand(Guid Id, string? Reason = null) : ICommand<Result<CommercialInvoiceDto>>;

public sealed class CancelCommercialInvoiceCommandHandler
    : ICommandHandler<CancelCommercialInvoiceCommand, Result<CommercialInvoiceDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CancelCommercialInvoiceCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<CommercialInvoiceDto>> Handle(CancelCommercialInvoiceCommand request, CancellationToken ct)
    {
        var ci = await _context.CommercialInvoices
            .Include(c => c.Lines)
                .ThenInclude(l => l.Item)
            .Include(c => c.Lines)
                .ThenInclude(l => l.UoM)
            .Include(c => c.ConsigneePartner)
            .Include(c => c.ConsignorPartner)
            .Include(c => c.ClientOrder)
            .Include(c => c.Shipment)
            .Include(c => c.CustomsDeclaration)
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (ci is null)
            return Result<CommercialInvoiceDto>.Failure($"CommercialInvoice '{request.Id}' not found.");
        if (ci.Status == CommercialInvoiceStatus.Cancelled)
            return Result<CommercialInvoiceDto>.Failure("CommercialInvoice is already cancelled.");

        ci.Status = CommercialInvoiceStatus.Cancelled;
        ci.CancelledAt = DateTime.UtcNow;
        ci.CancelledBy = _currentUser?.AuditName ?? "System";
        ci.CancellationReason = request.Reason;

        await _context.SaveChangesAsync(ct);
        return Result<CommercialInvoiceDto>.Success(CommercialInvoiceMapper.Map(ci));
    }
}

/// <summary>
/// Phase 17 §E8.5 (D4) — soft-delete a commercial invoice (Draft only).
/// Use Cancel for Issued invoices.
/// </summary>
public record DeleteCommercialInvoiceCommand(Guid Id) : ICommand<Result<bool>>;

public sealed class DeleteCommercialInvoiceCommandHandler
    : ICommandHandler<DeleteCommercialInvoiceCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteCommercialInvoiceCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(DeleteCommercialInvoiceCommand request, CancellationToken ct)
    {
        var ci = await _context.CommercialInvoices
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (ci is null)
            return Result<bool>.Failure($"CommercialInvoice '{request.Id}' not found.");
        if (ci.Status != CommercialInvoiceStatus.Draft)
            return Result<bool>.Failure(
                $"Only Draft commercial invoices can be deleted; use Cancel for {ci.Status}.");

        ci.IsDeleted = true;
        ci.DeletedAt = DateTime.UtcNow;
        ci.DeletedBy = _currentUser?.AuditName ?? "System";

        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
