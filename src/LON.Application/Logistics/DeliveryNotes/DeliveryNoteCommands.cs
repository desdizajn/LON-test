using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Logistics;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Logistics.DeliveryNotes;

/// <summary>
/// Phase 17 §E7.6 — DTO shape returned by GET endpoints. Init-only properties
/// (memory: `feedback_positional_records_trap`) so JSON binding stays robust.
/// </summary>
public record DeliveryNoteDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public int DocumentType { get; init; }
    public string DocumentTypeName { get; init; } = string.Empty;
    public Guid RelatedDocumentId { get; init; }
    public DateTime DispatchDate { get; init; }
    public Guid FromLocationId { get; init; }
    public string? FromLocationCode { get; init; }
    public Guid? ToLocationId { get; init; }
    public string? ToLocationCode { get; init; }
    public Guid? ToPartnerId { get; init; }
    public string? ToPartnerCode { get; init; }
    public string? ToPartnerName { get; init; }
    public string? DriverName { get; init; }
    public string? VehicleRegistration { get; init; }
    public string? Remarks { get; init; }
    public int Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTime? ConfirmedAt { get; init; }
    public Guid? ConfirmedBy { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancelReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<DeliveryNoteLineDto> Lines { get; init; } = new();
}

public record DeliveryNoteLineDto
{
    public Guid Id { get; init; }
    public Guid ItemId { get; init; }
    public string? ItemCode { get; init; }
    public string? ItemName { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public Guid UoMId { get; init; }
    public string? UoMCode { get; init; }
    public string? BatchNumber { get; init; }
    public string? MRN { get; init; }
    public string? Notes { get; init; }
}

internal static class DeliveryNoteMapper
{
    public static DeliveryNoteDto Map(DeliveryNote dn) => new()
    {
        Id = dn.Id,
        Number = dn.Number,
        DocumentType = (int)dn.DocumentType,
        DocumentTypeName = dn.DocumentType.ToString(),
        RelatedDocumentId = dn.RelatedDocumentId,
        DispatchDate = dn.DispatchDate,
        FromLocationId = dn.FromLocationId,
        ToLocationId = dn.ToLocationId,
        ToPartnerId = dn.ToPartnerId,
        DriverName = dn.DriverName,
        VehicleRegistration = dn.VehicleRegistration,
        Remarks = dn.Remarks,
        Status = (int)dn.Status,
        StatusName = dn.Status.ToString(),
        ConfirmedAt = dn.ConfirmedAt,
        ConfirmedBy = dn.ConfirmedBy,
        CancelledAt = dn.CancelledAt,
        CancelReason = dn.CancelReason,
        CreatedAt = dn.CreatedAt,
        Lines = dn.Lines.Select(l => new DeliveryNoteLineDto
        {
            Id = l.Id,
            ItemId = l.ItemId,
            Description = l.Description,
            Quantity = l.Quantity,
            UoMId = l.UoMId,
            BatchNumber = l.BatchNumber,
            MRN = l.MRN,
            Notes = l.Notes,
        }).ToList(),
    };
}

// ─────────────────────────── Queries ───────────────────────────

public record GetDeliveryNotesQuery : ICommand<Result<List<DeliveryNoteDto>>>
{
    public int? Type { get; init; }
    public int? Status { get; init; }
    public Guid? PartnerId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed class GetDeliveryNotesQueryHandler
    : ICommandHandler<GetDeliveryNotesQuery, Result<List<DeliveryNoteDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetDeliveryNotesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<DeliveryNoteDto>>> Handle(GetDeliveryNotesQuery request, CancellationToken ct)
    {
        var query = _context.DeliveryNotes
            .Include(d => d.Lines)
            .AsQueryable();

        if (request.Type.HasValue)
            query = query.Where(d => (int)d.DocumentType == request.Type.Value);
        if (request.Status.HasValue)
            query = query.Where(d => (int)d.Status == request.Status.Value);
        if (request.PartnerId.HasValue)
            query = query.Where(d => d.ToPartnerId == request.PartnerId.Value);
        if (request.From.HasValue)
            query = query.Where(d => d.DispatchDate >= request.From.Value);
        if (request.To.HasValue)
            query = query.Where(d => d.DispatchDate <= request.To.Value);

        var skip = Math.Max(0, (request.Page - 1) * request.PageSize);
        var rows = await query
            .OrderByDescending(d => d.DispatchDate)
            .ThenByDescending(d => d.CreatedAt)
            .Skip(skip)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return Result<List<DeliveryNoteDto>>.Success(rows.Select(DeliveryNoteMapper.Map).ToList());
    }
}

public record GetDeliveryNoteByIdQuery(Guid Id) : ICommand<Result<DeliveryNoteDto>>;

public sealed class GetDeliveryNoteByIdQueryHandler
    : ICommandHandler<GetDeliveryNoteByIdQuery, Result<DeliveryNoteDto>>
{
    private readonly IApplicationDbContext _context;
    public GetDeliveryNoteByIdQueryHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<DeliveryNoteDto>> Handle(GetDeliveryNoteByIdQuery request, CancellationToken ct)
    {
        var dn = await _context.DeliveryNotes
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == request.Id, ct);
        if (dn is null)
            return Result<DeliveryNoteDto>.Failure($"DeliveryNote '{request.Id}' not found.");
        return Result<DeliveryNoteDto>.Success(DeliveryNoteMapper.Map(dn));
    }
}

