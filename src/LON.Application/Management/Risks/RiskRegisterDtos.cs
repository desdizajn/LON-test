using LON.Domain.Entities.Management;

namespace LON.Application.Management.Risks;

/// <summary>
/// P16.C1 — wire-friendly projection of <see cref="RiskRegisterItem"/>.
/// Enum-as-int kept simple; the frontend resolves the i18n labels.
/// </summary>
public sealed record RiskRegisterItemDto(
    Guid Id,
    Guid TenantId,
    RiskKind Kind,
    string Title,
    string? Category,
    RiskSeverity Severity,
    RiskStatus Status,
    string? Owner,
    string? Mitigation,
    string? Resolution,
    DateTime? DueDate,
    DateTime? ReviewDate,
    DateTime CreatedAt,
    DateTime? ModifiedAt)
{
    public static RiskRegisterItemDto From(RiskRegisterItem e) => new(
        e.Id,
        e.TenantId,
        e.Kind,
        e.Title,
        e.Category,
        e.Severity,
        e.Status,
        e.Owner,
        e.Mitigation,
        e.Resolution,
        e.DueDate,
        e.ReviewDate,
        e.CreatedAt,
        e.ModifiedAt);
}
