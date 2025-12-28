# Entity Relationship Diagram

## Core Domain Model

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              MASTER DATA                                     │
└─────────────────────────────────────────────────────────────────────────────┘

    ┌──────────────┐         ┌──────────────┐
    │    Item      │         │     UoM      │
    ├──────────────┤         ├──────────────┤
    │ Id (PK)      │         │ Id (PK)      │
    │ Code         │         │ Code         │
    │ Name         │◄────────┤ Name         │
    │ Type (enum)  │ BaseUoM │ IsActive     │
    │ IsBatchTracked│        └──────────────┘
    │ IsMRNTracked │
    │ HSCode       │         ┌──────────────┐
    │ CountryOfOrigin│       │  Warehouse   │
    │ BaseUoMId (FK)│        ├──────────────┤
    └──────────────┘         │ Id (PK)      │
                             │ Code         │
    ┌──────────────┐         │ Name         │
    │   Partner    │         │ IsActive     │
    ├──────────────┤         └──────┬───────┘
    │ Id (PK)      │                │
    │ Code         │                │
    │ Name         │         ┌──────▼───────┐
    │ Type (enum)  │         │   Location   │
    │ TaxId        │         ├──────────────┤
    │ Country      │         │ Id (PK)      │
    └──────────────┘         │ WarehouseId(FK)│
                             │ Code         │
    ┌──────────────┐         │ Name         │
    │  WorkCenter  │         │ Type (enum)  │
    ├──────────────┤         └──────────────┘
    │ Id (PK)      │
    │ Code         │
    │ Name         │         ┌──────────────┐
    └──────────────┘         │   Employee   │
                             ├──────────────┤
    ┌──────────────┐         │ Id (PK)      │
    │   Machine    │         │ Code         │
    ├──────────────┤         │ FirstName    │
    │ Id (PK)      │         │ LastName     │
    │ Code         │         └──────────────┘
    │ Name         │
    │ WorkCenterId(FK)│
    └──────────────┘


┌─────────────────────────────────────────────────────────────────────────────┐
│                                  WMS                                         │
└─────────────────────────────────────────────────────────────────────────────┘

    ┌──────────────────┐
    │     Receipt      │
    ├──────────────────┤
    │ Id (PK)          │
    │ ReceiptNumber    │
    │ ReceiptDate      │
    │ PartnerId (FK)   │───┐
    │ WarehouseId (FK) │   │
    └────────┬─────────┘   │
             │             │
             │ 1           │
             │             │
             │ N           │
             ▼             │
    ┌──────────────────┐   │
    │   ReceiptLine    │   │
    ├──────────────────┤   │
    │ Id (PK)          │   │
    │ ReceiptId (FK)   │   │
    │ ItemId (FK) ─────┼───┤
    │ Quantity         │   │
    │ UoMId (FK)       │   │
    │ BatchNumber *    │   │
    │ MRN *            │   │     * = Important for Traceability
    │ QualityStatus    │   │
    │ CustomsDeclarationId(FK)│
    └──────────┬───────┘   │
               │           │
               └───────────┼─────────┐
                           │         │
                           ▼         ▼
    ┌──────────────────────────────────────┐
    │       InventoryBalance               │
    ├──────────────────────────────────────┤
    │ Id (PK)                              │
    │ ItemId (FK) ─────────────────────────┤───┐
    │ LocationId (FK)                      │   │
    │ BatchNumber *                        │   │
    │ MRN *                                │   │
    │ Quantity                             │   │
    │ UoMId (FK)                           │   │
    │ QualityStatus (enum)                 │   │
    │ IsReserved                           │   │
    │ UQ: ItemId+LocationId+Batch+MRN      │   │
    └──────────────────────────────────────┘   │
                           ▲                    │
                           │                    │
    ┌──────────────────────┴──────────┐         │
    │    InventoryMovement             │         │
    ├──────────────────────────────────┤         │
    │ Id (PK)                          │         │
    │ MovementDate                     │         │
    │ ItemId (FK) ─────────────────────┼─────────┘
    │ FromLocationId (FK) nullable     │
    │ ToLocationId (FK) nullable       │
    │ Quantity                         │
    │ UoMId (FK)                       │
    │ BatchNumber                      │
    │ MRN                              │
    │ MovementType (enum)              │
    │ ReferenceType (string)           │
    │ ReferenceId (guid)               │
    └──────────────────────────────────┘

    ┌──────────────────┐
    │    Shipment      │
    ├──────────────────┤
    │ Id (PK)          │
    │ ShipmentNumber   │
    │ ShipmentDate     │
    │ CustomerId (FK)  │
    └────────┬─────────┘
             │
             │ 1
             │
             │ N
             ▼
    ┌──────────────────┐
    │  ShipmentLine    │
    ├──────────────────┤
    │ Id (PK)          │
    │ ShipmentId (FK)  │
    │ ItemId (FK)      │
    │ Quantity         │
    │ BatchNumber      │
    │ CustomsDeclarationId(FK)│
    └──────────────────┘


