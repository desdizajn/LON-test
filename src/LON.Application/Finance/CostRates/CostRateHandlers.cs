using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Finance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Finance.CostRates;

public sealed record CostRateDto(
    Guid Id,
    Guid TenantId,
    CostRateScope Scope,
    Guid? ScopeId,
    decimal? CostPerHour,
    decimal? CostPerUnit,
    string Currency,
    DateTime ValidFrom,
    DateTime? ValidTo,
    string? Notes,
    DateTime CreatedAt,
    DateTime? ModifiedAt)
{
    public static CostRateDto From(CostRate e) => new(
        e.Id, e.TenantId, e.Scope, e.ScopeId,
        e.CostPerHour, e.CostPerUnit, e.Currency, e.ValidFrom, e.ValidTo,
        e.Notes, e.CreatedAt, e.ModifiedAt);
}

public sealed record CreateCostRateCommand : ICommand<Result<CostRateDto>>
{
    public CostRateScope Scope { get; init; }
    public Guid? ScopeId { get; init; }
    public decimal? CostPerHour { get; init; }
    public decimal? CostPerUnit { get; init; }
    public string Currency { get; init; } = "EUR";
    public DateTime ValidFrom { get; init; }
    public DateTime? ValidTo { get; init; }
    public string? Notes { get; init; }
}

public class CreateCostRateCommandHandler : ICommandHandler<CreateCostRateCommand, Result<CostRateDto>>
{
    private readonly IApplicationDbContext _ctx;
    public CreateCostRateCommandHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<Result<CostRateDto>> Handle(CreateCostRateCommand r, CancellationToken ct)
    {
        if (r.CostPerHour is null && r.CostPerUnit is null)
            return Result<CostRateDto>.Failure("Either CostPerHour or CostPerUnit must be set.");
        if (string.IsNullOrWhiteSpace(r.Currency) || r.Currency.Length != 3)
            return Result<CostRateDto>.Failure("Currency must be a 3-letter ISO code.");

        var e = new CostRate
        {
            Id = Guid.NewGuid(),
            Scope = r.Scope,
            ScopeId = r.ScopeId,
            CostPerHour = r.CostPerHour,
            CostPerUnit = r.CostPerUnit,
            Currency = r.Currency.ToUpperInvariant(),
            ValidFrom = r.ValidFrom == default ? DateTime.UtcNow.Date : r.ValidFrom,
            ValidTo = r.ValidTo,
            Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes,
        };
        _ctx.CostRates.Add(e);
        await _ctx.SaveChangesAsync(ct);
        return Result<CostRateDto>.Success(CostRateDto.From(e));
    }
}

public sealed record UpdateCostRateCommand : ICommand<Result<CostRateDto>>
{
    public Guid Id { get; init; }
    public CostRateScope Scope { get; init; }
    public Guid? ScopeId { get; init; }
    public decimal? CostPerHour { get; init; }
    public decimal? CostPerUnit { get; init; }
    public string Currency { get; init; } = "EUR";
    public DateTime ValidFrom { get; init; }
    public DateTime? ValidTo { get; init; }
    public string? Notes { get; init; }
}

public class UpdateCostRateCommandHandler : ICommandHandler<UpdateCostRateCommand, Result<CostRateDto>>
{
    private readonly IApplicationDbContext _ctx;
    public UpdateCostRateCommandHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<Result<CostRateDto>> Handle(UpdateCostRateCommand r, CancellationToken ct)
    {
        if (r.CostPerHour is null && r.CostPerUnit is null)
            return Result<CostRateDto>.Failure("Either CostPerHour or CostPerUnit must be set.");

        var e = await _ctx.CostRates.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (e is null) return Result<CostRateDto>.Failure($"CostRate '{r.Id}' not found.");

        e.Scope = r.Scope;
        e.ScopeId = r.ScopeId;
        e.CostPerHour = r.CostPerHour;
        e.CostPerUnit = r.CostPerUnit;
        e.Currency = string.IsNullOrWhiteSpace(r.Currency) ? e.Currency : r.Currency.ToUpperInvariant();
        e.ValidFrom = r.ValidFrom == default ? e.ValidFrom : r.ValidFrom;
        e.ValidTo = r.ValidTo;
        e.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes;

        await _ctx.SaveChangesAsync(ct);
        return Result<CostRateDto>.Success(CostRateDto.From(e));
    }
}

public sealed record DeleteCostRateCommand(Guid Id) : ICommand<Result<bool>>;

public class DeleteCostRateCommandHandler : ICommandHandler<DeleteCostRateCommand, Result<bool>>
{
    private readonly IApplicationDbContext _ctx;
    public DeleteCostRateCommandHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<Result<bool>> Handle(DeleteCostRateCommand r, CancellationToken ct)
    {
        var e = await _ctx.CostRates.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (e is null) return Result<bool>.Failure($"CostRate '{r.Id}' not found.");
        e.IsDeleted = true;
        await _ctx.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed record GetCostRatesQuery(CostRateScope? Scope)
    : IRequest<Result<IReadOnlyList<CostRateDto>>>;

public class GetCostRatesQueryHandler
    : IRequestHandler<GetCostRatesQuery, Result<IReadOnlyList<CostRateDto>>>
{
    private readonly IApplicationDbContext _ctx;
    public GetCostRatesQueryHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<Result<IReadOnlyList<CostRateDto>>> Handle(GetCostRatesQuery r, CancellationToken ct)
    {
        var q = _ctx.CostRates.AsQueryable();
        if (r.Scope.HasValue) q = q.Where(x => x.Scope == r.Scope.Value);
        var rows = await q.OrderByDescending(x => x.ValidFrom).ToListAsync(ct);
        return Result<IReadOnlyList<CostRateDto>>.Success(rows.Select(CostRateDto.From).ToList());
    }
}
