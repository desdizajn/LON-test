# Production Flow

## Overview
Производствен процес од прием на суровина до готов производ и извоз.

## Flow Diagram

```
┌─────────────────────────────────────────────────────────────┐
│ 1. ПРИЕМ НА СУРОВИНА                                         │
│ Receipt → InventoryBalance (Item + Batch + MRN + Location) │
└────────────────────┬────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────────┐
│ 2. КРЕИРАЊЕ ПРОИЗВОДЕН НАЛОГ (LON)                          │
│ ProductionOrder (Draft → Released → InProgress)             │
│   - Item (FG)                                               │
│   - Quantity                                                │
│   - BOM (materials needed)                                  │
│   - Routing (operations)                                    │
└────────────────────┬────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────────┐
│ 3. РЕЗЕРВАЦИЈА МАТЕРИЈАЛ                                     │
│ ProductionOrderMaterials                                    │
│   - RequiredQuantity                                        │
│   - ReservedQuantity                                        │
└────────────────────┬────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────────┐
│ 4. PICK ЗА ПРОИЗВОДСТВО                                      │
│ PickTask → Location (bin level)                            │
└────────────────────┬────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────────┐
│ 5. ISSUE TO WO (MANDATORY BATCH + MRN)                      │
│ MaterialIssue                                               │
│   - ProductionOrderId                                       │
│   - ItemId, BatchNumber, MRN                                │
│   - Quantity                                                │
│                                                             │
│ → Create TraceLink (Material → WO)                         │
│ → Update InventoryBalance (-)                              │
│ → Create InventoryMovement                                 │
└────────────────────┬────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────────┐
│ 6. ПРОИЗВОДСТВО (OPERATIONS)                                 │
│ ProductionOrderOperations                                   │
│   - Start/End time                                          │
│   - WorkCenter, Machine                                     │
│   - Actual time vs Standard time                           │
└────────────────────┬────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────────┐
│ 7. FG RECEIPT (ГЕНЕРИРАЊЕ BATCH)                            │
│ ProductionReceipt                                           │
│   - New Batch Number (Auto: FG-LON-{date}-{seq})           │
│   - Quantity produced                                       │
│   - Scrap quantity                                          │
│   - Quality status (OK/Blocked/Quarantine)                 │
│                                                             │
│ → Create BatchGenealogy (ParentBatches, ParentMRNs)       │
│ → Create TraceLink (WO → FG Batch)                        │
│ → Update InventoryBalance (+)                             │
│ → Update ProductionOrder.ProducedQuantity                 │
└────────────────────┬────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────────┐
│ 8. КВАЛИТЕТ (ако е потребно)                                │
│ Quality inspection → Update QualityStatus                   │
└────────────────────┬────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────────┐
│ 9. ПАКУВАЊЕ И ИСПОРАКА                                      │
│ Shipment → CustomsDeclaration (Export)                     │
│   - Export MRN                                              │
│   - FG Batch traceability                                   │
└────────────────────┬────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────────┐
│ 10. РАЗДОЛЖУВАЊЕ ГАРАНЦИЈА                                  │
│ GuaranteeLedgerEntry (Credit)                              │
│   - Link to Export MRN                                      │
│   - Link to Import MRN (if Inward Processing)              │
└─────────────────────────────────────────────────────────────┘
```

## Detailed Steps

### 1. Receipt (Прием)

**Trigger:** Material arrives from supplier

**Actions:**
1. Create `Receipt` with lines
2. Each line has:
   - ItemId
   - Quantity
   - BatchNumber (from supplier or generated)
   - MRN (from customs declaration)
   - QualityStatus
   - CustomsDeclarationId

3. If Customs procedure requires guarantee:
   - Create `GuaranteeLedgerEntry` (Debit)
   - Link to MRN and CustomsDeclarationId

4. Create `InventoryBalance`:
   - Item + Batch + MRN + Location
   - Initial quantity

### 2. Production Order Creation

**Trigger:** Sales order or forecast

**Actions:**
1. Create `ProductionOrder`:
   - Status = Draft
   - Item (Finished Good)
   - OrderQuantity
   - PlannedStartDate, PlannedEndDate

2. Load BOM:
   - Create `ProductionOrderMaterials` from BOM lines
   - Calculate RequiredQuantity

3. Load Routing:
   - Create `ProductionOrderOperations`

4. Release order:
   - Status = Released
   - Ready for material reservation

### 3. Material Reservation

**Trigger:** Production order released

**Actions:**
1. For each `ProductionOrderMaterial`:
   - Find available inventory (QualityStatus = OK)
   - Update `ReservedQuantity`
   - Flag inventory as reserved

### 4. Pick for Production

**Trigger:** Production ready to start

**Actions:**
1. Create `PickTasks`:
   - Per material line
   - Assign to employee
   - Specify location (bin)

2. Employee picks:
   - Scan item + batch + location
   - Confirm quantity
   - Update PickTask.Status = Completed

### 5. Issue to Work Order

