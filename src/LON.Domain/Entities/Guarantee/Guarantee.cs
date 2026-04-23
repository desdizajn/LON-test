using LON.Domain.Common;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;

namespace LON.Domain.Entities.Guarantee;

public class GuaranteeAccount : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public Guid? BankPartnerId { get; set; }
    public virtual Partner? BankPartner { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal TotalLimit { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public virtual ICollection<GuaranteeLedgerEntry> LedgerEntries { get; set; } = new List<GuaranteeLedgerEntry>();
    
    public decimal GetCurrentBalance()
    {
        return LedgerEntries
            .Where(e => !e.IsDeleted)
            .Sum(e => e.EntryType == GuaranteeEntryType.Debit ? e.Amount : -e.Amount);
    }
    
    public decimal GetAvailableLimit()
    {
        return TotalLimit - GetCurrentBalance();
    }
}

public class GuaranteeLedgerEntry : BaseEntity, ITenantScoped, IAuditable
{
    public Guid TenantId { get; set; }
    public Guid GuaranteeAccountId { get; set; }
    public virtual GuaranteeAccount GuaranteeAccount { get; set; } = null!;
    public DateTime EntryDate { get; set; }
    public GuaranteeEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Description { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? MRN { get; set; }
    public Guid? CustomsDeclarationId { get; set; }
    public DateTime? ExpectedReleaseDate { get; set; }
    public DateTime? ActualReleaseDate { get; set; }
    public bool IsReleased { get; set; }
    public string? Notes { get; set; }
}

public class DutyCalculation : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? CustomsDeclarationId { get; set; }
    public Guid? ItemId { get; set; }
    public virtual Item? Item { get; set; }
    public string? HSCode { get; set; }
    public decimal CustomsValue { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal DutyRate { get; set; }
    public decimal DutyAmount { get; set; }
    public decimal VATRate { get; set; }
    public decimal VATAmount { get; set; }
    public decimal OtherCharges { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CalculationDate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// P15.5 — legacy <c>tblSostojbaNaGarancija</c>. Point-in-time snapshot of
/// a <see cref="GuaranteeAccount"/>'s ledger-derived balance. Taken at
/// month-end (or on-demand via admin trigger) so audit/reporting has an
/// attested state even if the ledger is later amended (corrections,
/// reversed Zaverkas etc.).
///
/// <para>Balance at snapshot time is stored directly on the row:</para>
/// <list type="bullet">
///   <item><c>DebitedAmount</c> = Σ non-released Debit ledger entries at date.</item>
///   <item><c>CreditedAmount</c> = Σ Credit ledger entries at date.</item>
///   <item><c>NetBalance</c> = <c>DebitedAmount − CreditedAmount</c>.</item>
///   <item><c>AvailableLimit</c> = <c>TotalLimit − NetBalance</c>.</item>
///   <item><c>ActiveDebitCount</c> = count of non-released Debit entries at date (for
///         traffic-light trend charts).</item>
/// </list>
/// </summary>
public class GuaranteeBalanceSnapshot : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid GuaranteeAccountId { get; set; }
    public virtual GuaranteeAccount GuaranteeAccount { get; set; } = null!;

    /// <summary>Inclusive date the snapshot covers. Typically end-of-month.</summary>
    public DateTime SnapshotDate { get; set; }

    public string Currency { get; set; } = "EUR";
    public decimal TotalLimit { get; set; }
    public decimal DebitedAmount { get; set; }
    public decimal CreditedAmount { get; set; }
    public decimal NetBalance { get; set; }
    public decimal AvailableLimit { get; set; }
    public int ActiveDebitCount { get; set; }

    /// <summary>Free-text note: who triggered, purpose (month-end, ad-hoc), gazette ref...</summary>
    public string? Notes { get; set; }
}
