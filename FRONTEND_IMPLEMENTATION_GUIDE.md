# 🚀 LON Frontend Implementation - Remaining Components

**Date:** 17 January 2026  
**Status:** In Progress - Core components created, remaining components documented

---

## ✅ Completed Components (Just Created)

### WMS Module
1. ✅ `ReceiptForm.tsx` - Full receipt creation with lines
2. ✅ `TransferForm.tsx` - Transfer between warehouses/locations
3. ✅ `ShipmentForm.tsx` - Shipment creation with lines

### Production Module
1. ✅ `ProductionOrderForm.tsx` - Create production orders

### Type Definitions
1. ✅ `types/wms.ts` - All WMS interfaces
2. ✅ `types/production.ts` - All Production interfaces
3. ✅ `types/customs.ts` - All Customs interfaces
4. ✅ `types/guarantee.ts` - All Guarantee interfaces
5. ✅ `types/knowledgeBase.ts` - All Knowledge Base interfaces

### API Services
1. ✅ Extended `api.ts` with all CRUD methods for:
   - WMS (create receipt, shipment, transfer, quality status)
   - Production (create order, material issue, receipt, scrap)
   - Customs (create/update declaration, document upload)
   - Guarantee (debit/credit ledger entries)
   - Knowledge Base (ask, explain, search)

---

## 📋 Remaining Components to Implement

### 1. WMS Components (High Priority)

#### PickTaskDetail.tsx
```tsx
// Location: /frontend/web/src/components/WMS/PickTaskDetail.tsx
// Purpose: Display pick task details and allow completion
// Features:
// - Show task details (item, batch, MRN, location)
// - Assign to employee
// - Complete task with actual quantities
// - Update status (Pending → Assigned → InProgress → Completed)

Key Fields:
- pickTaskNumber (display)
- item, quantity, location (display)
- batchNumber, MRN (display)
- assignedTo (select employee)
- status (badge)
- completedQuantity (input on complete)
- completionNotes (textarea)

API Calls:
- GET /api/WMS/pick-tasks/{id}
- POST /api/WMS/pick-tasks/{id}/assign
- POST /api/WMS/pick-tasks/{id}/complete
```

#### QualityStatusForm.tsx
```tsx
// Location: /frontend/web/src/components/WMS/QualityStatusForm.tsx
// Purpose: Change quality status of inventory (Block/Unblock)
// Features:
// - Select inventory balance (item + batch + location)
// - Current status display
// - New status selection (OK, Blocked, Quarantine)
// - Reason (required)
// - Notes

Key Fields:
- inventoryBalanceId (hidden/from parent)
- currentQualityStatus (display only)
- newQualityStatus (select: OK, Blocked, Quarantine)
- reason (input, required)
- notes (textarea)

API Calls:
- POST /api/WMS/inventory/quality-status
```

---

### 2. Production Components (High Priority)

#### ProductionOrderDetail.tsx
```tsx
// Location: /frontend/web/src/pages/Production/ProductionOrderDetail.tsx
// Purpose: Full production order details with lifecycle management
// Features:
// - Order header (item, quantity, dates, status)
// - BOM lines display
// - Material reservations table
// - Material issues list
// - Production receipts list
// - Scrap reports
// - Operations list with status
// - Status transition buttons (Release, Start, Complete, Close)

Sections:
1. Header Card (Order info, status badge, action buttons)
2. BOM Tab (materials required)
3. Materials Tab (issues with batch/MRN tracking)
4. Receipts Tab (finished goods received)
5. Scrap Tab (scrap reports)
6. Operations Tab (routing operations with status)

API Calls:
- GET /api/Production/orders/{id}
- GET /api/Production/orders/{id}/material-issues
- GET /api/Production/orders/{id}/receipts
- PUT /api/Production/orders/{id}/status
```

