using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Hr.Certifications;

public sealed record UpdateEmployeeCertificationCommand : ICommand<Result<EmployeeCertificationDto>>
{
    public Guid Id { get; init; }
    public string CertificationName { get; init; } = string.Empty;
    public string? SkillArea { get; init; }
    public DateTime IssuedDate { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public string? IssuingAuthority { get; init; }
    public string? CertificateNumber { get; init; }
    public string? Notes { get; init; }
}

public class UpdateEmployeeCertificationCommandHandler
    : ICommandHandler<UpdateEmployeeCertificationCommand, Result<EmployeeCertificationDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateEmployeeCertificationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<EmployeeCertificationDto>> Handle(
        UpdateEmployeeCertificationCommand request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CertificationName))
            return Result<EmployeeCertificationDto>.Failure("CertificationName is required.");

        var entity = await _context.EmployeeCertifications
            .Include(c => c.Employee)
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (entity is null)
            return Result<EmployeeCertificationDto>.Failure($"EmployeeCertification '{request.Id}' not found.");

        entity.CertificationName = request.CertificationName.Trim();
        entity.SkillArea = string.IsNullOrWhiteSpace(request.SkillArea) ? null : request.SkillArea.Trim();
        entity.IssuedDate = request.IssuedDate == default ? entity.IssuedDate : request.IssuedDate;
        entity.ExpiryDate = request.ExpiryDate;
        entity.IssuingAuthority = string.IsNullOrWhiteSpace(request.IssuingAuthority) ? null : request.IssuingAuthority.Trim();
        entity.CertificateNumber = string.IsNullOrWhiteSpace(request.CertificateNumber) ? null : request.CertificateNumber.Trim();
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes;

        await _context.SaveChangesAsync(ct);
        return Result<EmployeeCertificationDto>.Success(EmployeeCertificationDto.From(entity, entity.Employee));
    }
}
