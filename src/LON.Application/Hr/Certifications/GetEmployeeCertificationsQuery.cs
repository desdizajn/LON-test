using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Hr.Certifications;

/// <summary>
/// Optional <paramref name="EmployeeId"/> narrows to a single employee.
/// When omitted, returns every certification visible to the tenant.
/// </summary>
public sealed record GetEmployeeCertificationsQuery(Guid? EmployeeId)
    : IRequest<Result<IReadOnlyList<EmployeeCertificationDto>>>;

public class GetEmployeeCertificationsQueryHandler
    : IRequestHandler<GetEmployeeCertificationsQuery, Result<IReadOnlyList<EmployeeCertificationDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetEmployeeCertificationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<EmployeeCertificationDto>>> Handle(
        GetEmployeeCertificationsQuery request,
        CancellationToken ct)
    {
        var query = _context.EmployeeCertifications.Include(c => c.Employee).AsQueryable();
        if (request.EmployeeId.HasValue)
            query = query.Where(c => c.EmployeeId == request.EmployeeId.Value);

        var rows = await query
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync(ct);

        return Result<IReadOnlyList<EmployeeCertificationDto>>.Success(
            rows.Select(r => EmployeeCertificationDto.From(r, r.Employee)).ToList());
    }
}

/// <summary>
/// P16.C2 — certifications expiring within <paramref name="WithinDays"/>
/// days from today (inclusive). Already-expired rows count too if their
/// expiry is within the window in the past — the page sorts by daysLeft
/// to surface negative values first.
/// </summary>
public sealed record GetExpiringCertificationsQuery(int WithinDays)
    : IRequest<Result<IReadOnlyList<EmployeeCertificationDto>>>;

public class GetExpiringCertificationsQueryHandler
    : IRequestHandler<GetExpiringCertificationsQuery, Result<IReadOnlyList<EmployeeCertificationDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetExpiringCertificationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<EmployeeCertificationDto>>> Handle(
        GetExpiringCertificationsQuery request,
        CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var horizon = today.AddDays(Math.Max(request.WithinDays, 0));

        var rows = await _context.EmployeeCertifications
            .Include(c => c.Employee)
            .Where(c => c.ExpiryDate != null && c.ExpiryDate <= horizon)
            .OrderBy(c => c.ExpiryDate)
            .ToListAsync(ct);

        return Result<IReadOnlyList<EmployeeCertificationDto>>.Success(
            rows.Select(r => EmployeeCertificationDto.From(r, r.Employee)).ToList());
    }
}