#### MaterialIssueForm.tsx ⚡ CRITICAL!
```tsx
// Location: /frontend/web/src/components/Production/MaterialIssueForm.tsx
// Purpose: Issue materials to production order (MUST have Batch + MRN picker!)
// Features:
// - Production Order selection (or passed from parent)
// - Item selection (filtered by BOM)
// - Quantity input
// - **Batch Number picker** (autocomplete from inventory)
// - **MRN picker** (autocomplete from inventory) - CRITICAL FOR TRACEABILITY
// - Location selection (from inventory with batch)
// - Issue date
// - Notes

Key Fields:
- productionOrderId (select or hidden)
- itemId (select from BOM lines)
- quantity (number)
- batchNumber (autocomplete from inventory) ⚡
- mrn (autocomplete from inventory) ⚡
- locationId (select, filtered by item+batch+mrn)
- issueDate (date)
- notes (textarea)

IMPORTANT:
- When item selected, show available inventory with batches & MRNs
- Must track which raw material batch + MRN goes into which production order
- This enables full backward traceability (FG → Raw materials)

API Calls:
- GET /api/Production/orders/{id} (get BOM)
- GET /api/WMS/inventory?itemId={itemId} (get available batches)
- POST /api/Production/material-issues
```

#### ProductionReceiptForm.tsx
```tsx
// Location: /frontend/web/src/components/Production/ProductionReceiptForm.tsx
// Purpose: Receive finished goods from production order
// Features:
// - Production Order selection
// - Item (auto from order)
// - Quantity received
// - **Auto-generate batch number** (format: FG-{ItemCode}-{Date}-{Seq})
// - Location selection
// - Quality status (OK, Blocked, Quarantine)
// - Receipt date
// - Notes

Key Fields:
- productionOrderId (select or hidden)
- itemId (display only, from order)
- quantity (number)
- batchNumber (auto-generated, read-only)
- locationId (select)
- qualityStatus (select: OK, Blocked, Quarantine)
- receiptDate (date)
- notes (textarea)

Batch Generation Logic:
const batchNumber = `FG-${itemCode}-${yyyymmdd}-${sequence}`;
// Example: FG-CHAIR01-20260117-001

API Calls:
- GET /api/Production/orders/{id}
- POST /api/Production/receipts
```

#### ScrapReportForm.tsx
```tsx
// Location: /frontend/web/src/components/Production/ScrapReportForm.tsx
// Purpose: Report scrap/waste during production
// Features:
// - Production Order selection
// - Item (from order)
// - Scrap quantity
// - Scrap reason (dropdown: Defect, Rework, Trimming, Other)
// - Scrap date
// - Notes

Key Fields:
- productionOrderId (select)
- itemId (display, from order)
- quantity (number)
- reason (select: Defect, Rework, Trimming, Setup, Other)
- scrapDate (date)
- notes (textarea)

API Calls:
- POST /api/Production/scrap
```

---

### 3. Customs Components (High Priority)

#### CustomsDeclarationForm.tsx ⚡ CRITICAL COMPLEX FORM!
```tsx
// Location: /frontend/web/src/pages/Customs/CustomsDeclarationForm.tsx
// Purpose: Multi-step wizard for customs declaration (SAD Form)
// Architecture: Multi-step wizard with validation

STEP 1: General Information (Boxes 1-14)
Fields:
- declarationDate (date)
- customsProcedureId (select: Local Purchase, Temp Import, Inward Processing, etc.)
- box1_DeclarationType (select: IM, EX, etc.)
- box2_Exporter (input or partner select)
- box8_ConsigneeId (partner select)
- box14_Declarant (input)

STEP 2: Transport & Location (Boxes 15-29)
Fields:
- box15a_ExportCountry (country select)
- box17a_DestinationCountry (country select)
- box18_IdentityMeans (input: vehicle registration)
- box20_DeliveryTerms (select: FOB, CIF, EXW, etc.)
- box22_Currency (select: MKD, EUR, USD)
- box25_TransportMode (select: 1-Road, 2-Rail, 3-Sea, 4-Air)
- box29_CustomsOffice (select from code list)

STEP 3: Items (Box 31-54)
Features:
- Add multiple item lines
- Each line has:
  - itemNumber (auto: 1, 2, 3...)
  - itemId (select)
  - hsCode (input, 10 digits)
  - description (textarea)
  - quantity, uoMId
  - batchNumber, MRN (for traceability!)
  - countryOfOrigin (select)
  - customsValue (number)
  - dutyRate (number, %)
  - vatRate (number, %)
  - netMass, grossMass (kg)
  
Auto-Calculate:
- dutyAmount = customsValue * dutyRate / 100
- vatAmount = (customsValue + dutyAmount) * vatRate / 100

STEP 4: Summary & Submit
Display:
- All entered data in readonly format
- Total customs value (sum of all items)
- Total duty (sum)
- Total VAT (sum)
- Grand total
- Submit button

Validation:
- Step 1: Required fields
- Step 2: Valid codes
- Step 3: At least 1 item, all item fields filled
- Step 4: Review and confirm

API Calls:
- GET /api/Customs/procedures
- POST /api/Customs/declarations
```

