using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.ClientOrders;

/// <summary>
/// Phase 17 §E9 — toggle <see cref="LON.Domain.Entities.Customs.CustomsDeclarationLine.RazdolzenaDaNe"/>
/// for a single declaration line on a ClientOrder's import declarations.
/// Stamps the timestamp + auditing user; idempotent (setting the same value
/// twice doesn't error).
/// </summary>
public sealed record MarkLineRazdolzenaCommand(
    Guid ClientOrderId,
    Guid LineId,
    bool RazdolzenaDaNe) : ICommand<Result<bool>>;

public sealed class MarkLineRazdolzenaCommandHandler
    : ICommandHandler<MarkLineRazdolzenaCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public MarkLineRazdolzenaCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(MarkLineRazdolzenaCommand request, CancellationToken ct)
    {
        // Resolve the line via its parent declaration to guarantee it really
        // belongs to this ClientOrder — prevents cross-order tampering by
        // crafting LineId from a different order's payload.
        var line = await _context.CustomsDeclarationLines
            .Include(l => l.CustomsDeclaration)
            .FirstOrDefaultAsync(l => l.Id == request.LineId && !l.IsDeleted, ct);
        if (line is null)
            return Result<bool>.Failure($"CustomsDeclarationLine '{request.LineId}' not found.");
        if (line.CustomsDeclaration.ClientOrderId != request.ClientOrderId)
            return Result<bool>.Failure(
                $"Line {request.LineId} does not belong to ClientOrder {request.ClientOrderId}.");
        if (line.CustomsDeclaration.DeclarationType != "IM")
            return Result<bool>.Failure(
                "RazdolzenaDaNe applies only to IM declaration lines.");

        if (line.RazdolzenaDaNe == request.RazdolzenaDaNe)
            return Result<bool>.Success(true); // idempotent

        line.RazdolzenaDaNe = request.RazdolzenaDaNe;
        line.RazdolzenaAt = DateTime.UtcNow;
        line.RazdolzenaBy = _currentUser?.AuditName ?? "System";

        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
