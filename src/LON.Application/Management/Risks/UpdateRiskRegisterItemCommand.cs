using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Management;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Management.Risks;

public sealed record UpdateRiskRegisterItemCommand : ICommand<Result<RiskRegisterItemDto>>
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Category { get; init; }
    public RiskSeverity Severity { get; init; }
    public RiskStatus Status { get; init; }
    public string? Owner { get; init; }
    public string? Mitigation { get; init; }
    public string? Resolution { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? ReviewDate { get; init; }
}

public class UpdateRiskRegisterItemCommandHandler
    : ICommandHandler<UpdateRiskRegisterItemCommand, Result<RiskRegisterItemDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateRiskRegisterItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<RiskRegisterItemDto>> Handle(
        UpdateRiskRegisterItemCommand request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<RiskRegisterItemDto>.Failure("Title is required.");

        var entity = await _context.RiskRegisterItems
            .FirstOrDefaultAsync(r => r.Id == request.Id, ct);
        if (entity is null)
            return Result<RiskRegisterItemDto>.Failure($"RiskRegisterItem '{request.Id}' not found.");

        entity.Title = request.Title.Trim();
        entity.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        entity.Severity = request.Severity;
        entity.Status = request.Status;
        entity.Owner = string.IsNullOrWhiteSpace(request.Owner) ? null : request.Owner.Trim();
        entity.Mitigation = string.IsNullOrWhiteSpace(request.Mitigation) ? null : request.Mitigation;
        entity.Resolution = string.IsNullOrWhiteSpace(request.Resolution) ? null : request.Resolution;
        entity.DueDate = request.DueDate;
        entity.ReviewDate = request.ReviewDate;

        await _context.SaveChangesAsync(ct);
        return Result<RiskRegisterItemDto>.Success(RiskRegisterItemDto.From(entity));
    }
}
