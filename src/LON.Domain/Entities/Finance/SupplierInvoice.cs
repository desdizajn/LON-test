using LON.Domain.Common;
using LON.Domain.Entities.MasterData;

namespace LON.Domain.Entities.Finance;

/// <summary>
/// P16.C3.c — accounts-payable register: invoices we owe to suppliers.
/// Replaces the localStorage-only persistence used by
/// <c>pages/Finance/SupplierInvoices.tsx</c>.
///
/// The stored <see cref="Status"/> is one of Open / Paid / Cancelled.
/// "Overdue" is **derived**, not stored — the read query projects it
/// when Status=Open AND DueDate &lt; today.
/// </summary>
public class SupplierInvoice : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Supplier-facing invoice number.</summary>
    public string Number { get; set; } = string.Empty;

    public Guid SupplierPartnerId { get; set; }
    public virtual Partner SupplierPartner { get; set; } = null!;

    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";

    public SupplierInvoiceStatus Status { get; set; } = SupplierInvoiceStatus.Open;

    public DateTime? PaidDate { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Persisted set. Note "Overdue" is intentionally absent — it's derived
/// from Status=Open + DueDate &lt; today.
/// </summary>
public enum SupplierInvoiceStatus
{
    Open = 1,
    Paid = 2,
    Cancelled = 3,
}