┌─────────────────────────────────────────────────────────────────────────────┐
│                            PRODUCTION                                        │
└─────────────────────────────────────────────────────────────────────────────┘

    ┌──────────────────┐
    │      BOM         │
    ├──────────────────┤
    │ Id (PK)          │
    │ ItemId (FK)      │────┐
    │ Version          │    │
    │ IsActive         │    │
    └────────┬─────────┘    │
             │              │
             │ 1            │
             │              │
             │ N            │
             ▼              │
    ┌──────────────────┐    │
    │    BOMLine       │    │
    ├──────────────────┤    │
    │ Id (PK)          │    │
    │ BOMId (FK)       │    │
    │ ItemId (FK) ─────┼────┤
    │ Quantity         │    │
    │ UoMId (FK)       │    │
    └──────────────────┘    │
                            │
    ┌──────────────────┐    │
    │    Routing       │    │
    ├──────────────────┤    │
    │ Id (PK)          │    │
    │ ItemId (FK) ─────┼────┘
    │ Version          │
    │ IsActive         │
    └────────┬─────────┘
             │
             │ 1
             │
             │ N
             ▼
    ┌──────────────────┐
    │  RoutingLine     │
    ├──────────────────┤
    │ Id (PK)          │
    │ RoutingId (FK)   │
    │ OperationName    │
    │ WorkCenterId(FK) │
    │ StandardMinutes  │
    └──────────────────┘


    ┌─────────────────────────────┐
    │     ProductionOrder         │
    ├─────────────────────────────┤
    │ Id (PK)                     │
    │ OrderNumber                 │
    │ ItemId (FK) ────────────────┼───┐
    │ OrderQuantity               │   │
    │ ProducedQuantity            │   │
    │ ScrapQuantity               │   │
    │ Status (enum)               │   │
    │ PlannedStartDate            │   │
    │ PlannedEndDate              │   │
    │ ActualStartDate             │   │
    │ ActualEndDate               │   │
    │ BOMId (FK)                  │   │
    │ RoutingId (FK)              │   │
    └────────┬────────────────────┘   │
             │                        │
             ├──┐ 1                   │
             │  │                     │
             │  │ N                   │
             │  ▼                     │
             │ ┌────────────────────────────┐
             │ │ ProductionOrderMaterial    │
             │ ├────────────────────────────┤
             │ │ Id (PK)                    │
             │ │ ProductionOrderId (FK)     │
             │ │ ItemId (FK) ───────────────┼───┤
             │ │ RequiredQuantity           │   │
             │ │ IssuedQuantity             │   │
             │ │ ReservedQuantity           │   │
             │ └────────────────────────────┘   │
             │                                  │
             │                                  │
             ├──┐ 1                             │
             │  │                               │
             │  │ N                             │
             │  ▼                               │
             │ ┌────────────────────────────┐   │
             │ │ ProductionOrderOperation   │   │
             │ ├────────────────────────────┤   │
             │ │ Id (PK)                    │   │
             │ │ ProductionOrderId (FK)     │   │
             │ │ OperationName              │   │
             │ │ WorkCenterId (FK)          │   │
             │ │ StandardMinutes            │   │
             │ │ ActualMinutes              │   │
             │ │ StartDate                  │   │
             │ │ EndDate                    │   │
             │ │ IsCompleted                │   │
             │ └────────────────────────────┘   │
             │                                  │
             │                                  │
             ▼                                  │
    ┌──────────────────────┐                   │
    │   MaterialIssue      │                   │
    ├──────────────────────┤                   │
    │ Id (PK)              │                   │
    │ IssueNumber          │                   │
    │ ProductionOrderId(FK)│                   │
    │ ItemId (FK) ─────────┼───────────────────┤
    │ Quantity             │                   │
    │ UoMId (FK)           │                   │
    │ BatchNumber *        │                   │
    │ MRN *                │                   │
    │ IssuedByEmployeeId   │                   │
    └──────────────────────┘                   │
                                               │
    ┌──────────────────────┐                   │
    │  ProductionReceipt   │                   │
    ├──────────────────────┤                   │
    │ Id (PK)              │                   │
    │ ReceiptNumber        │                   │
    │ ProductionOrderId(FK)│                   │
    │ ItemId (FK) ─────────┼───────────────────┘
    │ Quantity             │
    │ UoMId (FK)           │
    │ BatchNumber *        │   ← NEW batch (FG)
    │ ScrapQuantity        │
    │ QualityStatus        │
    │ LocationId (FK)      │
    │ ReceivedByEmployeeId │
    └──────────────────────┘


