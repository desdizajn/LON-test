using LON.Domain.Common;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;

namespace LON.Domain.Entities.Customs;

public class CustomsProcedure : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CustomsProcedureType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool RequiresGuarantee { get; set; }
    public decimal GuaranteePercentage { get; set; }
    public int? DueDays { get; set; }
    public bool RequiresMRNTracking { get; set; }
    public bool AllowsProduction { get; set; }
    public bool AllowsExport { get; set; }
    public bool IsActive { get; set; }
    public virtual ICollection<CustomsProcedureDocument> RequiredDocuments { get; set; } = new List<CustomsProcedureDocument>();
}

public class CustomsProcedureDocument : BaseEntity
{
    public Guid CustomsProcedureId { get; set; }
    public virtual CustomsProcedure CustomsProcedure { get; set; } = null!;
    public DocumentType DocumentType { get; set; }
    public bool IsMandatory { get; set; }
}

public class CustomsDeclaration : BaseEntity
{
    public string DeclarationNumber { get; set; } = string.Empty;
    public string MRN { get; set; } = string.Empty;
    public DateTime DeclarationDate { get; set; }
    public Guid CustomsProcedureId { get; set; }
    public virtual CustomsProcedure CustomsProcedure { get; set; } = null!;
    public Guid? PartnerId { get; set; }
    public virtual Partner? Partner { get; set; }
    public decimal TotalCustomsValue { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal TotalDuty { get; set; }
    public decimal TotalVAT { get; set; }
    public decimal TotalOtherCharges { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ClearedDate { get; set; }
    public bool IsCleared { get; set; }
    public string? Notes { get; set; }
    public virtual ICollection<CustomsDeclarationLine> Lines { get; set; } = new List<CustomsDeclarationLine>();
    public virtual ICollection<CustomsDocument> Documents { get; set; } = new List<CustomsDocument>();
}

public class CustomsDeclarationLine : BaseEntity
{
    public Guid CustomsDeclarationId { get; set; }
    public virtual CustomsDeclaration CustomsDeclaration { get; set; } = null!;
    public int LineNumber { get; set; }
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public string? HSCode { get; set; }
    public decimal Quantity { get; set; }
    public Guid UoMId { get; set; }
    public virtual UnitOfMeasure UoM { get; set; } = null!;
    public decimal CustomsValue { get; set; }
    public string? CountryOfOrigin { get; set; }
    public decimal DutyRate { get; set; }
    public decimal DutyAmount { get; set; }
    public decimal VATRate { get; set; }
    public decimal VATAmount { get; set; }
    public decimal OtherCharges { get; set; }
}

public class CustomsDocument : BaseEntity
{
    public Guid CustomsDeclarationId { get; set; }
    public virtual CustomsDeclaration CustomsDeclaration { get; set; } = null!;
    public DocumentType DocumentType { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
    public string? FilePath { get; set; }
    public string? Notes { get; set; }
}

public class MRNRegistry : BaseEntity
{
    public string MRN { get; set; } = string.Empty;
    public Guid? CustomsDeclarationId { get; set; }
    public virtual CustomsDeclaration? CustomsDeclaration { get; set; }
    public DateTime RegistrationDate { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal UsedQuantity { get; set; }
    public decimal RemainingQuantity => TotalQuantity - UsedQuantity;
    public bool IsFullyUsed => RemainingQuantity <= 0;
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}
