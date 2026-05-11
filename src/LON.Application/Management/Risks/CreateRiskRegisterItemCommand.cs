using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Management;

namespace LON.Application.Management.Risks;

public sealed record CreateRiskRegisterItemCommand : ICommand<Result<RiskRegisterItemDto>>
{
    public RiskKind Kind { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Category { get; init; }
    public RiskSeverity Severity { get; init; } = RiskSeverity.Medium;
    public RiskStatus Status { get; init; } = RiskStatus.Open;
    public string? Owner { get; init; }
    public string? Mitigation { get; init; }
    public string? Resolution { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? ReviewDate { get; init; }
}

public class CreateRiskRegisterItemCommandHandler
    : ICommandHandler<CreateRiskRegisterItemCommand, Result<RiskRegisterItemDto>>
{
    private readonly IApplicationDbContext _context;

    public CreateRiskRegisterItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<RiskRegisterItemDto>> Handle(
        CreateRiskRegisterItemCommand request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<RiskRegisterItemDto>.Failure("Title is required.");

        var entity = new RiskRegisterItem
        {
            Id = Guid.NewGuid(),
            Kind = request.Kind,
            Title = request.Title.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
            Severity = request.Severity,
            Status = request.Status,
            Owner = string.IsNullOrWhiteSpace(request.Owner) ? null : request.Owner.Trim(),
            Mitigation = string.IsNullOrWhiteSpace(request.Mitigation) ? null : request.Mitigation,
            Resolution = string.IsNullOrWhiteSpace(request.Resolution) ? null : request.Resolution,
            DueDate = request.DueDate,
            ReviewDate = request.ReviewDate,
        };

        _context.RiskRegisterItems.Add(entity);
        await _context.SaveChangesAsync(ct);

        return Result<RiskRegisterItemDto>.Success(RiskRegisterItemDto.From(entity));
    }
}
