# 🔍 GAP Анализа - Frontend vs Backend

**Дата:** 17 Јануари 2026  
**Статус:** Бекенд е комплетен (~95%), Frontend има значителни празнини (~40% завршено)

---

## 📊 Брз Преглед

| Модул | Backend API | Frontend Форми | Frontend Листи | Статус |
|-------|------------|----------------|----------------|--------|
| **Master Data** | ✅ 100% | ✅ 85% | ✅ 90% | 🟡 Добро |
| **WMS** | ✅ 100% | ❌ 10% | ✅ 50% | 🔴 Критично |
| **Production** | ✅ 100% | ❌ 10% | ✅ 50% | 🔴 Критично |
| **Customs** | ✅ 100% | ❌ 0% | ✅ 50% | 🔴 Критично |
| **Guarantees** | ✅ 100% | ❌ 0% | ✅ 70% | 🔴 Критично |
| **Traceability** | ✅ 100% | N/A | ✅ 80% | 🟡 Добро |
| **Analytics** | ✅ 100% | N/A | ✅ 90% | 🟢 Одлично |
| **Knowledge Base** | ✅ 100% | N/A | ❌ 0% | 🔴 Недостасува |

---

## 🔴 КРИТИЧНИ НЕДОСТАТОЦИ (За клиентско тестирање)

### 1. WMS - Warehouse Management System ⚠️

#### ❌ Недостасува (КРИТИЧНО):
1. **Receipt Form** - Форма за примка на стока
   - Backend: ✅ `POST /api/wms/receipts`
   - Frontend: ❌ НЕ постои
   - Компоненти: `ReceiptForm.tsx`, `ReceiptLineForm.tsx`

2. **Transfer Form** - Форма за трансфер меѓу локации
   - Backend: ✅ Entity и логика постојат
   - Frontend: ❌ НЕ постои
   - Компоненти: `TransferForm.tsx`

3. **Shipment Form** - Форма за испорака
   - Backend: ✅ `GET /api/wms/shipments`
   - Frontend: ❌ Само листа, нема форма
   - Компоненти: `ShipmentForm.tsx`, `ShipmentLineForm.tsx`

4. **Pick Task Management** - Управување со pick задачи
   - Backend: ✅ `GET /api/wms/pick-tasks`
   - Frontend: ❌ Само листа, нема детали/форми
   - Компоненти: `PickTaskDetail.tsx`, `PickTaskAssignment.tsx`

5. **Cycle Count Form** - Форма за инвентура
   - Backend: ✅ Entity постои
   - Frontend: ❌ НЕ постои никаде
   - Компоненти: `CycleCountForm.tsx`, `CycleCountExecution.tsx`

6. **Quality Status Management** - Блокирање/одблокирање на стока
   - Backend: ✅ InventoryBalance има qualityStatus
   - Frontend: ❌ Нема форма за промена на статус
   - Компоненти: `QualityStatusForm.tsx`

#### ✅ Постои (Делумно):
- `Inventory.tsx` - Листа на inventory (само read-only) ✅

---

### 2. Production (LON) 🏭

#### ❌ Недостасува (КРИТИЧНО):
1. **Production Order Form** - Форма за креирање на налози
   - Backend: ✅ `POST /api/production/orders`
   - Frontend: ❌ НЕ постои
   - Компоненти: `ProductionOrderForm.tsx`

2. **Material Issue Form** - Форма за издавање материјал на Work Order
   - Backend: ✅ MaterialIssue entity, `GET /api/production/orders/{id}/material-issues`
   - Frontend: ❌ НЕ постои
   - Компоненти: `MaterialIssueForm.tsx` (MUST have Batch + MRN picker!)

3. **Production Receipt Form** - Форма за примка на готов производ
   - Backend: ✅ ProductionReceipt entity, `GET /api/production/orders/{id}/receipts`
   - Frontend: ❌ НЕ постои
   - Компоненти: `ProductionReceiptForm.tsx` (Auto-generate batch!)