┌─────────────────────────────────────────────────────────────────────────────┐
│                           CUSTOMS & TRADE                                    │
└─────────────────────────────────────────────────────────────────────────────┘

    ┌─────────────────────────────┐
    │    CustomsProcedure         │
    ├─────────────────────────────┤
    │ Id (PK)                     │
    │ Code                        │
    │ Name                        │
    │ Type (enum)                 │
    │ RequiresGuarantee           │
    │ GuaranteePercentage         │
    │ DueDays                     │
    │ RequiresMRNTracking         │
    │ AllowsProduction            │
    │ AllowsExport                │
    └─────────┬───────────────────┘
              │
              │ 1
              │
              │ N
              ▼
    ┌─────────────────────────────┐
    │   CustomsDeclaration        │
    ├─────────────────────────────┤
    │ Id (PK)                     │
    │ DeclarationNumber           │
    │ MRN *                       │
    │ DeclarationDate             │
    │ CustomsProcedureId (FK)     │
    │ PartnerId (FK)              │
    │ TotalCustomsValue           │
    │ Currency                    │
    │ TotalDuty                   │
    │ TotalVAT                    │
    │ IsCleared                   │
    │ ClearanceDate               │
    │ DueDate                     │
    └────────┬────────────────────┘
             │
             ├──┐ 1
             │  │
             │  │ N
             │  ▼
             │ ┌─────────────────────────────┐
             │ │ CustomsDeclarationLine      │
             │ ├─────────────────────────────┤
             │ │ Id (PK)                     │
             │ │ CustomsDeclarationId (FK)   │
             │ │ ItemId (FK)                 │
             │ │ HSCode                      │
             │ │ Quantity                    │
             │ │ UoMId (FK)                  │
             │ │ CustomsValue                │
             │ │ CountryOfOrigin             │
             │ │ DutyRate                    │
             │ │ DutyAmount                  │
             │ │ VATRate                     │
             │ │ VATAmount                   │
             │ └─────────────────────────────┘
             │
             │
             ▼
    ┌─────────────────────────────┐
    │      MRNRegistry            │
    ├─────────────────────────────┤
    │ Id (PK)                     │
    │ MRN * (Unique)              │
    │ CustomsDeclarationId (FK)   │
    │ RegistrationDate            │
    │ TotalQuantity               │
    │ UsedQuantity                │
    │ RemainingQuantity (Computed)│
    │ IsFullyUsed (Computed)      │
    │ ExpiryDate                  │
    │ IsActive                    │
    └─────────────────────────────┘


┌─────────────────────────────────────────────────────────────────────────────┐
│                       GUARANTEE MANAGEMENT                                   │
└─────────────────────────────────────────────────────────────────────────────┘

    ┌─────────────────────────────┐
    │    GuaranteeAccount         │
    ├─────────────────────────────┤
    │ Id (PK)                     │
    │ AccountNumber               │
    │ AccountName                 │
    │ Currency                    │
    │ TotalLimit                  │
    │ BankId (FK) → Partner       │
    │ BankAccountNumber           │
    │ ContractDate                │
    │ ExpiryDate                  │
    │ IsActive                    │
    └────────┬────────────────────┘
             │
             │ 1
             │
             │ N
             ▼
    ┌─────────────────────────────┐
    │   GuaranteeLedgerEntry      │
    ├─────────────────────────────┤
    │ Id (PK)                     │
    │ GuaranteeAccountId (FK)     │
    │ EntryDate                   │
    │ EntryType (Debit/Credit)    │   ← Ledger Pattern!
    │ Amount                      │
    │ Currency                    │
    │ Description                 │
    │ MRN                         │
    │ CustomsDeclarationId (FK)   │
    │ ExpectedReleaseDate         │
    │ ActualReleaseDate           │
    │ IsReleased                  │
    └─────────────────────────────┘

    Balance = SUM(IF EntryType=Debit THEN Amount ELSE -Amount)


    ┌─────────────────────────────┐
    │     DutyCalculation         │
    ├─────────────────────────────┤
    │ Id (PK)                     │
    │ CustomsDeclarationId (FK)   │
    │ HSCode                      │
    │ CustomsValue                │
    │ DutyRate                    │
    │ DutyAmount                  │
    │ VATRate                     │
    │ VATAmount                   │
    │ OtherCharges                │
    │ TotalAmount                 │
    │ SnapshotDate                │   ← Historical snapshot
    └─────────────────────────────┘