#### MRNDetail.tsx
```tsx
// Location: /frontend/web/src/pages/Customs/MRNDetail.tsx
// Purpose: Show MRN details with usage tracking
// Features:
// - MRN header (number, procedure, dates, amounts)
// - Guarantee information
// - Linked batches table (which batches use this MRN)
// - Usage timeline
// - Status (Active/Expired/Closed)
// - Expiration alert if near due date

Sections:
1. MRN Card (mrn, procedure, registration date, due date)
2. Financial Card (duty amount, guarantee amount, used/available)
3. Linked Batches Table (batch, item, quantity, used quantity)
4. Related Declarations (which imports/exports used this MRN)

API Calls:
- GET /api/Customs/mrn-registry/{mrn}
```

#### DocumentUpload.tsx
```tsx
// Location: /frontend/web/src/components/Customs/DocumentUpload.tsx
// Purpose: Upload supporting documents for customs declaration
// Features:
// - File upload (PDF, DOCX, JPG, PNG)
// - Document type selection (Invoice, Packing List, Certificate of Origin, etc.)
// - Document number
// - Document date
// - Notes

Key Fields:
- customsDeclarationId (hidden, from parent)
- documentType (select: Invoice, PackingList, Certificate, TIR, CMR, Other)
- documentNumber (input)
- documentDate (date)
- file (file input)
- notes (textarea)

API Calls:
- POST /api/Customs/documents (multipart/form-data)
```

---

### 4. Guarantees Components

#### LedgerEntryForm.tsx
```tsx
// Location: /frontend/web/src/components/Guarantee/LedgerEntryForm.tsx
// Purpose: Manual debit/credit ledger entry
// Features:
// - Account selection
// - Entry type (Debit/Credit radio buttons)
// - Amount
// - Currency
// - MRN (optional, for linking)
// - Description (required)
// - Expected release date (for debits)
// - Notes

Key Fields:
- guaranteeAccountId (select)
- entryType (radio: Debit, Credit)
- amount (number)
- currency (select: MKD, EUR, USD)
- mrn (input, optional)
- description (textarea, required)
- expectedReleaseDate (date, required if Debit)
- notes (textarea)

API Calls:
- GET /api/Guarantee/accounts
- POST /api/Guarantee/debit (if entryType = Debit)
- POST /api/Guarantee/credit (if entryType = Credit)
```

#### GuaranteeAccountDetail.tsx
```tsx
// Location: /frontend/web/src/pages/Guarantee/GuaranteeAccountDetail.tsx
// Purpose: Full guarantee account details with ledger
// Features:
// - Account header (name, bank, limit, balance, available)
// - Exposure chart (visual gauge of usage)
// - Ledger table (all debit/credit entries with MRN links)
// - Filter by date range, released/unreleased
// - Export to Excel

Sections:
1. Account Card (name, bank, currency, limits)
2. Exposure Gauge (visual: used vs available)
3. Ledger Table (date, type, amount, MRN, description, released, balance)
4. Summary Stats (total debit, total credit, current balance, active count)

API Calls:
- GET /api/Guarantee/accounts/{id}
- GET /api/Guarantee/ledger?accountId={id}
```