4. **Scrap Reporting Form** - Форма за отпад
   - Backend: ✅ ProductionOrder.ScrapQuantity field
   - Frontend: ❌ НЕ постои
   - Компоненти: `ScrapReportForm.tsx`

5. **Production Order Detail Page** - Детална страница за Work Order
   - Backend: ✅ `GET /api/production/orders/{id}`
   - Frontend: ❌ НЕ постои
   - Компоненти: `ProductionOrderDetail.tsx` (покажи lifecycle, материјали, операции)

6. **Operation Execution** - Извршување на операции
   - Backend: ✅ ProductionOrderOperation entity
   - Frontend: ❌ НЕ постои
   - Компоненти: `OperationExecutionForm.tsx`

#### ✅ Постои (Делумно):
- `Production.tsx` - Листа на Production Orders (само read-only) ✅
- `BOMsList.tsx` + `BOMDetail.tsx` - BOM management ✅
- `RoutingsList.tsx` + `RoutingDetail.tsx` - Routing management ✅

---

### 3. Customs & MRN 🚢

#### ❌ Недостасува (КРИТИЧНО):
1. **Customs Declaration Form** - Форма за царинска декларација (SADка)
   - Backend: ✅ `POST /api/customs/declarations`
   - Frontend: ❌ НЕ постои
   - Компоненти: `CustomsDeclarationForm.tsx` (комплексна форма!)
   - Полиња: Box 1-54, Item lines, Duty calculation

2. **MRN Registry Detail** - Детали за MRN
   - Backend: ✅ `GET /api/customs/mrn-registry/{mrn}`
   - Frontend: ❌ НЕ постои
   - Компоненти: `MRNDetail.tsx` (покажи usage, expiration, linked batches)

3. **Procedure Selection Wizard** - Wizard за избор на процедура
   - Backend: ✅ `GET /api/customs/procedures`
   - Frontend: ❌ НЕ постои
   - Компоненти: `ProcedureWizard.tsx`

4. **Document Upload** - Upload на документи
   - Backend: ✅ Document entity
   - Frontend: ❌ НЕ постои
   - Компоненти: `DocumentUpload.tsx`

5. **Due Date Alerts** - Алерти за истекувачки MRN
   - Backend: ✅ MRNRegistry.DueDate
   - Frontend: ❌ Нема alert UI
   - Компоненти: `DueDateAlerts.tsx`

#### ✅ Постои (Делумно):
- `Customs.tsx` - Листа на декларации (само read-only) ✅

---

### 4. Guarantees 💰

#### ❌ Недостасува (КРИТИЧНО):
1. **Manual Ledger Entry Form** - Рачна ентија (debit/credit)
   - Backend: ✅ `POST /api/guarantee/debit`, `POST /api/guarantee/credit`
   - Frontend: ❌ НЕ постои
   - Компоненти: `LedgerEntryForm.tsx`

2. **Guarantee Account Detail** - Детали за сметка
   - Backend: ✅ `GET /api/guarantee/accounts/{id}`
   - Frontend: ❌ НЕ постои
   - Компоненти: `GuaranteeAccountDetail.tsx` (покажи ledger, exposure timeline)

3. **Multi-currency Support UI** - Приказ на повеќе валути
   - Backend: ✅ GuaranteeLedgerEntry.Currency field
   - Frontend: ❌ Нема currency conversion UI
   - Компоненти: `CurrencyConverter.tsx`

4. **Expiring Guarantees Alerts** - Алерти за истекувачки гаранции
   - Backend: ✅ Data постои
   - Frontend: ❌ Нема alert UI
   - Компоненти: `ExpiringGuaranteesWidget.tsx`

#### ✅ Постои (Делумно):
- `Guarantees.tsx` - Листа на accounts + active guarantees ✅

---

### 5. Knowledge Base (RAG) 🧠

#### ❌ ЦЕЛОСНО НЕДОСТАСУВА:
1. **RAG Interface** - Интерфејс за прашања и одговори
   - Backend: ✅ `POST /api/knowledge-base/ask`
   - Frontend: ❌ НЕ постои
   - Компоненти: `KnowledgeBaseChat.tsx`, `RAGInterface.tsx`

