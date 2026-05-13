namespace LON.Application.Common.Interfaces;

/// <summary>
/// Phase 17 §E1 — abstraction over per-tenant SQL SEQUENCE pulls.
///
/// Each numbered entity (ClientOrder, IM declaration, EX declaration, etc.)
/// has a per-tenant SEQUENCE `seq_&lt;entity&gt;_&lt;tenantId&gt;`. This service
/// returns the next value. Concurrent callers always get distinct values
/// (no DMax+1 races).
///
/// Implementation in <c>LON.Infrastructure.Services.SqlNumberSequenceService</c>.
/// §E12 may move SEQUENCE creation/seeding behind a tenant-provisioning
/// pipeline; this interface is the stable contract callers rely on.
/// </summary>
public interface INumberSequenceService
{
    /// <summary>
    /// Pull the next sequential value from <c>seq_{entityKey}_{tenantId}</c>.
    /// Returns 1, 2, 3, ... in insertion order. Callers should pass the value
    /// to <see cref="LON.Domain.Common.NumberFormatter"/> to stamp the final
    /// human-readable number.
    /// </summary>
    /// <param name="entityKey">Sequence key, e.g. "ClientOrder", "ImDeclaration".</param>
    /// <param name="tenantId">Tenant guid.</param>
    Task<long> NextAsync(string entityKey, Guid tenantId, CancellationToken cancellationToken = default);
}
