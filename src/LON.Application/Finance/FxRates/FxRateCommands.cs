using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Finance;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Finance.FxRates;

public record GetFxRatesQuery : ICommand<Result<List<FxRateDto>>>
{
    public string? FromCurrency { get; init; }
    public string? ToCurrency { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
}

public sealed class GetFxRatesQueryHandler : ICommandHandler<GetFxRatesQuery, Result<List<FxRateDto>>>
{
    private readonly IApplicationDbContext _context;
    public GetFxRatesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<List<FxRateDto>>> Handle(GetFxRatesQuery request, CancellationToken ct)
    {
        var q = _context.FxRates.AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.FromCurrency))
            q = q.Where(r => r.FromCurrency == request.FromCurrency.ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(request.ToCurrency))
            q = q.Where(r => r.ToCurrency == request.ToCurrency.ToUpperInvariant());
        if (request.From.HasValue) q = q.Where(r => r.EffectiveDate >= request.From.Value);
        if (request.To.HasValue) q = q.Where(r => r.EffectiveDate <= request.To.Value);

        var pageSize = Math.Clamp(request.PageSize, 1, 500);
        var skip = Math.Max(0, (request.Page - 1) * pageSize);

        var rows = await q
            .OrderByDescending(r => r.EffectiveDate)
            .ThenBy(r => r.FromCurrency).ThenBy(r => r.ToCurrency)
            .Skip(skip).Take(pageSize)
            .Select(r => new FxRateDto
            {
                Id = r.Id,
                FromCurrency = r.FromCurrency,
                ToCurrency = r.ToCurrency,
                Rate = r.Rate,
                EffectiveDate = r.EffectiveDate,
                Source = (int)r.Source,
                SourceName = r.Source.ToString(),
                Notes = r.Notes,
            })
            .ToListAsync(ct);
        return Result<List<FxRateDto>>.Success(rows);
    }
}

public record CreateFxRateCommand : ICommand<Result<Guid>>
{
    public string FromCurrency { get; init; } = string.Empty;
    public string ToCurrency { get; init; } = string.Empty;
    public decimal Rate { get; init; }
    public DateTime EffectiveDate { get; init; }
    public int Source { get; init; } = 1;
    public string? Notes { get; init; }
}

public sealed class CreateFxRateCommandHandler : ICommandHandler<CreateFxRateCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public CreateFxRateCommandHandler(IApplicationDbContext context, ICurrentUserService user)
    {
        _context = context; _user = user;
    }

    public async Task<Result<Guid>> Handle(CreateFxRateCommand request, CancellationToken ct)
    {
        var from = request.FromCurrency?.Trim().ToUpperInvariant() ?? string.Empty;
        var to = request.ToCurrency?.Trim().ToUpperInvariant() ?? string.Empty;
        if (from.Length != 3 || to.Length != 3) return Result<Guid>.Failure("Currency must be 3-char ISO code.");
        if (from == to) return Result<Guid>.Failure("From and To currencies must differ.");
        if (request.Rate <= 0) return Result<Guid>.Failure("Rate must be positive.");

        var dup = await _context.FxRates.AnyAsync(r =>
            r.FromCurrency == from && r.ToCurrency == to && r.EffectiveDate == request.EffectiveDate && !r.IsDeleted, ct);
        if (dup) return Result<Guid>.Failure("DuplicateFxRate",
            $"An FX rate for {from}→{to} on {request.EffectiveDate:yyyy-MM-dd} already exists.");

        var entry = new FxRate
        {
            Id = Guid.NewGuid(),
            FromCurrency = from,
            ToCurrency = to,
            Rate = request.Rate,
            EffectiveDate = request.EffectiveDate.Date,
            Source = (FxRateSource)request.Source,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _user.AuditName,
        };
        _context.FxRates.Add(entry);
        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(entry.Id);
    }
}

public record UpdateFxRateCommand : ICommand<Result<Guid>>
{
    public Guid Id { get; init; }
    public decimal Rate { get; init; }
    public DateTime EffectiveDate { get; init; }
    public int Source { get; init; } = 1;
    public string? Notes { get; init; }
}

public sealed class UpdateFxRateCommandHandler : ICommandHandler<UpdateFxRateCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public UpdateFxRateCommandHandler(IApplicationDbContext context, ICurrentUserService user)
    {
        _context = context; _user = user;
    }

    public async Task<Result<Guid>> Handle(UpdateFxRateCommand request, CancellationToken ct)
    {
        var entry = await _context.FxRates.FirstOrDefaultAsync(r => r.Id == request.Id, ct);
        if (entry is null) return Result<Guid>.Failure($"FxRate '{request.Id}' not found.");
        if (request.Rate <= 0) return Result<Guid>.Failure("Rate must be positive.");
        entry.Rate = request.Rate;
        entry.EffectiveDate = request.EffectiveDate.Date;
        entry.Source = (FxRateSource)request.Source;
        entry.Notes = request.Notes;
        entry.ModifiedAt = DateTime.UtcNow;
        entry.ModifiedBy = _user.AuditName;
        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(entry.Id);
    }
}

public record DeleteFxRateCommand(Guid Id) : ICommand<Result<bool>>;

public sealed class DeleteFxRateCommandHandler : ICommandHandler<DeleteFxRateCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public DeleteFxRateCommandHandler(IApplicationDbContext context, ICurrentUserService user)
    {
        _context = context; _user = user;
    }

    public async Task<Result<bool>> Handle(DeleteFxRateCommand request, CancellationToken ct)
    {
        var entry = await _context.FxRates.FirstOrDefaultAsync(r => r.Id == request.Id, ct);
        if (entry is null) return Result<bool>.Failure($"FxRate '{request.Id}' not found.");
        entry.IsDeleted = true;
        entry.ModifiedAt = DateTime.UtcNow;
        entry.ModifiedBy = _user.AuditName;
        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public record GetEffectiveRateQuery(string From, string To, DateTime AsOf) : ICommand<Result<decimal>>;

public sealed class GetEffectiveRateQueryHandler : ICommandHandler<GetEffectiveRateQuery, Result<decimal>>
{
    private readonly IFxRateService _service;
    public GetEffectiveRateQueryHandler(IFxRateService service) => _service = service;

    public async Task<Result<decimal>> Handle(GetEffectiveRateQuery request, CancellationToken ct)
    {
        try
        {
            var rate = await _service.GetRateAsync(request.From, request.To, request.AsOf, ct);
            return Result<decimal>.Success(rate);
        }
        catch (FxRateMissingException ex)
        {
            return Result<decimal>.Failure("FxRateMissing", ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Result<decimal>.Failure(ex.Message);
        }
    }
}

public record FxRateDto
{
    public Guid Id { get; init; }
    public string FromCurrency { get; init; } = string.Empty;
    public string ToCurrency { get; init; } = string.Empty;
    public decimal Rate { get; init; }
    public DateTime EffectiveDate { get; init; }
    public int Source { get; init; }
    public string SourceName { get; init; } = string.Empty;
    public string? Notes { get; init; }
}
