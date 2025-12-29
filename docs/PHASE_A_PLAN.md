# 🎯 Phase A Implementation Plan

**Start Date**: 29 December 2024  
**Duration**: Week 1-2 (7-10 working days)  
**Goal**: Foundation - Master Data + Common Components

---

## 📦 Dependencies Added

```json
"@mui/material": "^5.15.3"           // Material-UI components
"@mui/icons-material": "^5.15.3"     // Icons
"@emotion/react": "^11.11.3"         // MUI dependency
"@emotion/styled": "^11.11.0"        // MUI dependency
"@mui/x-date-pickers": "^6.18.6"     // Date pickers
"react-hook-form": "^7.49.2"         // Form management
"@tanstack/react-table": "^8.11.2"   // Advanced tables
"react-toastify": "^9.1.3"           // Toast notifications
"zustand": "^4.4.7"                  // State management
"date-fns": "^3.0.6"                 // Date utilities
```

---

## 🏗️ Project Structure

```
frontend/web/src/
├── components/
│   ├── common/
│   │   ├── DataTable.tsx          // Generic table with sorting/filtering
│   │   ├── FormDialog.tsx         // Modal dialog for forms
│   │   ├── LoadingSpinner.tsx     // Loading indicator
│   │   ├── ErrorDisplay.tsx       // Error messages
│   │   ├── ConfirmDialog.tsx      // Confirmation dialogs
│   │   └── ToastNotification.tsx  // Toast setup
│   ├── forms/
│   │   ├── FormInput.tsx          // Text input with validation
│   │   ├── FormSelect.tsx         // Dropdown select
│   │   ├── FormCheckbox.tsx       // Checkbox
│   │   ├── FormDatePicker.tsx     // Date picker
│   │   └── FormAutocomplete.tsx   // Autocomplete select
│   └── Sidebar.tsx (existing)
├── pages/
│   ├── MasterData/
│   │   ├── Items/
│   │   │   ├── ItemsList.tsx
│   │   │   ├── ItemForm.tsx
│   │   │   └── ItemDetail.tsx
│   │   ├── Partners/
│   │   │   ├── PartnersList.tsx
│   │   │   ├── PartnerForm.tsx
│   │   │   └── PartnerDetail.tsx
│   │   ├── Warehouses/
│   │   │   ├── WarehousesList.tsx
│   │   │   ├── WarehouseForm.tsx
│   │   │   └── LocationsManager.tsx
│   │   ├── UoM/
│   │   │   ├── UoMList.tsx
│   │   │   └── UoMForm.tsx
│   │   ├── BOMs/
│   │   │   ├── BOMsList.tsx
│   │   │   ├── BOMForm.tsx
│   │   │   └── BOMDetail.tsx
│   │   └── Routings/
│   │       ├── RoutingsList.tsx
│   │       ├── RoutingForm.tsx
│   │       └── RoutingDetail.tsx
│   └── (existing pages)
├── services/
│   ├── api.ts (existing - extend)
│   └── masterDataApi.ts (new)
├── store/
│   └── useStore.ts (Zustand store)
├── types/
│   └── masterData.ts (TypeScript interfaces)
└── utils/
    └── validators.ts (Validation functions)
```

---

## 📋 Implementation Checklist

### Day 1-2: Setup & Common Components ✅

- [x] Install dependencies (npm install)
- [ ] Create common components:
  - [ ] DataTable.tsx
  - [ ] FormDialog.tsx
  - [ ] LoadingSpinner.tsx
  - [ ] ErrorDisplay.tsx
  - [ ] ConfirmDialog.tsx
  - [ ] ToastNotification setup
- [ ] Create form components:
  - [ ] FormInput.tsx
  - [ ] FormSelect.tsx
  - [ ] FormCheckbox.tsx
  - [ ] FormDatePicker.tsx
  - [ ] FormAutocomplete.tsx
- [ ] Create TypeScript interfaces (types/masterData.ts)
- [ ] Setup Zustand store (store/useStore.ts)
- [ ] Extend API service (services/masterDataApi.ts)

### Day 3: Items Management

- [ ] ItemsList.tsx (table со search/filter)
- [ ] ItemForm.tsx (create/edit)
- [ ] ItemDetail.tsx (view)
- [ ] API integration (GET, POST, PUT, DELETE)
- [ ] Validation (code, HSCode format)
- [ ] Test CRUD operations

### Day 4: Partners Management

- [ ] PartnersList.tsx
- [ ] PartnerForm.tsx
- [ ] PartnerDetail.tsx
- [ ] API integration
- [ ] EORI/VAT validation
- [ ] Test CRUD operations

### Day 5: Warehouses & Locations

- [ ] WarehousesList.tsx
- [ ] WarehouseForm.tsx
- [ ] LocationsManager.tsx (tree view)
- [ ] API integration
- [ ] Hierarchical location display
- [ ] Test CRUD operations

### Day 6: UoM Management

- [ ] UoMList.tsx
- [ ] UoMForm.tsx
- [ ] API integration
- [ ] Conversion factor logic
- [ ] Test CRUD operations

### Day 7: BOMs Management

- [ ] BOMsList.tsx
- [ ] BOMForm.tsx (master-detail)
- [ ] BOMDetail.tsx
- [ ] BOM Lines editable table
- [ ] API integration
- [ ] Version management
- [ ] Test CRUD operations

### Day 8: Routings Management

- [ ] RoutingsList.tsx
- [ ] RoutingForm.tsx
- [ ] RoutingDetail.tsx
- [ ] Operations editable table
- [ ] API integration
- [ ] Test CRUD operations

### Day 9: Integration & Polish

- [ ] Update Sidebar with Master Data menu
- [ ] Update App.tsx routing
- [ ] End-to-end testing
- [ ] Error handling improvements
- [ ] Loading states polish
- [ ] Responsive design check

### Day 10: Testing & Documentation

- [ ] User acceptance testing
- [ ] Bug fixes
- [ ] Performance optimization
- [ ] Update documentation
- [ ] Create demo data

---

## 🎯 Acceptance Criteria

**Phase A is complete when:**

1. ✅ All Master Data entities може да се креираат (Create)
2. ✅ All Master Data entities може да се читаат (Read/List)
3. ✅ All Master Data entities може да се едитираат (Update)
4. ✅ All Master Data entities може да се бришат (Delete)
5. ✅ Validation работи на сите форми
6. ✅ Error handling е функционален
7. ✅ Loading states се прикажуваат коректно
8. ✅ Tables имаат search/filter/pagination
9. ✅ Master Data menu е достапно во sidebar
10. ✅ Нема console errors
11. ✅ API calls работат коректно
12. ✅ Responsive design на mobile/tablet

---

## 🚀 Quick Start

```bash
cd /workspaces/LON-test/frontend/web

# Install dependencies
npm install

# Start development server
npm start

# В друг терминал - стартувај backend
cd /workspaces/LON-test
dotnet run --project src/LON.API/LON.API.csproj
```

---

## 📝 Notes

- Користиме Material-UI за брз и consistent UI
- React Hook Form за form management (performance)
- Zustand за лесен state management
- TanStack Table за advanced table features
- Toast notifications за user feedback

---

**Status**: 🔄 IN PROGRESS  
**Next**: Common Components Implementation