---

### 5. Knowledge Base Components (High Priority)

#### KnowledgeBaseChat.tsx ⚡ WOW FACTOR!
```tsx
// Location: /frontend/web/src/pages/KnowledgeBase/KnowledgeBaseChat.tsx
// Purpose: RAG-powered chat interface for customs/regulations questions
// Architecture: Chat-style interface with message history

UI Components:
1. Chat Header (title, status indicator)
2. Message List (scrollable)
   - User messages (right-aligned, blue)
   - AI responses (left-aligned, gray)
   - Source references (collapsible under each answer)
3. Input Area (textarea + send button)
4. Typing indicator (animated dots while processing)

Features:
- Ask questions about customs regulations
- Display answers with source references
- Click source to see full document chunk
- Message history (stored in state)
- Loading indicator while processing
- Confidence score display
- Copy answer button
- Clear chat button

Example Messages:
User: "Што е Box 15a во SADката?"
AI: "Box 15a претставува земја на испорака... [Sources: Правилник Art. 32, SAD Упатство стр. 15]"

API Calls:
- POST /api/KnowledgeBase/ask
- GET /api/KnowledgeBase/health (on mount)
```

#### ContextHelp.tsx ⚡ INLINE HELP WIDGET!
```tsx
// Location: /frontend/web/src/components/KnowledgeBase/ContextHelp.tsx
// Purpose: Inline help widget for form fields (especially customs form)
// Architecture: Tooltip/popover component

Usage:
<FormInput label="Box 15a - Export Country" />
<ContextHelp field="Box15a" />

When clicked:
- Show popover with:
  - Field explanation
  - Related regulations
  - Example values
  - Common mistakes
  - Links to full documentation

Props:
- field: string (field identifier)
- context?: string (additional context)

API Calls:
- POST /api/KnowledgeBase/explain
```

#### DocumentSearch.tsx
```tsx
// Location: /frontend/web/src/components/KnowledgeBase/DocumentSearch.tsx
// Purpose: Semantic search across all knowledge base documents
// Features:
// - Search input
// - Results list with relevance scores
// - Document title, chunk preview
// - Click to expand full chunk
// - Highlight search terms
// - Filter by document type

UI:
- Search bar (with icon)
- Results count
- Result cards:
  - Document title
  - Chunk preview (truncated)
  - Relevance score (visual bar)
  - Expand/collapse button

API Calls:
- POST /api/KnowledgeBase/search
```

---

## 🔄 Integration Points

### Update Inventory.tsx Page
Add buttons to open forms:
```tsx
<button onClick={() => setShowReceiptForm(true)}>+ New Receipt</button>
<button onClick={() => setShowTransferForm(true)}>+ Transfer</button>
<button onClick={() => setShowQualityForm(true)}>Quality Status</button>

{showReceiptForm && <ReceiptForm onSuccess={handleSuccess} onCancel={handleCancel} />}
```

### Update Production.tsx Page
Add buttons:
```tsx
<button onClick={() => setShowOrderForm(true)}>+ New Production Order</button>
<button onClick={() => navigateTo(`/production/orders/${id}`)}>View Details</button>
```

### Update Customs.tsx Page
Add buttons:
```tsx
<button onClick={() => setShowDeclarationForm(true)}>+ New Declaration</button>
<button onClick={() => navigateTo(`/customs/mrn/${mrn}`)}>View MRN</button>
```

### Update App.tsx Routes
Add new routes:
```tsx
<Route path="/production/orders/:id" element={<ProtectedRoute><ProductionOrderDetail /></ProtectedRoute>} />
<Route path="/customs/mrn/:mrn" element={<ProtectedRoute><MRNDetail /></ProtectedRoute>} />
<Route path="/guarantees/accounts/:id" element={<ProtectedRoute><GuaranteeAccountDetail /></ProtectedRoute>} />
<Route path="/knowledge-base" element={<ProtectedRoute><KnowledgeBaseChat /></ProtectedRoute>} />
```

