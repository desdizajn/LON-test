# API Endpoints

Base URL: `http://localhost:5000/api`

## Authentication

All endpoints require JWT Bearer token (except login).

```http
Authorization: Bearer {token}
```

---

## Master Data

### Items

#### Get All Items
```http
GET /MasterData/items?search={keyword}
```

**Response:**
```json
[
  {
    "id": "guid",
    "code": "RM-001",
    "name": "Raw Material A",
    "type": 1,
    "isBatchTracked": true,
    "isMRNTracked": true,
    "hsCode": "3901.10",
    "baseUoM": { "code": "KG", "name": "Kilogram" }
  }
]
```

### Warehouses & Locations

#### Get Warehouses
```http
GET /MasterData/warehouses
```

#### Get Locations
```http
GET /MasterData/locations?warehouseId={guid}
```

### Partners

#### Get Partners
```http
GET /MasterData/partners?type={1|2|3|4|5}
```
Types: 1=Supplier, 2=Customer, 3=Carrier, 4=CustomsBroker, 5=Bank

---

## WMS

### Inventory

#### Get Inventory
```http
GET /WMS/inventory?itemId={guid}&locationId={guid}
```

**Response:**
```json
[
  {
    "id": "guid",
    "item": { "code": "RM-001", "name": "Raw Material A" },
    "location": { "code": "STG-A-01", "name": "Storage Aisle A Rack 01" },
    "batchNumber": "B-2024-1234",
    "mrn": "24MK123456789012345",
    "quantity": 100.00,
    "uoM": { "code": "KG" },
    "qualityStatus": 1
  }
]
```

### Receipts

#### Create Receipt
```http
POST /WMS/receipts
Content-Type: application/json

{
  "receiptDate": "2024-12-28T10:00:00Z",
  "partnerId": "guid",
  "warehouseId": "guid",
  "lines": [
    {
      "itemId": "guid",
      "quantity": 100,
      "uoMId": "guid",
      "batchNumber": "B-2024-1234",
      "mrn": "24MK123456789012345",
      "qualityStatus": 1,
      "customsDeclarationId": "guid"
    }
  ]
}
```

#### Get Receipts
```http
GET /WMS/receipts?page=1&pageSize=20
```

#### Get Receipt by ID
```http
GET /WMS/receipts/{id}
```

### Shipments

#### Get Shipments
```http
GET /WMS/shipments?page=1&pageSize=20
```

### Pick Tasks

#### Get Pick Tasks
```http
GET /WMS/pick-tasks?status={1|2|3|4}
```
Status: 1=Pending, 2=InProgress, 3=Completed, 4=Cancelled

---

## Production

### Production Orders

#### Create Production Order
```http
POST /Production/orders
Content-Type: application/json

{
  "itemId": "guid",
  "orderQuantity": 100,
  "uoMId": "guid",
  "plannedStartDate": "2024-12-28T08:00:00Z",
  "plannedEndDate": "2024-12-30T17:00:00Z",
  "bomId": "guid",
  "routingId": "guid",
  "notes": "Rush order"
}
```

#### Get Production Orders
```http
GET /Production/orders?status={1|2|3|4|5|6}
```
Status: 1=Draft, 2=Released, 3=InProgress, 4=Completed, 5=Closed, 6=Cancelled

#### Get Production Order by ID
```http
GET /Production/orders/{id}
```

**Response:**
```json
{
  "id": "guid",
  "orderNumber": "LON-20241228-ABC123",
  "item": { "code": "FG-001", "name": "Finished Good A" },
  "orderQuantity": 100.00,
  "producedQuantity": 0.00,
  "scrapQuantity": 0.00,
  "status": 2,
  "plannedStartDate": "2024-12-28T08:00:00Z",
  "plannedEndDate": "2024-12-30T17:00:00Z",
  "bom": {
    "lines": [
      { "item": { "code": "RM-001" }, "quantity": 50.00 }
    ]
  },
  "materials": [
    {
      "item": { "code": "RM-001" },
      "requiredQuantity": 50.00,
      "issuedQuantity": 0.00,
      "reservedQuantity": 0.00
    }
  ]
}
```

#### Get Material Issues
```http
GET /Production/orders/{id}/material-issues
```

#### Get Production Receipts
```http
GET /Production/orders/{id}/receipts
```

### BOMs

#### Get BOMs
```http
GET /Production/boms?itemId={guid}
```

---

## Customs

### Declarations

#### Create Declaration
```http
POST /Customs/declarations
Content-Type: application/json

{
  "declarationNumber": "CD-2024-12345",
  "mrn": "24MK123456789012345",
  "declarationDate": "2024-12-28T10:00:00Z",
  "customsProcedureId": "guid",
  "partnerId": "guid",
  "totalCustomsValue": 10000.00,
  "currency": "EUR",
  "lines": [
    {
      "itemId": "guid",
      "hsCode": "3901.10",
      "quantity": 100,
      "uoMId": "guid",
      "customsValue": 10000.00,
      "countryOfOrigin": "DEU",
      "dutyRate": 6.5,
      "vatRate": 18
    }
  ]
}
```

#### Get Declarations
```http
GET /Customs/declarations?isCleared={true|false}
```

#### Get Declaration by ID
```http
GET /Customs/declarations/{id}
```

### Procedures

#### Get Procedures
```http
GET /Customs/procedures
```

