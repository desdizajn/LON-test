using LON.Domain.Entities.MasterData;

namespace LON.Application.Hr.Certifications;

public sealed record EmployeeCertificationDto(
    Guid Id,
    Guid TenantId,
    Guid EmployeeId,
    string? EmployeeName,
    string? EmployeeNumber,
    string CertificationName,
    string? SkillArea,
    DateTime IssuedDate,
    DateTime? ExpiryDate,
    string? IssuingAuthority,
    string? CertificateNumber,
    string? Notes,
    DateTime CreatedAt,
    DateTime? ModifiedAt)
{
    public static EmployeeCertificationDto From(EmployeeCertification e, Employee? emp) => new(
        e.Id,
        e.TenantId,
        e.EmployeeId,
        emp is null ? null : $"{emp.FirstName} {emp.LastName}".Trim(),
        emp?.EmployeeNumber,
        e.CertificationName,
        e.SkillArea,
        e.IssuedDate,
        e.ExpiryDate,
        e.IssuingAuthority,
        e.CertificateNumber,
        e.Notes,
        e.CreatedAt,
        e.ModifiedAt);
}
