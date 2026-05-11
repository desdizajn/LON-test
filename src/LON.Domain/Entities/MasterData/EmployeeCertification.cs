using LON.Domain.Common;

namespace LON.Domain.Entities.MasterData;

/// <summary>
/// P16.C2 — per-employee certification / training record. Replaces the
/// localStorage-only persistence used by <c>pages/Hr/Training.tsx</c>.
/// </summary>
public class EmployeeCertification : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;

    /// <summary>Topic of the training / certification (e.g. "Sewing skill", "Safety").</summary>
    public string CertificationName { get; set; } = string.Empty;

    /// <summary>Optional taxonomy bucket — Sewing / Cutting / QC / Customs / ...</summary>
    public string? SkillArea { get; set; }

    public DateTime IssuedDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    /// <summary>Issuer name / training provider.</summary>
    public string? IssuingAuthority { get; set; }

    /// <summary>Optional certificate number / id.</summary>
    public string? CertificateNumber { get; set; }

    public string? Notes { get; set; }
}
