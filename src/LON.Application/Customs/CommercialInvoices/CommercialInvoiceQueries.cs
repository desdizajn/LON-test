using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.CommercialInvoices;

/// <summary>
/// Phase 17 §E8.5 (D4) — list query with the typical hub-friendly filters.
/// All filters AND together. Default ordering: most-recent first.
/// </summary>
public record GetCommercialInvoicesQuery : ICommand<Result<List<CommercialInvoiceDto>>>
{
    public Guid? ClientOrderId { get; init; }
    public Guid? ConsigneePartnerId { get; init; }
    public int? Status { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed class GetCommercialInvoicesQueryHandler
    : ICommandHandler<GetCommercialInvoicesQuery, Result<List<CommercialInvoiceDto>>>
{
    private readonly IApplicationDbContext _context;
    public GetCommercialInvoicesQueryHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<List<CommercialInvoiceDto>>> Handle(GetCommercialInvoicesQuery request, CancellationToken ct)
    {
        var query = _context.CommercialInvoices
            .Include(ci => ci.Lines)
                .ThenInclude(l => l.Item)
            .Include(ci => ci.Lines)
                .ThenInclude(l => l.UoM)
            .Include(ci => ci.ConsigneePartner)
            .Include(ci => ci.ConsignorPartner)
            .Include(ci => ci.ClientOrder)
            .Include(ci => ci.Shipment)
            .Include(ci => ci.CustomsDeclaration)
            .AsQueryable();

        if (request.ClientOrderId.HasValue)
            query = query.Where(c => c.ClientOrderId == request.ClientOrderId.Value);
        if (request.ConsigneePartnerId.HasValue)
            query = query.Where(c => c.ConsigneePartnerId == request.ConsigneePartnerId.Value);
        if (request.Status.HasValue)
            query = query.Where(c => (int)c.Status == request.Status.Value);
        if (request.From.HasValue)
            query = query.Where(c => c.InvoiceDate >= request.From.Value);
        if (request.To.HasValue)
            query = query.Where(c => c.InvoiceDate <= request.To.Value);

        var skip = Math.Max(0, (request.Page - 1) * request.PageSize);
        var rows = await query
            .OrderByDescending(c => c.InvoiceDate)
            .ThenByDescending(c => c.CreatedAt)
            .Skip(skip)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return Result<List<CommercialInvoiceDto>>.Success(rows.Select(CommercialInvoiceMapper.Map).ToList());
    }
}

public record GetCommercialInvoiceByIdQuery(Guid Id) : ICommand<Result<CommercialInvoiceDto>>;

public sealed class GetCommercialInvoiceByIdQueryHandler
    : ICommandHandler<GetCommercialInvoiceByIdQuery, Result<CommercialInvoiceDto>>
{
    private readonly IApplicationDbContext _context;
    public GetCommercialInvoiceByIdQueryHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<CommercialInvoiceDto>> Handle(GetCommercialInvoiceByIdQuery request, CancellationToken ct)
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
        return Result<CommercialInvoiceDto>.Success(CommercialInvoiceMapper.Map(ci));
    }
}
