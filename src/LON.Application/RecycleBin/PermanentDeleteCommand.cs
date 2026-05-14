using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.RecycleBin;

/// <summary>
/// Phase 17 §E14 — admin-only hard-delete from the recycle bin. v1 only
/// supports ClientOrder. The retention worker (90-day rolling) also calls
/// into this same logic.
/// </summary>
public record PermanentDeleteClientOrderCommand(Guid Id) : ICommand<Result<bool>>;

public sealed class PermanentDeleteClientOrderCommandHandler
    : ICommandHandler<PermanentDeleteClientOrderCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    public PermanentDeleteClientOrderCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<bool>> Handle(PermanentDeleteClientOrderCommand request, CancellationToken ct)
    {
        var order = await _context.ClientOrders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == request.Id, ct);
        if (order is null) return Result<bool>.Failure($"ClientOrder '{request.Id}' does not exist.");
        if (!order.IsDeleted)
            return Result<bool>.Failure("Only soft-deleted ClientOrders can be permanently deleted.");

        _context.ClientOrders.Remove(order);
        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
