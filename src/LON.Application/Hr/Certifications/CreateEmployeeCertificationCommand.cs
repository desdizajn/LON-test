using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Hr.Certifications;

public sealed record CreateEmployeeCertificationCommand : ICommand<Result<EmployeeCertificationDto>>
{
    public Guid EmployeeId { get; init; }
    public string CertificationName { get; init; } = string.Empty;
    public string? SkillArea { get; init; }
    public DateTime IssuedDate { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public string? IssuingAuthority { get; init; }
    public string? CertificateNumber { get; init; }
    public string? Notes { get; init; }
}

public class CreateEmployeeCertificationCommandHandler
    : ICommandHandler<CreateEmployeeCertificationCommand, Result<EmployeeCertificationDto>>
{
    private readonly IApplicationDbContext _context;

    public CreateEmployeeCertificationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<EmployeeCertificationDto>> Handle(
        CreateEmployeeCertificationCommand request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CertificationName))
            return Result<EmployeeCertificationDto>.Failure("CertificationName is required.");

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId, ct);
        if (employee is null)
            return Result<EmployeeCertificationDto>.Failure($"Employee '{request.EmployeeId}' not found.");

        var entity = new EmployeeCertification
        {
            Id = Guid.NewGuid(),
            EmployeeId = request.EmployeeId,
            CertificationName = request.CertificationName.Trim(),
            SkillArea = string.IsNullOrWhiteSpace(request.SkillArea) ? null : request.SkillArea.Trim(),
            IssuedDate = request.IssuedDate == default ? DateTime.UtcNow : request.IssuedDate,
            ExpiryDate = request.ExpiryDate,
            IssuingAuthority = string.IsNullOrWhiteSpace(request.IssuingAuthority) ? null : request.IssuingAuthority.Trim(),
            CertificateNumber = string.IsNullOrWhiteSpace(request.CertificateNumber) ? null : request.CertificateNumber.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes,
        };

        _context.EmployeeCertifications.Add(entity);
        await _context.SaveChangesAsync(ct);

        return Result<EmployeeCertificationDto>.Success(EmployeeCertificationDto.From(entity, employee));
    }
}
