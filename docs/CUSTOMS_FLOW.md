# Customs & Guarantee Flow

## Overview
Управување со царински процедури и гаранции (ledger-based).

## Customs Procedure Types

### 1. Local Purchase (Локална набавка)
- **RequiresGuarantee:** NO
- **RequiresMRNTracking:** NO
- **Use Case:** Набавка од локален добавувач

### 2. Temporary Import (Привремен увоз)
- **RequiresGuarantee:** YES (100% од duty)
- **RequiresMRNTracking:** YES
- **DueDays:** 365
- **Use Case:** Увоз за изложба, тестирање

### 3. Inward Processing (Облагородување)
- **RequiresGuarantee:** YES (50% од duty)
- **RequiresMRNTracking:** YES
- **DueDays:** 180
- **Use Case:** Увоз за преработка и реекспорт
- **Most Common** во производство

### 4. Final Clearance (Дефинитивно царинење)
- **RequiresGuarantee:** NO
- **RequiresMRNTracking:** YES
- **Use Case:** Дефинитивен увоз со плаќање duty

### 5. Export (Извоз)
- **RequiresGuarantee:** NO
- **RequiresMRNTracking:** YES
- **Use Case:** Извоз на готов производ

## Flow Diagram

```
┌──────────────────────────────────────────────────────────┐
│ 1. УВОЗ СУРОВИНА (INWARD PROCESSING)                     │
│ Customs Declaration → MRN                                │
└────────────────┬─────────────────────────────────────────┘
                 ↓
┌──────────────────────────────────────────────────────────┐
│ 2. DUTY CALCULATION                                      │
│ HS Code → Duty % → Amount                                │
└────────────────┬─────────────────────────────────────────┘
                 ↓
┌──────────────────────────────────────────────────────────┐
│ 3. GUARANTEE DEBIT (50% за Inward Processing)           │
│ GuaranteeLedgerEntry (Debit)                            │
│   - Amount = CustomsValue * DutyRate * 0.5              │
│   - MRN                                                  │
│   - ExpectedReleaseDate (180 days)                      │
└────────────────┬─────────────────────────────────────────┘
                 ↓
┌──────────────────────────────────────────────────────────┐
│ 4. MRN REGISTRY                                          │
│ MRNRegistry                                              │
│   - MRN                                                  │
│   - TotalQuantity                                        │
│   - UsedQuantity = 0                                     │
│   - RemainingQuantity = TotalQuantity                   │
└────────────────┬─────────────────────────────────────────┘
                 ↓
┌──────────────────────────────────────────────────────────┐
│ 5. ПРИЕМ НА МАТЕРИЈАЛ                                    │
│ Receipt → InventoryBalance (+ MRN linkage)              │
└────────────────┬─────────────────────────────────────────┘
                 ↓
┌──────────────────────────────────────────────────────────┐
│ 6. ПРОИЗВОДСТВО                                          │
│ Material Issue (со MRN) → FG Receipt                    │
│ TraceLinks: MRN → WO → FG Batch                         │
└────────────────┬─────────────────────────────────────────┘
                 ↓
┌──────────────────────────────────────────────────────────┐
│ 7. ИЗВОЗ (EXPORT)                                        │
│ Shipment → Export Declaration                            │
│   - Export MRN                                           │
│   - FG Batch                                             │
└────────────────┬─────────────────────────────────────────┘
                 ↓
┌──────────────────────────────────────────────────────────┐
│ 8. GUARANTEE CREDIT (Раздолжување)                      │
│ GuaranteeLedgerEntry (Credit)                           │
│   - Amount = original Debit amount                       │
│   - Link to Export MRN                                   │
│   - Link to Import MRN                                   │
│   - ActualReleaseDate = Now                             │
└──────────────────────────────────────────────────────────┘
```

## Detailed Steps

### 1. Customs Declaration (Import)

**Input:**
- Supplier Invoice
- Packing List
- Transport Document (CMR/BL/AWB)