// ─────────────────────────── Commands ───────────────────────────

public record UpdateDeliveryNoteCommand : ICommand<Result<DeliveryNoteDto>>
{
    public Guid Id { get; init; }
    public string? DriverName { get; init; }
    public string? VehicleRegistration { get; init; }
    public string? Remarks { get; init; }
    public DateTime? DispatchDate { get; init; }
}

public sealed class UpdateDeliveryNoteCommandHandler
    : ICommandHandler<UpdateDeliveryNoteCommand, Result<DeliveryNoteDto>>
{
    private readonly IApplicationDbContext _context;
    public UpdateDeliveryNoteCommandHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<DeliveryNoteDto>> Handle(UpdateDeliveryNoteCommand request, CancellationToken ct)
    {
        var dn = await _context.DeliveryNotes
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == request.Id, ct);
        if (dn is null)
            return Result<DeliveryNoteDto>.Failure($"DeliveryNote '{request.Id}' not found.");
        if (dn.Status != DeliveryNoteStatus.Draft)
            return Result<DeliveryNoteDto>.Failure(
                $"Only Draft delivery notes can be edited; this one is {dn.Status}.");

        if (request.DriverName is not null) dn.DriverName = request.DriverName;
        if (request.VehicleRegistration is not null) dn.VehicleRegistration = request.VehicleRegistration;
        if (request.Remarks is not null) dn.Remarks = request.Remarks;
        if (request.DispatchDate.HasValue) dn.DispatchDate = request.DispatchDate.Value;

        await _context.SaveChangesAsync(ct);
        return Result<DeliveryNoteDto>.Success(DeliveryNoteMapper.Map(dn));
    }
}

public record ConfirmDeliveryNoteCommand(Guid Id) : ICommand<Result<DeliveryNoteDto>>;

public sealed class ConfirmDeliveryNoteCommandHandler
    : ICommandHandler<ConfirmDeliveryNoteCommand, Result<DeliveryNoteDto>>
{
    private readonly IApplicationDbContext _context;
    public ConfirmDeliveryNoteCommandHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<DeliveryNoteDto>> Handle(ConfirmDeliveryNoteCommand request, CancellationToken ct)
    {
        var dn = await _context.DeliveryNotes
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == request.Id, ct);
        if (dn is null)
            return Result<DeliveryNoteDto>.Failure($"DeliveryNote '{request.Id}' not found.");
        if (dn.Status != DeliveryNoteStatus.Draft)
            return Result<DeliveryNoteDto>.Failure(
                $"Only Draft delivery notes can be confirmed; this one is {dn.Status}.");

        dn.Status = DeliveryNoteStatus.Sent;
        dn.ConfirmedAt = DateTime.UtcNow;
        // ConfirmedBy is set by the controller via a passing override (it has access to the
        // ICurrentUserService); the command-level handler doesn't pull the user directly so
        // tests can run without a live HTTP context.

        await _context.SaveChangesAsync(ct);
        return Result<DeliveryNoteDto>.Success(DeliveryNoteMapper.Map(dn));
    }
}

public record CancelDeliveryNoteCommand(Guid Id, string? Reason = null) : ICommand<Result<DeliveryNoteDto>>;

public sealed class CancelDeliveryNoteCommandHandler
    : ICommandHandler<CancelDeliveryNoteCommand, Result<DeliveryNoteDto>>
{
    private readonly IApplicationDbContext _context;
    public CancelDeliveryNoteCommandHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<DeliveryNoteDto>> Handle(CancelDeliveryNoteCommand request, CancellationToken ct)
    {
        var dn = await _context.DeliveryNotes
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == request.Id, ct);
        if (dn is null)
            return Result<DeliveryNoteDto>.Failure($"DeliveryNote '{request.Id}' not found.");
        if (dn.Status != DeliveryNoteStatus.Draft)
            return Result<DeliveryNoteDto>.Failure(
                $"Only Draft delivery notes can be cancelled; this one is {dn.Status}.");

        dn.Status = DeliveryNoteStatus.Cancelled;
        dn.CancelledAt = DateTime.UtcNow;
        dn.CancelReason = request.Reason;

        await _context.SaveChangesAsync(ct);
        return Result<DeliveryNoteDto>.Success(DeliveryNoteMapper.Map(dn));
    }
}
