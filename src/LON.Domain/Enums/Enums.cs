namespace LON.Domain.Enums;

public enum ItemType
{
    RawMaterial = 1,
    SemiFinished = 2,
    FinishedGood = 3,
    Packaging = 4
}

public enum QualityStatus
{
    None = 0,
    OK = 1,
    Blocked = 2,
    Quarantine = 3
}

public enum LocationType
{
    Receiving = 1,
    Storage = 2,
    Picking = 3,
    Production = 4,
    Shipping = 5,
    Quarantine = 6,
    Blocked = 7
}

public enum ProductionOrderStatus
{
    Draft = 1,
    Released = 2,
    InProgress = 3,
    Completed = 4,
    Closed = 5,
    Cancelled = 6
}

public enum CustomsProcedureType
{
    LocalPurchase = 1,
    TemporaryImport = 2,
    InwardProcessing = 3,
    FinalClearance = 4,
    Export = 5
}

public enum GuaranteeEntryType
{
    Debit = 1,
    Credit = 2
}

public enum DocumentType
{
    CustomsDeclaration = 1,
    CommercialInvoice = 2,
    PackingList = 3,
    CMR = 4,
    BillOfLading = 5,
    AirWaybill = 6,
    Certificate = 7
}

public enum PickTaskStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public enum ShipmentStatus
{
    Draft = 1,
    Planned = 2,
    Picked = 3,
    Packed = 4,
    Shipped = 5,
    Delivered = 6,
    Cancelled = 7
}

public enum MovementType
{
    Receipt = 1,
    Issue = 2,
    Transfer = 3,
    Adjustment = 4,
    ProductionReceipt = 5,
    ProductionIssue = 6,
    Shipment = 7,
    Return = 8
}

public enum CycleCountStatus
{
    Planned = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public enum PartnerType
{
    Supplier = 1,
    Customer = 2,
    Carrier = 3,
    CustomsBroker = 4,
    Bank = 5
}

// LON (Inward Processing) енумерации
public enum LONAuthorizationStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Expired = 5,
    Suspended = 6
}

public enum LONSystemType
{
    ОдложеноПлаќање = 1,      // Suspension system (42 00)
    ВраќањеДавачки = 2         // Drawback system (51 00)
}

public enum LONOperationType
{
    Обработка = 1,
    Преработка = 2,
    Склопување = 3,
    Поправка = 4
}

/// <summary>
/// Lifecycle states for <see cref="Entities.Customs.CustomsDeclaration"/>.
/// Draft: user started but hasn't registered an MRN yet.
/// Registered: MRN assigned (auto-generated or pasted from customs portal).
/// Submitted: declaration XML delivered to customs (PEE010) — future Phase 4.2.
/// Cleared: customs accepted; goods released. Mirrors legacy IsCleared=true.
/// Cancelled: terminal state for withdrawn declarations.
/// </summary>
public enum DeclarationStatus
{
    Draft = 0,
    Registered = 1,
    Submitted = 2,
    Cleared = 3,
    Cancelled = 99
}

/// <summary>
/// Business state for a parcel of LON material (legacy `LagerMaterijali.Proces`).
/// The numeric values mirror legacy so reports / XML (PEE060) stay compatible.
///   1 = Imported / distributed (receipt booked)
///   6 = In production (issued to production order)
///   7 = Exported (consumed against EX declaration)
///   8 = Final domestic import (converted from LON suspension to regular import)
///   9 = Waste (written off to waste slot)
/// </summary>
public enum LonProcessState
{
    Imported = 1,
    InProduction = 6,
    Exported = 7,
    FinalImport = 8,
    Waste = 9
}

/// <summary>P5.1 — generic importer source format.</summary>
public enum ImportSourceFormat
{
    Xlsx = 1,
    Csv = 2,
    Tsv = 3,
    Json = 4,
    Xml = 5
}

/// <summary>P5.1 — generic importer session lifecycle.</summary>
public enum ImportSessionStatus
{
    Uploaded = 1,
    Mapped = 2,
    Committed = 3,
    Failed = 4
}