**Process:**
```csharp
var declaration = new CustomsDeclaration
{
    DeclarationNumber = "CD-2024-12345",
    MRN = "24MK123456789012345",
    DeclarationDate = DateTime.UtcNow,
    CustomsProcedureId = inwProcessingId, // Inward Processing
    PartnerId = supplierId,
    TotalCustomsValue = 10000.00m,
    Currency = "EUR",
    DueDate = DateTime.UtcNow.AddDays(180)
};

foreach (var line in lines)
{
    var dutyAmount = line.CustomsValue * line.DutyRate / 100;
    var vatAmount = (line.CustomsValue + dutyAmount) * line.VATRate / 100;
    
    declaration.Lines.Add(new CustomsDeclarationLine
    {
        ItemId = line.ItemId,
        HSCode = "3901.10",
        Quantity = line.Quantity,
        CustomsValue = line.CustomsValue,
        DutyRate = 6.5m,
        DutyAmount = dutyAmount,
        VATRate = 18m,
        VATAmount = vatAmount
    });
}

declaration.TotalDuty = declaration.Lines.Sum(l => l.DutyAmount);
declaration.TotalVAT = declaration.Lines.Sum(l => l.VATAmount);
```

### 2. Duty Calculation

**Formula:**
```
CustomsValue = CIF value (Cost + Insurance + Freight)
DutyAmount = CustomsValue * DutyRate%
VATAmount = (CustomsValue + DutyAmount) * VATRate%
TotalAmount = DutyAmount + VATAmount + OtherCharges
```

**Example:**
```
Item: Raw Material A
HS Code: 3901.10
Customs Value: 10,000 EUR
Duty Rate: 6.5%
VAT Rate: 18%

DutyAmount = 10,000 * 6.5% = 650 EUR
VAT Base = 10,000 + 650 = 10,650 EUR
VATAmount = 10,650 * 18% = 1,917 EUR
TotalAmount = 650 + 1,917 = 2,567 EUR
```

### 3. Guarantee Debit

**For Inward Processing (50% guarantee):**

```csharp
if (procedure.RequiresGuarantee)
{
    var guaranteeAmount = declaration.TotalDuty * 
                          (procedure.GuaranteePercentage / 100);
    
    var ledgerEntry = new GuaranteeLedgerEntry
    {
        GuaranteeAccountId = accountId,
        EntryDate = DateTime.UtcNow,
        EntryType = GuaranteeEntryType.Debit,
        Amount = guaranteeAmount, // 650 * 0.5 = 325 EUR
        Currency = "EUR",
        Description = $"Import MRN {declaration.MRN}",
        MRN = declaration.MRN,
        CustomsDeclarationId = declaration.Id,
        ExpectedReleaseDate = declaration.DueDate,
        IsReleased = false
    };
    
    // Check available limit
    var currentBalance = account.GetCurrentBalance();
    if (currentBalance + guaranteeAmount > account.TotalLimit)
    {
        throw new Exception("Insufficient guarantee limit");
    }
}
```

**Ledger Visualization:**

| Date       | Type   | Amount | Description           | Balance |
|------------|--------|--------|-----------------------|---------|
| 2024-12-28 | Debit  | 325.00 | Import MRN 24MK123456 | 325.00  |

### 4. MRN Registry

```csharp
var mrnRegistry = new MRNRegistry
{
    MRN = declaration.MRN,
    CustomsDeclarationId = declaration.Id,
    RegistrationDate = DateTime.UtcNow,
    TotalQuantity = declaration.Lines.Sum(l => l.Quantity),
    UsedQuantity = 0,
    ExpiryDate = declaration.DueDate,
    IsActive = true
};
```

### 5. Material Receipt (Linkage)

```csharp
var receiptLine = new ReceiptLine
{
    ItemId = itemId,
    Quantity = 100,
    UoMId = kgUoMId,
    BatchNumber = "B-2024-1234",
    MRN = "24MK123456789012345", // ← Link to MRN
    QualityStatus = QualityStatus.OK,
    CustomsDeclarationId = declarationId
};

// Update InventoryBalance with MRN
var inventoryBalance = new InventoryBalance
{
    ItemId = itemId,
    LocationId = locationId,
    BatchNumber = "B-2024-1234",
    MRN = "24MK123456789012345", // ← Важно!
    Quantity = 100,
    QualityStatus = QualityStatus.OK
};
```

### 6. Production (MRN Tracking)