┌─────────────────────────────────────────────────────────────────────────────┐
│                          TRACEABILITY                                        │
└─────────────────────────────────────────────────────────────────────────────┘

    ┌─────────────────────────────┐
    │       TraceLink             │
    ├─────────────────────────────┤
    │ Id (PK)                     │
    │ LinkDate                    │
    │ SourceType (string)         │   ← "Receipt", "ProductionOrder", etc.
    │ SourceId (guid)             │
    │ SourceBatchNumber           │
    │ SourceMRN                   │
    │ TargetType (string)         │
    │ TargetId (guid)             │
    │ TargetBatchNumber           │
    │ TargetMRN                   │
    │ ItemId (FK)                 │
    │ Quantity                    │
    └─────────────────────────────┘

    Example Links:
    1. Receipt → ProductionOrder
       Source: Receipt (ItemId=RM-001, Batch=B123, MRN=24MK123456)
       Target: ProductionOrder (LON-20241228-ABC)

    2. ProductionOrder → ProductionReceipt
       Source: ProductionOrder (LON-20241228-ABC)
       Target: ProductionReceipt (ItemId=FG-001, Batch=FG-001-20241228-001)


    ┌─────────────────────────────┐
    │     BatchGenealogy          │
    ├─────────────────────────────┤
    │ Id (PK)                     │
    │ BatchNumber (Unique)        │   ← FG batch
    │ ItemId (FK)                 │
    │ CreatedDate                 │
    │ ProductionOrderId (FK)      │
    │ ParentBatches (JSON)        │   ← Array of raw batches
    │ ParentMRNs (JSON)           │   ← Array of import MRNs
    └─────────────────────────────┘

    Example:
    BatchNumber: "FG-001-20241228-001"
    ParentBatches: ["B-2024-1234", "B-2024-5678"]
    ParentMRNs: ["24MK123456789012345", "24MK987654321098765"]


┌─────────────────────────────────────────────────────────────────────────────┐
│                          EVENT SYSTEM                                        │
└─────────────────────────────────────────────────────────────────────────────┘

    ┌─────────────────────────────┐
    │      OutboxMessage          │
    ├─────────────────────────────┤
    │ Id (PK)                     │
    │ Type (string)               │   ← Event type name
    │ Content (JSON)              │   ← Serialized event
    │ OccurredOnUtc               │
    │ ProcessedOnUtc (nullable)   │
    │ Error (nullable)            │
    └─────────────────────────────┘

    Worker processes every 10 seconds:
    - Fetch unprocessed messages (ProcessedOnUtc IS NULL)
    - Deserialize and handle
    - Update ProcessedOnUtc or Error


┌─────────────────────────────────────────────────────────────────────────────┐
│                              RELATIONSHIPS SUMMARY                           │
└─────────────────────────────────────────────────────────────────────────────┘

Key Foreign Key Relationships:

Item ──┬─► ReceiptLine
       ├─► InventoryBalance
       ├─► MaterialIssue
       ├─► ProductionReceipt
       ├─► CustomsDeclarationLine
       └─► TraceLink

ProductionOrder ──┬─► ProductionOrderMaterial
                  ├─► ProductionOrderOperation
                  ├─► MaterialIssue
                  ├─► ProductionReceipt
                  └─► TraceLink

CustomsDeclaration ──┬─► CustomsDeclarationLine
                     ├─► ReceiptLine
                     ├─► GuaranteeLedgerEntry
                     └─► MRNRegistry

GuaranteeAccount ──► GuaranteeLedgerEntry

Location ──► InventoryBalance

Partner ──┬─► Receipt
          ├─► Shipment
          └─► CustomsDeclaration


Key Indexes:

- InventoryBalance: UNIQUE(ItemId, LocationId, BatchNumber, MRN)
- MRNRegistry: UNIQUE(MRN)
- TraceLink: INDEX(SourceBatchNumber, SourceMRN), INDEX(TargetBatchNumber, TargetMRN)
- GuaranteeLedgerEntry: INDEX(MRN), INDEX(IsReleased)
- OutboxMessage: INDEX(ProcessedOnUtc)
```

## Critical Business Rules Enforced by Schema

1. **Batch + MRN Uniqueness**
   - `InventoryBalance` has UNIQUE constraint on (ItemId, LocationId, BatchNumber, MRN)
   - Prevents duplicate balances

2. **MRN Uniqueness**
   - `MRNRegistry` has UNIQUE constraint on MRN
   - Each MRN can only exist once

3. **Ledger-Based Guarantee**
   - NO balance column on `GuaranteeAccount`
   - Balance calculated from `GuaranteeLedgerEntry` sum

4. **Soft Delete**
   - All entities have `IsDeleted` flag
   - Query filters exclude deleted records

5. **Audit Trail**
   - All entities have: CreatedAt, CreatedBy, ModifiedAt, ModifiedBy

6. **Quality Status**
   - Only items with QualityStatus = OK can be issued

7. **Traceability**
   - `TraceLink` provides forward/backward navigation
   - `BatchGenealogy` stores full lineage