### Update Sidebar.tsx
Add Knowledge Base menu item:
```tsx
<Link to="/knowledge-base">
  <i className="icon-brain"></i> Knowledge Base
</Link>
```

---

## 🎨 Shared Components Needed

### BatchMRNPicker.tsx ⚡ CRITICAL REUSABLE COMPONENT!
```tsx
// Location: /frontend/web/src/components/common/BatchMRNPicker.tsx
// Purpose: Reusable component for selecting Batch + MRN from inventory
// Used in: MaterialIssueForm, ReceiptForm, TransferForm, ShipmentForm

Props:
- itemId: string (filter inventory by item)
- locationId?: string (filter by location)
- onSelect: (batch: string, mrn: string, locationId: string) => void

UI:
- Autocomplete/Dropdown showing:
  - Batch Number
  - MRN (if exists)
  - Location
  - Available Quantity
  - Quality Status

Features:
- Real-time search/filter
- Shows available quantity
- Disabled if blocked/quarantine
- Highlights low stock
```

### FormDialog.tsx
```tsx
// Generic modal dialog wrapper for forms
// Makes forms appear as modals instead of full pages
```

---

## 📦 Next Steps for Completion

### Priority 1 (Next 2-3 days)
1. ✅ MaterialIssueForm with Batch+MRN picker
2. ✅ ProductionReceiptForm
3. ✅ ProductionOrderDetail page
4. ✅ CustomsDeclarationForm (multi-step wizard)
5. ✅ KnowledgeBaseChat

### Priority 2 (Next 2 days)
6. ✅ MRNDetail page
7. ✅ GuaranteeAccountDetail page
8. ✅ PickTaskDetail
9. ✅ QualityStatusForm
10. ✅ ContextHelp widget

### Priority 3 (1-2 days)
11. ✅ DocumentSearch
12. ✅ ScrapReportForm
13. ✅ LedgerEntryForm
14. ✅ BatchMRNPicker component
15. ✅ Update all routes and navigation

### Priority 4 (Testing & Polish)
16. ✅ Form validation (all forms)
17. ✅ Error handling (user-friendly messages)
18. ✅ Loading states (spinners, skeletons)
19. ✅ Confirmation dialogs (before delete/submit)
20. ✅ End-to-end testing

---

## 🎯 Estimated Time Remaining

- **Priority 1 (Critical):** 2-3 days
- **Priority 2 (Important):** 2 days  
- **Priority 3 (Nice-to-have):** 1-2 days
- **Priority 4 (Polish):** 1 day

**Total:** 6-8 days for complete implementation

---

## 📝 Quick Implementation Template

For each remaining component, follow this pattern:

1. **Create component file** in appropriate folder
2. **Import types** from types folder
3. **Setup state** with useState
4. **Load data** with useEffect
5. **Handle form submit** with API call
6. **Add validation** before submit
7. **Show toast** on success/error
8. **Call parent callback** (onSuccess, onCancel)

Example skeleton:
```tsx
import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { FormData } from '../../types/module';
import { toast } from 'react-toastify';

const MyForm: React.FC<Props> = ({ onSuccess, onCancel }) => {
  const [formData, setFormData] = useState<FormData>({...});
  const [loading, setLoading] = useState(false);

  useEffect(() => { loadData(); }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      await api.createSomething(formData);
      toast.success('Success!');
      if (onSuccess) onSuccess();
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Error');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="form-container">
      <form onSubmit={handleSubmit}>
        {/* Form fields */}
        <div className="form-actions">
          <button type="submit" disabled={loading}>Submit</button>
        </div>
      </form>
    </div>
  );
};

export default MyForm;
```

---

**Status:** Ready for continued implementation!  
**Next Action:** Start with Priority 1 components (MaterialIssueForm, ProductionReceipt, CustomsDeclaration, KnowledgeBaseChat)
