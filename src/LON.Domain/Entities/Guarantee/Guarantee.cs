using LON.Domain.Common;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;

namespace LON.Domain.Entities.Guarantee;

public class GuaranteeAccount : BaseEntity
{
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

public class GuaranteeLedgerEntry : BaseEntity
{
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

public class DutyCalculation : BaseEntity
{
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