2. **Semantic Search** - Пребарување на документи
   - Backend: ✅ `POST /api/knowledge-base/search`
   - Frontend: ❌ НЕ постои
   - Компоненти: `DocumentSearch.tsx`

3. **Context Help Widget** - Context-aware помош на форми
   - Backend: ✅ `POST /api/knowledge-base/explain`
   - Frontend: ❌ НЕ постои
   - Компоненти: `ContextHelp.tsx` (inline на customs форма!)

4. **Stats Dashboard** - Статистики за KB
   - Backend: ✅ `GET /api/knowledge-base/stats`
   - Frontend: ❌ НЕ постои
   - Компоненти: `KnowledgeBaseStats.tsx`

---

## 🟡 СРЕДНО ПРИОРИТЕТНИ НЕДОСТАТОЦИ

### Traceability 🔍
- ✅ `Traceability.tsx` постои со forward/backward trace
- ❌ Недостасува: Genealogy visualization (графички приказ)
- ❌ Недостасува: Batch detail page со full history

### Master Data 📂
- ✅ Items, Partners, Warehouses, UoM, BOMs, Routings - сè постои
- ❌ Недостасува: WorkCenters form
- ❌ Недостасува: Machines form
- ❌ Недостасува: Locations (bin level) form

### User Management 👥
- ✅ UserManagement, EmployeeManagement, ShiftManagement, RoleManagement - сè постои
- 🟢 Овој модул е комплетен!

---

## 📋 ПРИОРИТЕТНА ЛИСТА ЗА ИМПЛЕМЕНТАЦИЈА

### **Фаза 1: КРИТИЧНО (За клиентско тестирање)**
Овие форми се АПСОЛУТНО неопходни за да системот биде функционален:

#### 1. WMS (1-2 дена)
- [ ] `ReceiptForm.tsx` - Примка на стока ⚡ КРИТИЧНО
- [ ] `TransferForm.tsx` - Трансфер меѓу локации
- [ ] `ShipmentForm.tsx` - Испорака
- [ ] `PickTaskDetail.tsx` - Детали на pick задача
- [ ] `QualityStatusForm.tsx` - Блокирање на стока

#### 2. Production (2-3 дена)
- [ ] `ProductionOrderForm.tsx` - Креирање на Work Order ⚡ КРИТИЧНО
- [ ] `ProductionOrderDetail.tsx` - Детали на Work Order ⚡ КРИТИЧНО
- [ ] `MaterialIssueForm.tsx` - Издавање материјал (со Batch + MRN picker!) ⚡ КРИТИЧНО
- [ ] `ProductionReceiptForm.tsx` - Примка на готов производ ⚡ КРИТИЧНО
- [ ] `ScrapReportForm.tsx` - Отпад

#### 3. Customs (3-4 дена)
- [ ] `CustomsDeclarationForm.tsx` - Царинска декларација (SADка) ⚡ КРИТИЧНО
  - Multi-step wizard (General info → Items → Duty calc → Submit)
  - Box 1-54 полиња
  - Item lines со HS Code, Origin, Value
  - Auto-calculate duty
- [ ] `MRNDetail.tsx` - MRN детали со usage tracking
- [ ] `DocumentUpload.tsx` - Upload документи

#### 4. Guarantees (1 ден)
- [ ] `LedgerEntryForm.tsx` - Рачна ентија
- [ ] `GuaranteeAccountDetail.tsx` - Детали на сметка

#### 5. Knowledge Base (2 дена)
- [ ] `KnowledgeBaseChat.tsx` - RAG интерфејс ⚡ КРИТИЧНО
- [ ] `ContextHelp.tsx` - Inline помош на customs форма
- [ ] `DocumentSearch.tsx` - Semantic search

---

### **Фаза 2: СРЕДНО ПРИОРИТЕТНИ (После клиентско тестирање)**

#### 6. Enhanced WMS (1-2 дена)
- [ ] `CycleCountForm.tsx` - Инвентура
- [ ] `ReplenishmentForm.tsx` - Пополнување
- [ ] `InventoryAdjustmentForm.tsx` - Adjustment