**Response:**
```json
[
  {
    "id": "guid",
    "code": "INW-PROC",
    "name": "Inward Processing",
    "type": 3,
    "requiresGuarantee": true,
    "guaranteePercentage": 50,
    "dueDays": 180,
    "requiresMRNTracking": true,
    "allowsProduction": true,
    "allowsExport": true
  }
]
```

### MRN Registry

#### Get MRN Registry
```http
GET /Customs/mrn-registry?mrn={keyword}&isActive={true|false}
```

#### Get MRN by Number
```http
GET /Customs/mrn-registry/{mrn}
```

**Response:**
```json
{
  "mrn": "24MK123456789012345",
  "totalQuantity": 100.00,
  "usedQuantity": 50.00,
  "remainingQuantity": 50.00,
  "isFullyUsed": false,
  "expiryDate": "2025-06-28T00:00:00Z",
  "customsDeclaration": {
    "declarationNumber": "CD-2024-12345",
    "lines": [...]
  }
}
```

---

## Guarantees

### Accounts

#### Get Accounts
```http
GET /Guarantee/accounts
```

**Response:**
```json
[
  {
    "id": "guid",
    "accountNumber": "GUA-2024-001",
    "accountName": "Main Guarantee Account EUR",
    "currency": "EUR",
    "totalLimit": 500000.00,
    "currentBalance": 1250.00,
    "availableLimit": 498750.00,
    "bankName": "National Bank"
  }
]
```

#### Get Account by ID
```http
GET /Guarantee/accounts/{id}
```

### Ledger

#### Debit Guarantee
```http
POST /Guarantee/debit
Content-Type: application/json

{
  "guaranteeAccountId": "guid",
  "amount": 325.00,
  "description": "Import MRN 24MK123456789012345",
  "mrn": "24MK123456789012345",
  "customsDeclarationId": "guid",
  "expectedReleaseDate": "2025-06-28T00:00:00Z"
}
```

#### Credit Guarantee
```http
POST /Guarantee/credit
Content-Type: application/json

{
  "guaranteeAccountId": "guid",
  "amount": 325.00,
  "description": "Export MRN 24MK999888 - Release for Import",
  "mrn": "24MK999888777666555",
  "customsDeclarationId": "guid",
  "relatedDebitEntryId": "guid"
}
```

#### Get Ledger
```http
GET /Guarantee/ledger?accountId={guid}&isReleased={true|false}
```

#### Get Active Guarantees
```http
GET /Guarantee/active-guarantees
```

---

## Traceability

### Trace Forward
```http
GET /Traceability/trace-forward?batchNumber={batch}&mrn={mrn}
```

**Response:**
```json
[
  {
    "sourceType": "Receipt",
    "sourceBatchNumber": "B-2024-1234",
    "sourceMRN": "24MK123456789012345",
    "targetType": "ProductionOrder",
    "targetId": "guid",
    "targetBatchNumber": null,
    "itemCode": "RM-001",
    "itemName": "Raw Material A",
    "quantity": 50.00
  }
]
```

### Trace Backward
```http
GET /Traceability/trace-backward?batchNumber={batch}&mrn={mrn}
```

### Get Batch Genealogy
```http
GET /Traceability/genealogy/{batchNumber}
```

**Response:**
```json
{
  "batchNumber": "FG-001-20241228-001",
  "item": { "code": "FG-001", "name": "Finished Good A" },
  "createdDate": "2024-12-28T15:00:00Z",
  "productionOrderId": "guid",
  "parentBatches": "[\"B-2024-1234\",\"B-2024-5678\"]",
  "parentMRNs": "[\"24MK123456789012345\",\"24MK987654321098765\"]"
}
```

### Trace Full Path
```http
GET /Traceability/trace-full?batchNumber={batch}
```

---

## Analytics

### Dashboard
```http
GET /Analytics/dashboard
```

**Response:**
```json
{
  "inventory": {
    "totalItems": 150,
    "totalLocations": 25,
    "totalBalance": 5234.50,
    "blockedQty": 123.00
  },
  "production": {
    "activeOrders": 12,
    "completedToday": 3,
    "wip": 450.00
  },
  "customs": {
    "pendingDeclarations": 5,
    "activeMRNs": 8,
    "expiringMRNs": 2
  },
  "guarantees": {
    "totalAccounts": 2,
    "activeGuarantees": 6,
    "totalExposure": 3250.00,
    "expiringGuarantees": 1
  }
}
```

### Production KPIs
```http
GET /Analytics/production-kpi?fromDate={date}&toDate={date}
```

### WMS KPIs
```http
GET /Analytics/wms-kpi?fromDate={date}&toDate={date}
```

### Guarantee Exposure
```http
GET /Analytics/guarantee-exposure
```

### Customs Summary
```http
GET /Analytics/customs-summary?fromDate={date}&toDate={date}
```

### Inventory by Location
```http
GET /Analytics/inventory-by-location
```

### MRN Usage
```http
GET /Analytics/mrn-usage
```

---

## Error Responses

All endpoints return standard error responses:

**400 Bad Request:**
```json
{
  "isSuccess": false,
  "errorMessage": "Validation failed",
  "errors": [
    "ItemId is required",
    "Quantity must be greater than 0"
  ]
}
```

**401 Unauthorized:**
```json
{
  "message": "Unauthorized"
}
```

**404 Not Found:**
```json
{
  "message": "Resource not found"
}
```

**500 Internal Server Error:**
```json
{
  "message": "An error occurred",
  "details": "..."
}
```
