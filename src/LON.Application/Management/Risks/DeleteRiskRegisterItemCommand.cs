using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Management.Risks;

public sealed record DeleteRiskRegisterItemCommand(Guid Id) : ICommand<Result<bool>>;

public class DeleteRiskRegisterItemCommandHandler
    : ICommandHandler<DeleteRiskRegisterItemCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public DeleteRiskRegisterItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(DeleteRiskRegisterItemCommand request, CancellationToken ct)
    {
        var entity = await _context.RiskRegisterItems
            .FirstOrDefaultAsync(r => r.Id == request.Id, ct);
        if (entity is null)
            return Result<bool>.Failure($"RiskRegisterItem '{request.Id}' not found.");

        // Soft-delete handled by ApplicationDbContext.SaveChangesAsync via the
        // BaseEntity.IsDeleted flag (set by EF's soft-delete interceptor).
        entity.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