#### 7. Enhanced Production (1 ден)
- [ ] `OperationExecutionForm.tsx` - Извршување на операции
- [ ] `ProductionScheduler.tsx` - Scheduler за Work Orders

#### 8. Enhanced Traceability (1 ден)
- [ ] `BatchGenealogyGraph.tsx` - Графички приказ на genealogy
- [ ] `BatchDetailPage.tsx` - Batch историја

#### 9. Master Data Enhancement (1 ден)
- [ ] `WorkCenterForm.tsx` - Work centers
- [ ] `MachineForm.tsx` - Machines
- [ ] `LocationForm.tsx` - Bin locations

---

## 📊 Процент на Завршеност

### Backend (Одлично! 🟢)
- **API Endpoints:** 65+ endpoints - ✅ 100%
- **Domain Entities:** 42+ entities - ✅ 100%
- **CQRS Commands:** ✅ Имплементирани
- **Event Sourcing:** ✅ Guarantee ledger work
- **Vector Store + RAG:** ✅ 100%

### Frontend (Критично! 🔴)
- **Листи (Read-only):** ~50-70% ✅
- **Форми (Create/Update):** ~15% ❌
- **Детални страници:** ~20% ❌
- **Knowledge Base UI:** 0% ❌
- **Workflow Wizards:** 0% ❌

### **Вкупна Завршеност:**
- **Backend:** 95% ✅
- **Frontend:** 35-40% ❌
- **ВКУПНО СИСТЕМОТ:** ~60% 🟡

---

## 🚀 Предлог за Брзо Решение

### Опција 1: Минимален Viable Product (MVP) - 3-4 дена
Имплементирај само критичните форми за основен flow:
1. `ReceiptForm.tsx` - Примка
2. `ProductionOrderForm.tsx` - Production Order
3. `MaterialIssueForm.tsx` - Issue материјал
4. `ProductionReceiptForm.tsx` - FG Receipt
5. `CustomsDeclarationForm.tsx` - Basic SADка форма
6. `KnowledgeBaseChat.tsx` - RAG интерфејс

**Резултат:** Основен end-to-end flow функционален за тестирање.

---

### Опција 2: Комплетно Решение - 7-10 дена
Имплементирај ги сите критични форми од Фаза 1:
- Сите WMS форми
- Сите Production форми
- Customs wizard со валидација
- Guarantees management
- Knowledge Base full UI

**Резултат:** Целосно функционален систем подготвен за production.

---

### Опција 3: Parallel Development - 4-5 дена со повеќе луѓе
- Dev 1: WMS forms (2 дена)
- Dev 2: Production forms (3 дена)
- Dev 3: Customs forms (4 дена)
- Dev 4: Knowledge Base UI (2 дена)

**Резултат:** Паралелна работа = побрза испорака.

---

## 📞 Препораки

### За Клиентско Тестирање (ИТНО):
1. **Имплементирај MVP (Опција 1)** - 3-4 дена
2. Фокусирај се на **end-to-end flow:**
   - Receipt → Production → Issue → FG Receipt → Customs → Export
3. Додади **Knowledge Base UI** за "wow" фактор
4. Тестирај со 2-3 тест сценарија

### За Production Release:
1. **Комплетирај Фаза 1** (сите критични форми)
2. Додади **валидација** на форми (пред submit)
3. Додади **error handling** (user-friendly messages)
4. Додади **loading states** (spinners, skeletons)
5. Додади **confirmation dialogs** (пред delete/submit)

---

## 🎯 Заклучок

**Бекендот е одличен!** 🟢 Сите API-ја се готови, чекаат да бидат користени.

**Фронтендот има сериозни празнини.** 🔴 Постојат само read-only листи, критичните форми за create/update недостасуваат.

**За клиентско тестирање:** Потребни се минимум **3-4 дена** за MVP имплементација на критичните форми.

**За production:** Потребни се **7-10 дена** за комплетна имплементација.

---

**Следен чекор:** Одлучи кој approach (MVP vs Full vs Parallel) и започнувам со имплементација веднаш! 🚀