**Trigger:** Material picked, ready to issue

**Actions:**
1. Create `MaterialIssue`:
   - ProductionOrderId
   - ItemId, BatchNumber, MRN
   - Quantity
   - IssuedByEmployeeId

2. Update `ProductionOrderMaterial.IssuedQuantity`

3. **CRITICAL:** Create `TraceLink`:
   - SourceType = "Receipt"
   - SourceId = ReceiptLineId
   - SourceBatchNumber = BatchNumber
   - SourceMRN = MRN
   - TargetType = "ProductionOrder"
   - TargetId = ProductionOrderId
   - Quantity

4. Update `InventoryBalance`:
   - Subtract quantity
   - If quantity = 0, delete balance record

5. Create `InventoryMovement`:
   - Type = ProductionIssue
   - FromLocation → ToLocation (Production floor)

### 6. Production Operations

**Trigger:** Work starts on operation

**Actions:**
1. Start operation:
   - `ProductionOrderOperation.StartDate = Now`
   - Status = InProgress

2. End operation:
   - `ProductionOrderOperation.EndDate = Now`
   - Calculate ActualTimeMinutes
   - IsCompleted = true

3. Track:
   - Employee (optional)
   - Machine
   - Quality notes

### 7. FG Receipt

**Trigger:** Production completed, goods ready

**Actions:**
1. Generate new Batch Number:
   - Format: `FG-{ItemCode}-{Date}-{Seq}`
   - Example: `FG-001-20241228-001`

2. Create `ProductionReceipt`:
   - ProductionOrderId
   - ItemId (Finished Good)
   - BatchNumber (новиот)
   - Quantity
   - ScrapQuantity (if any)
   - QualityStatus
   - LocationId (FG warehouse)

3. **CRITICAL:** Create `BatchGenealogy`:
   - BatchNumber (новиот FG batch)
   - ItemId
   - ProductionOrderId
   - ParentBatches (JSON array од материјал batches)
   - ParentMRNs (JSON array од MRNs)

4. Create `TraceLink`:
   - SourceType = "ProductionOrder"
   - SourceId = ProductionOrderId
   - TargetType = "ProductionReceipt"
   - TargetId = ProductionReceiptId
   - TargetBatchNumber = новиот FG batch
   - Quantity

5. Update `InventoryBalance`:
   - Add FG quantity
   - Item + новиот Batch + Location

6. Update `ProductionOrder`:
   - ProducedQuantity += Quantity
   - If ProducedQuantity >= OrderQuantity:
     - Status = Completed

### 8. Quality Inspection (Optional)

**Actions:**
- Inspect FG batch
- Update `InventoryBalance.QualityStatus`:
  - OK → Available for shipment
  - Blocked → Cannot ship
  - Quarantine → Pending review

### 9. Shipment & Export

**Actions:**
1. Create `Shipment` with lines:
   - ItemId (FG)
   - BatchNumber
   - Quantity
   - CustomerId

2. Create `CustomsDeclaration` (Export):
   - Type = Export
   - MRN (export MRN)
   - Lines with FG items

3. Update `MRNRegistry` (if Inward Processing):
   - UsedQuantity += Quantity
   - RemainingQuantity = TotalQuantity - UsedQuantity

4. Update `InventoryBalance`:
   - Subtract shipped quantity

### 10. Guarantee Release

**Trigger:** Export completed, customs cleared

**Actions:**
1. If original import had guarantee (Inward Processing):
   - Create `GuaranteeLedgerEntry` (Credit)
   - Amount = original debit amount
   - Link to Export MRN
   - Link to original Debit entry
   - IsReleased = true

2. Verify:
   - Guarantee account balance updated
   - Available limit increased

## Traceability Example

**Scenario:** Finished Good FG-001-20241228-001

**Backward Trace:**
```
FG-001-20241228-001
  ← ProductionOrder LON-20241228-ABC
    ← MaterialIssue RM-001, Batch B123, MRN 24MK123456
    ← MaterialIssue RM-002, Batch B456, MRN 24MK789012
```

**Forward Trace:**
```
RM-001, Batch B123, MRN 24MK123456
  → ProductionOrder LON-20241228-ABC
    → FG-001-20241228-001
      → Shipment SHP-20241229-001
        → Export MRN 24MK999888
```

**Customs Query:**
"За Export MRN 24MK999888, кои увозни MRN се користени?"

Answer:
- 24MK123456 (RM-001, Batch B123, Qty 10 kg)
- 24MK789012 (RM-002, Batch B456, Qty 5 kg)

## Key Business Rules

1. **No Issue without Batch** - Задолжителен batch број при издавање
2. **No FG Receipt without TraceLinks** - Мора да има lineage
3. **MRN Tracking** - За Inward Processing задолжително
4. **Guarantee Management** - Debit при увоз, Credit при извоз
5. **Quality Status** - Blocked items не можат да се издаваат
6. **No Negative Stock** - Inventory balance не може да биде негативен
