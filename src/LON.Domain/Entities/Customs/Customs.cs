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
    // ===== Основни податоци =====
    public string DeclarationNumber { get; set; } = string.Empty;
    public string MRN { get; set; } = string.Empty;
    public DateTime DeclarationDate { get; set; }
    
    // ===== Врски =====
    public Guid CustomsProcedureId { get; set; }
    public virtual CustomsProcedure CustomsProcedure { get; set; } = null!;
    public Guid? PartnerId { get; set; }
    public virtual Partner? Partner { get; set; }
    
    /// <summary>
    /// LON Одобрение (за процедури 42 00, 51 00)
    /// </summary>
    public Guid? LONAuthorizationId { get; set; }
    public virtual LONAuthorization? LONAuthorization { get; set; }
    
    // ===== Box 01 - Декларација =====
    /// <summary>
    /// Box 01: Вид на декларација (IM - увоз, EX - извоз, ...)
    /// </summary>
    public string DeclarationType { get; set; } = "IM";
    
    // ===== Box 02 - Испраќач/Извозник =====
    /// <summary>
    /// Box 02: Име на испраќач/извозник
    /// </summary>
    public string? SenderName { get; set; }
    public string? SenderAddress { get; set; }
    public string? SenderCountry { get; set; }
    
    // ===== Box 08 - Примач =====
    /// <summary>
    /// Box 08: Име на примач (имател на одобрение за LON)
    /// </summary>
    public string? ReceiverName { get; set; }
    public string? ReceiverAddress { get; set; }
    public string? ReceiverTaxId { get; set; }
    
    // ===== Box 15 - Земја на испраќање/извоз =====
    public string? CountryOfDispatch { get; set; }
    
    // ===== Box 17 - Земја на дестинација =====
    public string? CountryOfDestination { get; set; }
    
    // ===== Box 18 - Ознака и број на превозно средство =====
    public string? TransportIdentification { get; set; }
    
    // ===== Box 19 - Контејнер =====
    public bool HasContainer { get; set; }
    
    // ===== Box 20 - Услови на испорака =====
    public string? DeliveryTerms { get; set; } // Incoterms
    
    // ===== Box 21 - Ознака и број на активното превозно средство =====
    public string? ActiveTransportIdentification { get; set; }
    
    // ===== Box 22 - Валута и вкупен фактуриран износ =====
    public string Currency { get; set; } = "EUR";
    public decimal TotalInvoiceAmount { get; set; }
    
    // ===== Box 23 - Девизен курс =====
    public decimal? ExchangeRate { get; set; }
    
    // ===== Box 25 - Начин на транспорт =====
    public string? TransportMode { get; set; } // 1-Поморски, 3-Друмски, 4-Воздушен...
    
    // ===== Box 30 - Место на стока =====
    public string? LocationOfGoods { get; set; }
    
    // ===== Box 31 - Колети и опис на стока =====
    public int? TotalPackages { get; set; }
    public string? PackageDescription { get; set; }
    
    // ===== Box 37 - Процедура =====
    /// <summary>
    /// Box 37: Процедурен код (42 00 - LON одложено, 51 00 - LON враќање)
    /// </summary>
    public string ProcedureCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Box 37: Претходна процедура (за повторен извоз)
    /// </summary>
    public string? PreviousProcedureCode { get; set; }
    
    // ===== Царински износи =====
    public decimal TotalCustomsValue { get; set; }
    public decimal TotalDuty { get; set; }
    public decimal TotalVAT { get; set; }
    public decimal TotalOtherCharges { get; set; }
    
    // ===== Статус =====
    public DateTime? DueDate { get; set; }
    public DateTime? ClearedDate { get; set; }
    public bool IsCleared { get; set; }
    
    // ===== Box 44 - Посебни напомени / Документи =====
    public string? SpecialRemarks { get; set; }
    
    public string? Notes { get; set; }
    
    // ===== Колекции =====
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
    
    // ===== Box 31 - Колети и опис =====
    public string? PackageMarks { get; set; }
    public int? NumberOfPackages { get; set; }
    public string? PackageType { get; set; } // BX, CT, PK...
    
    // ===== Box 32 - Реден број =====
    // LineNumber covers this
    
    // ===== Box 33 - Ознака на стока =====
    /// <summary>
    /// Box 33: Тарифна ознака (commodity code) - 10 цифри
    /// </summary>
    public string? TariffCode { get; set; }
    
    /// <summary>
    /// TARIC додаток (2 дополнителни цифри)
    /// </summary>
    public string? TARICSuffix { get; set; }
    
    /// <summary>
    /// Националтен додаток (2 дополнителни цифри)
    /// </summary>
    public string? NationalSuffix { get; set; }
    
    // ===== Box 34 - Код на земја на потекло =====
    public string? CountryOfOrigin { get; set; }
    
    // ===== Box 35 - Бруто маса =====
    public decimal? GrossWeight { get; set; }
    
    // ===== Box 38 - Нето маса =====
    public decimal? NetWeight { get; set; }
    
    // ===== Box 41 - Дополнителна единица =====
    public decimal Quantity { get; set; }
    public Guid UoMId { get; set; }
    public virtual UnitOfMeasure UoM { get; set; } = null!;
    
    // ===== Box 42 - Цена на стока =====
    public decimal ItemPrice { get; set; }
    
    // ===== Box 45 - Стапка за корекција =====
    public decimal? AdjustmentRate { get; set; }
    
    // ===== Box 46 - Статистичка вредност =====
    public decimal StatisticalValue { get; set; }
    
    // ===== Box 47 - Начин на пресметка =====
    public string? CalculationMethod { get; set; }
    
    // ===== Царинска вредност и давачки =====
    public decimal CustomsValue { get; set; }
    public decimal DutyRate { get; set; }
    public decimal DutyAmount { get; set; }
    public decimal VATRate { get; set; }
    public decimal VATAmount { get; set; }
    public decimal OtherCharges { get; set; }
    
    // ===== LON специфики =====
    /// <summary>
    /// Врска кон претходна увозна декларација (за повторен извоз)
    /// </summary>
    public string? PreviousMRN { get; set; }
    
    /// <summary>
    /// Употребено количество од претходна декларација
    /// </summary>
    public decimal? UsedQuantityFromPrevious { get; set; }
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
