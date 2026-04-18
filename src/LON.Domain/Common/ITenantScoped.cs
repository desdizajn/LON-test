namespace LON.Domain.Common;

/// <summary>
/// Marks a domain entity as belonging to a single tenant. Every query over
/// scoped entities will be filtered by the current tenant (global query
/// filter, added in P1.4). FK to Tenants is auto-configured via reflection
/// in ApplicationDbContext.OnModelCreating.
///
/// Do NOT apply to:
///  - Tenant itself (it IS the scope)
///  - Global reference data (TariffCode, CustomsRegulation, CodeListItem,
///    DeclarationRule, KnowledgeDocument/Chunk, CustomsProcedure, Role,
///    Permission). These are shared across tenants.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