**Material Issue:**
```csharp
var materialIssue = new MaterialIssue
{
    ProductionOrderId = woId,
    ItemId = itemId,
    BatchNumber = "B-2024-1234",
    MRN = "24MK123456789012345", // ← Се пренесува MRN
    Quantity = 50
};

// TraceLink
var traceLink = new TraceLink
{
    SourceType = "Receipt",
    SourceBatchNumber = "B-2024-1234",
    SourceMRN = "24MK123456789012345",
    TargetType = "ProductionOrder",
    TargetId = woId,
    Quantity = 50
};

// Update MRN usage
mrnRegistry.UsedQuantity += 50;
```

**FG Receipt (Lineage):**
```csharp
var fgReceipt = new ProductionReceipt
{
    BatchNumber = "FG-001-20241228-001",
    Quantity = 20
};

// BatchGenealogy
var genealogy = new BatchGenealogy
{
    BatchNumber = "FG-001-20241228-001",
    ItemId = fgItemId,
    ProductionOrderId = woId,
    ParentBatches = "[\"B-2024-1234\"]", // JSON
    ParentMRNs = "[\"24MK123456789012345\"]" // JSON
};
```

### 7. Export Declaration

```csharp
var exportDeclaration = new CustomsDeclaration
{
    DeclarationNumber = "EX-2024-9999",
    MRN = "24MK999888777666555", // Export MRN
    DeclarationDate = DateTime.UtcNow,
    CustomsProcedureId = exportId,
    PartnerId = customerId,
    TotalCustomsValue = 15000.00m,
    Currency = "EUR"
};

// Link FG batch
var exportLine = new CustomsDeclarationLine
{
    ItemId = fgItemId,
    Quantity = 20,
    CustomsValue = 15000.00m
};

// Shipment
var shipment = new Shipment
{
    ShipmentNumber = "SHP-2024-1234",
    CustomerId = customerId,
    Lines = new List<ShipmentLine>
    {
        new ShipmentLine
        {
            ItemId = fgItemId,
            BatchNumber = "FG-001-20241228-001",
            Quantity = 20,
            CustomsDeclarationId = exportDeclaration.Id
        }
    }
};
```

### 8. Guarantee Credit (Release)

**Trigger:** Export cleared

```csharp
// Find original debit entry
var debitEntry = await context.GuaranteeLedgerEntries
    .FirstOrDefaultAsync(e => 
        e.MRN == "24MK123456789012345" &&
        e.EntryType == GuaranteeEntryType.Debit &&
        !e.IsReleased);

// Create credit entry
var creditEntry = new GuaranteeLedgerEntry
{
    GuaranteeAccountId = debitEntry.GuaranteeAccountId,
    EntryDate = DateTime.UtcNow,
    EntryType = GuaranteeEntryType.Credit,
    Amount = debitEntry.Amount, // Same amount
    Currency = debitEntry.Currency,
    Description = $"Export MRN {exportMRN} - Release for Import MRN {debitEntry.MRN}",
    MRN = exportMRN,
    CustomsDeclarationId = exportDeclaration.Id,
    ActualReleaseDate = DateTime.UtcNow,
    IsReleased = true
};

// Mark original debit as released
debitEntry.IsReleased = true;
debitEntry.ActualReleaseDate = DateTime.UtcNow;
```

**Ledger After Credit:**

| Date       | Type   | Amount  | Description              | Balance |
|------------|--------|---------|--------------------------|---------|
| 2024-12-28 | Debit  | 325.00  | Import MRN 24MK123456    | 325.00  |
| 2024-12-29 | Credit | -325.00 | Export MRN 24MK999888    | 0.00    |

## Guarantee Account Balance

**Calculation (Ledger-based):**
```csharp
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
```

**Example:**
```
Account: GUA-2024-001
Currency: EUR
Total Limit: 500,000.00

Ledger Entries:
1. Debit  325.00 (MRN 24MK123456)
2. Debit  450.00 (MRN 24MK223344)
3. Credit -325.00 (MRN 24MK123456 released)

Current Balance = 325 + 450 - 325 = 450.00
Available Limit = 500,000 - 450 = 499,550.00
```

## Business Rules

1. **No Import without Procedure** - Задолжителна процедура
2. **Guarantee Check** - Проверка на лимит пред увоз
3. **MRN Uniqueness** - Секој MRN е уникатен
4. **Export Linkage** - Извоз мора да има lineage до увоз (за Inward Processing)
5. **Due Date Monitoring** - Алерт ако се приближува истекот
6. **Full Release Only** - Credit само ако има соодветен Debit
