# PLAN — Delta + Roadmap to v1

> **Read this together with [`BLUEPRINT.md`](BLUEPRINT.md).** PLAN describes *how* to get from current state to BLUEPRINT. Every task references a BLUEPRINT section (e.g. „BLUEPRINT §5.6 — Podelba"). When BLUEPRINT changes, PLAN follows.
>
> Status updates here, not in BLUEPRINT. SESSION_LOG.md remains the chronological evidence trail.
>
> *Последна ревизија: 2026-05-11 — иницијална верзија.*

---

## §1 — Текоven status (snapshot)

**As of 2026-05-11:**

| Слој | Status | Бројки |
|---|---|---|
| Backend (Domain + App + API) | Solid, active development | 31.8k LoC src/, 174 routes, 57 handlers, 76 DbSets |
| Database | Solid; 50 EF migrations | Last migration 2026-05-11 (P16.C3c `AddSupplierInvoice`) |
| Integration tests | Good coverage on core flows | 154 [Fact]/[Theory], 39 files; gaps in WMS controller, RBAC, MasterData CRUD |
| Frontend compile | Clean | 0 TS errors, 0 ESLint errors, 1 unused import |
| Frontend UI | **Хаотичен** | 122 pages, 131 routes, 91 inline styles, 82 bootstrap, 20 MUI, 6 DataTable, 8 react-hook-form |
| API contract | 100% covered | 85 FE endpoints all in 174 BE routes (case-correct match) |
| navGroups honesty | **Лажи** | 6 nav items marked `exists` actually use localStorage |
| Duplicate pages | **2 confirmed (warehouses)** | Audit deferred to Phase 16.A3 |
| ClientOrder concept | **Missing** | No top-level „order from customer" entity; flow scattered |
| AI helper | RAG exists, not user-facing | KnowledgeBase endpoints exist; not invoked from business pages |
| Tenant isolation | Query filter only | RLS not deployed; tampered queries can leak (security debt) |
| Audit trail | Partial | IAuditable + AuditLogEntry exist; interceptor not wired; many entities not audited |
| Soft-delete | Mostly missing | Some entities have IsDeleted; most don't; no recycle bin UI |
| Concurrency / numbering | DMax+1 risk | No SQL SEQUENCE objects yet (legacy pattern carried over) |
| ELON migration | Partial dry-run | Not reconciled; far from cutover-ready |
| Backup / DR | Manual | No automation, no tested restore |
| Languages | 4 stubbed | EN + MK active in v1; SQ + SR present but post-v1 |
| Multi-tenant deployment | Capability built, single tenant deployed | Teksport only |
| E2E tests | Not started | Playwright not yet installed; happy-path missing |
| Mobile | Flutter folder exists | Not in v1 |

---

## §2 — Delta per BLUEPRINT section

> For each BLUEPRINT section, the gap to v1.

| BLUEPRINT § | Topic | Current state | Gap to v1 | Owner phase |
|---|---|---|---|---|
| §1 Vision & Scope | Decisions captured | n/a | n/a (decisions made) | — |
| §2 Architecture | Stack chosen | All pieces present | TS 5 upgrade (optional) | Phase 17 (opportunistic) |
| §3.1 ClientOrder | Missing entity | none | Create entity + migration + handlers + endpoints | **Phase 17.1** |
| §3.2 Customs subdomain | Solid | All entities exist | Add ClientOrderId FK to declarations | Phase 17.1 |
| §3.3 Guarantee | Mostly built | GuaranteeAccount + Ledger + Snapshot exist | Add domain event triggers + ceiling enforcement + override permission | **Phase 17.11** |
| §3.4 Inventory state | LonProcessState exists | All 5 states defined | Verify all 5 transitions emit events + audit | Phase 17.11 |
| §3.5 Master data | Solid | Items, Partners, BOMs, etc. all present | Polish + missing Forms (Machines, WorkCenters, TariffCodes — Phase 16.A3 audit) | Phase 16.A3 |
| §3.6 Domain events | Not yet pattern | Direct handler-to-handler; no event log | Introduce IDomainEvent + DomainEventLog table + handler dispatch | **Phase 17.11** |
| §3.7 Soft-delete + audit | Partial | Interface exists; not enforced | Implement EF SaveChangesInterceptor; mark all required entities | **Phase 17.13** |
| §4 Roles & personas | 9 roles seeded | Sidebar matrix exists | RBAC enforced at endpoint level (FluentValidation + attributes); add Subcontractor + Speditor seed | Phase 18 / 19 |
| §5.1 ClientOrder приjem | Missing UI hub | n/a (no entity) | New `/orders` route + hub page | **Phase 17.2** |
| §5.2 IM declaration | Functional | Page exists | Wire from ClientOrder hub (inline create); add AI variance hint | Phase 17.3 |
| §5.3 Receipt | Functional | Inventory.tsx works | Pilot react-query migration (Phase 16.B1); wire from hub | Phase 16.B1 + 17.4 |
| §5.4 BOM + ProductionOrder | Functional | Pages exist | Wire from hub; add BOM template suggestion | Phase 17.5 |
| §5.5 Diпositiция за шпедитер | Manual exports work | Excel/CSV/XML download from declaration | Speditor profile entity + login (Phase 19) | Phase 19 |
| §5.6 Podelba | Functional | Page + command exist | Wire from hub; AI producer-suggestion | Phase 17.6 |
| §5.7 MaterialIssue | Functional | Page + command exist | Wire from hub | Phase 17.7 |
| §5.8 Production tracking | Functional | Multiple pages | Wire from hub; verify event emission | Phase 17.7 |
| §5.9 QC + Packaging | Minimal | FinishedGoods pages exist | Add FinishedGoodReceipt flow (HQ-bound) + QC inspection entity | Phase 17.8 |
| §5.10 Извоз (EX) | Functional | Page + command exist | Wire from hub; pre-flight guarantee check | Phase 17.8 |
| §5.11 Razdolzuvanje | Endpoint exists | Razdolzuvanje endpoint exists | Add ClientOrder-level view + reconciliation flags | Phase 17.9 |
| §6.1 Guarantee lifecycle | Partial | Ledger entries via direct command calls | Switch to event-driven (one ledger entry per event, no direct writes) | Phase 17.11 |
| §6.2 Inventory state machine | Working | Transitions in handlers | Add event emissions + verify all 5 transitions | Phase 17.11 |
| §6.3 Skart vs Otpad | Implemented | Skart entity + Otpad slots exist | UX clarification (separate entry points) | Phase 17 (polish) |
| §6.4 Average rate | Implemented | P15.17 | n/a | — |
| §6.5 Audit trail | Partial | Entity interfaces exist | Interceptor + before/after value capture + `/admin/audit-log` UI activation | **Phase 17.13** |
| §6.6 Numbering & concurrency | Risk: DMax+1 | Multiple SEQUENCE-eligible counters use legacy pattern | Replace with SQL SEQUENCE + NumberFormatter | **Phase 17.12** |
| §6.7 Soft-delete | Mostly missing | n/a | Global filter + recycle bin UI | **Phase 17.14** |
| §6.8 i18n | 4 locales stubbed | EN + MK active | Verify all v1 pages have MK + EN keys; server-side i18n for error messages | Phase 17 (polish) |
| §6.9 Tenant isolation (RLS) | EF filter only | Query filter applied | **SQL Server RLS policy** + middleware SESSION_CONTEXT setup + pen test | **Phase 20** |
| §6.10 Backup & DR | Manual | No automation | Daily backup cron + monthly restore drill | **Phase 20** |
| §6.11 RAG operations | Implemented | KnowledgeBase ingestion works | Connect AI helper to RAG (Phase 17.10) | Phase 17.10 |
| §7.1 Hub-and-spoke | Missing | Pages are stand-alone lists | ClientOrder hub becomes the central entry | Phase 17.2 |
| §7.2 Contextual actions | Partial | Some detail pages have actions | Audit + add missing action launchers on all detail pages | Phase 17 (polish) |
| §7.3 Smart prefill | Partial | KW12 wizard does mapping | Suggestions service: BOM template, TariffCode, Country, Producer | Phase 17.10 |
| §7.4 AI assistant | Missing UX | RAG endpoints exist | Floating button + 3 core recommendations + free-form Q&A | **Phase 17.10** |
| §7.5 Forms | Partial | 8 of 122 use react-hook-form | Migration ongoing (per page touched) | Phase 16.B (and onward) |
| §7.6 Tables | Partial | 6 of 122 use DataTable | Migration ongoing | Phase 16.B2 (and onward) |
| §7.7 Responsive | Untested | CRA default | Audit + fix breaking pages on tablet | Phase 17 (polish) |
| §8.1 Multi-tenant data model | Built | ITenantScoped + TenantId everywhere | RLS deployment | Phase 20 |
| §8.2 Auth + RBAC | Built | JWT works | `[HasPermission]` attribute enforcement | Phase 17 (polish) |
| §8.3 API design | OK | OpenAPI generated | Cursor pagination for big tables (post-v1 polish) | Post-v1 |
| §8.4 Frontend stack | Reaching consensus | Multiple patterns coexist | Phase 16.B locks in standard | Phase 16.B |
| §8.5 Testing strategy | Integration solid; E2E missing | 154 integration tests | Playwright E2E for v1 acceptance loop | **Phase 17.E + Phase 22** |
| §9.1 ELON migration | Partial | LON.Migration project exists | Reconciliation loop + cutover dry-run | **Phase 21** |
| §9.2 KW12 wizard | Functional | Phase 5 work | Polish (mapping templates persistence verified) | Phase 17 (opportunistic) |
| §9.3 Speditor export | Manual hardcoded | Excel download works | SpeditorExportProfile entity + UI | Phase 19 |
| §9.4 PEE XML | Implemented | P15.12+ | Manual download for v1 (no change) | — |

---

## §3 — Phase sequence with dependencies

```
Phase 16 ✅ closed 2026-05-11
                        │ blocks 17
Phase 17 (in progress: PRE → E0–E16 → E15) ─┐
                                            │ blocks 18, 19
Phase 18 ────────────────────────────────────┤
                                            │
Phase 19 ────────────────────────────────────┤
                                            │
Phase 20 ────────────────────────────────────┤ blocks 21
                                            │
Phase 21 ────────────────────────────────────┘  v1!
```

> Phase 17 inserted a **PRE sub-phase** (PRE.1–PRE.7) before E0 to fix the BLUEPRINT §9.1 mapping (9 corrections from prep recon), execute VPS wipe, and import Z2779 happy-path as the canonical fixture. See `CLAUDE.md §11.1`.

Phases 18 + 19 may run in parallel (independent role implementations).

**Critical path** to v1: 16 → 17 → 20 → 21. Phases 18 + 19 may slip to v1.1 if calendar pressure (subcontractor/speditor login is not Teksport-day-one-critical — they currently work via email/phone).

---

## §4 — Sub-task list per phase

### Phase 16 — Cleanup + UI foundation  *(detailed prompts in AGENT-PROMPTS.md §A–§D)*

- [ ] 16.A1  Remove dead WarehousesList + /warehouses-old route
- [ ] 16.A2  Honest navGroups status for 6 localStorage-only pages
- [ ] 16.A3  MasterData duplication audit
- [ ] 16.B1  react-query + Inventory.tsx pilot migration
- [ ] 16.B2  DataTable hardening + Production.tsx orders migration
- [ ] 16.B3  PageShell + MUI theme + 3 pilot pages
- [ ] 16.C1  RiskRegisterItem entity (Risks + Escalations)
- [ ] 16.C2  EmployeeCertification entity (Training)
- [ ] 16.C3.a CostRate entity (CostAccounting)
- [ ] 16.C3.b PayrollPeriod + PayrollLine entity (PayrollAggregate)
- [ ] 16.C3.c SupplierInvoice entity (SupplierInvoices)
- [ ] 16.D1  WMSController integration tests
- [ ] 16.D2  Role × permission matrix tests
- [ ] 16.D3  MasterData CRUD smoke tests

### Phase 17 — ClientOrder hub + flow wiring + AI helper minimum  *(prompts in AGENT-PROMPTS.md §E0–§E15)*

**Phase 17.PRE — Migration foundations + Z2779 happy-path** *(inserted 2026-05-12 after prep recon)*

- [x] 17.PRE.1 CLAUDE.md §4/§5 + §11 stale-fact corrections (LON DB row, migration count, Phase 16→17) → committed `6e27a88`
- [x] 17.PRE.2 BLUEPRINT §9.1 mapping update (9 corrections; Proces resolver; missing-tables flagged) + Cowork audit closeout (Izdatnica/Ispratnica + inflate-for-waste + sticky-defaults reframe + HR caveat) → committed `6e27a88`+`7e67f1e`
- [x] 17.PRE.3 6 user decisions resolved 2026-05-12 (D1=wipe approved, D2=env-var admin password, D3=local DB created, D4=new CommercialInvoice entity, D5=new DeliveryNote entity, D6=Phase 21 prod-export for HR) → committed `f6f0fb7`
- [x] 17.PRE.4 `docs/migration/MAPPING.md` (authoritative legacy→LON, table-by-table) → `4847d43`
- [x] 17.PRE.5 Executed VPS wipe (backup retained: `LONDB_pre-wipe_20260512T091454Z.bak`; all business tables empty; 50 migrations preserved) → `5f07cb2`+`9b0967b`
- [x] 17.PRE.6 Env-var admin password infrastructure (`LON_BOOTSTRAP_ADMIN_PASSWORD`) deployed + VPS seed verified (admin login HTTP 200, 30 permissions, 12 roles, 9 users) → `4b9170a`
- [x] 17.PRE.7 LON.Migration discovery + structural-mismatch findings documented (`docs/migration/PRE7_FINDINGS.md`); full Z2779 happy-path **deferred to new task `E.MIGRATE`** that lands after E1+E5+E7.6+E8.5

**Phase 17 main**

- [x] 17.0   `useStickyDefaults` hook + `BulkFieldUpdateButton` + bulk-update endpoint pattern (foundation; 13 FE tests green; 4 integration tests; deployed `06e6019`)  → BLUEPRINT §7.3.1
- [x] 17.1   ClientOrder entity + migration + 5 handlers + endpoints + SQL SEQUENCE + 5 integration tests (deployed `2d166d8`; VPS smoke: CO-2026-000001 Draft)  → BLUEPRINT §3.1
- [ ] 17.2   ClientOrder list + hub UI  → BLUEPRINT §5.1, §7.1
- [ ] 17.3   Wire IM declaration from hub  → BLUEPRINT §5.2
- [ ] 17.4   Wire Receipt from hub  → BLUEPRINT §5.3
- [ ] 17.5   Wire BOM + ProductionOrder from hub  → BLUEPRINT §5.4
- [ ] 17.6   Wire Podelba from hub  → BLUEPRINT §5.6
- [ ] 17.7   Wire MaterialIssue + ProductionReceipt from hub  → BLUEPRINT §5.7, §5.8
- [ ] 17.7.6 `DeliveryNote` entity + polymorphic auto-gen on commit events (D5 — replaces Propratnici/Stavki 1.6k+296k legacy rows)  → BLUEPRINT §3.8
- [ ] 17.8   Wire EX declaration + Shipment from hub + FinishedGoodReceipt + QC  → BLUEPRINT §5.9, §5.10
- [ ] 17.8.5 `CommercialInvoice` entity + EX hub chain (D4 — replaces tblIzvozniFakturi/Stavki 3.2k+57.9k legacy rows; finance integration deferred to Phase 27)  → BLUEPRINT §3.2.1
- [ ] 17.9   Razdolzuvanje view per ClientOrder  → BLUEPRINT §5.11
- [ ] 17.E.MIGRATE  LON.Migration refactor (OdobrenijaMapper + ClientOrderMapper + BOMMapper + MaterialIssueMapper + WasteDeclarationMapper + DeliveryNoteMapper + CommercialInvoiceMapper + `--zaklucok` filter) → run `Z2779` end-to-end + assert 6 reconciliation queries pass (PRE.7 deferred deliverable; see `docs/migration/PRE7_FINDINGS.md` §6)  → BLUEPRINT §9.1 + MAPPING.md
- [ ] 17.10  AI helper service + 3 core recommendations + floating UI  → BLUEPRINT §7.4
- [ ] 17.11  Domain events infrastructure + handler refactor (guarantee, inventory transitions)  → BLUEPRINT §3.6, §6.1, §6.2
- [ ] 17.12  SQL SEQUENCE objects + NumberFormatter  → BLUEPRINT §6.6
- [ ] 17.13  Audit interceptor + AuditLogEntry writes + /admin/audit-log UI activation  → BLUEPRINT §6.5
- [ ] 17.14  Soft-delete global filter + recycle bin UI  → BLUEPRINT §6.7
- [ ] 17.7.5 Department + Position lookup promotion (CodeListItem reuse) — **D6 decided 2026-05-12**: prod-export path. Recommended: defer schema + backfill entirely to Phase 21.1.1. Alternative: land schema in Phase 17 (empty seed) and backfill in Phase 21.1.1.  → BLUEPRINT §5.12.1
- [ ] 17.10.5 AlertRule + AlertEvent entities + 6 predefined rules + nightly worker evaluator (no UI editor — Phase 26 adds it)  → BLUEPRINT §5.13.4
- [ ] 17.X1  FxRate entity + manual maintenance UI `/finance/fx-rates`  → BLUEPRINT §5.14.8
- [ ] 17.E   Playwright E2E happy-path test (the v1 acceptance loop, dev mode)  → BLUEPRINT §8.5

### Phase 18 — Subcontractor login + role  *(prompts in AGENT-PROMPTS.md §F1–§F6)*

- [ ] 18.1  Subcontractor role + seed
- [ ] 18.2  JWT claims expansion (external_partner_id)
- [ ] 18.3  Server-side filter for subcontractor queries
- [ ] 18.4  Subcontractor dashboard UI
- [ ] 18.5  RLS predicates extended (when Phase 20 done) for `OR external_partner_id`
- [ ] 18.6  Integration + Playwright E2E for subcontractor isolation

### Phase 19 — Speditor role + export polish  *(prompts in AGENT-PROMPTS.md §G1–§G4)*

- [ ] 19.1  Speditor role + seed
- [ ] 19.2  SpeditorExportProfile entity + admin UI
- [ ] 19.3  Speditor login + shipment-detail view
- [ ] 19.4  Auto-email on shipment ready (optional)

### Phase 20 — RLS + tenant security audit  *(prompts in AGENT-PROMPTS.md §H1–§H5)*

- [ ] 20.1  RLS predicate function + policy creation
- [ ] 20.2  Middleware: SESSION_CONTEXT per request
- [ ] 20.3  Pen test (tampered JWT + IgnoreQueryFilters bypass attempt)
- [ ] 20.4  Security audit doc + signoff
- [ ] 20.5  Backup automation (cron + scp) + first restore drill

### Phase 21 — Migration + production hardening + launch  *(prompts in AGENT-PROMPTS.md §I1–§I6)*

- [ ] 21.1  ELON migration dry-run loop (until reconciliation 100%)
- [ ] 21.1.1 HR data backfill from prod ELON export (D6): import `tblKorisnikTEKSPORT` → resolve `FakturiU5Z.User` + `LagerMaterijali.User` int FKs to real User rows; backfill `Employee.DepartmentId` + `Employee.PositionId` from prod Department/Position strings; also missing master-data tables import (`KnigaNai`, `Aneksi`, `Preferencijal`, `tblFirmi`)  → BLUEPRINT §9.1
- [ ] 21.2  Reconciliation queries documented
- [ ] 21.3  Cutover plan written + reviewed
- [ ] 21.4  USER_MANUAL.md updated to reflect ClientOrder hub
- [ ] 21.5  Final Playwright E2E sweep (full v1 acceptance loop, prod-like)
- [ ] 21.6  Go-live ceremony — final smoke on VPS, archive ELON

---

### Post-v1 — Production detail expansion *(scoped in BLUEPRINT §5.8 + §5.9, deferred)*

**Phase 22 — Production tracking detail** *(post-v1, opens after launch stabilizes)*

- [ ] 22.1  OperationTimeLog real-time operator UI (start/pause/finish)  → BLUEPRINT §5.8.2
- [ ] 22.2  Operator performance reports + ranking  → BLUEPRINT §5.8.2
- [ ] 22.3  Machine state IoT integration (MQTT broker + auto-capture)  → BLUEPRINT §5.8.3
- [ ] 22.4  OEE dashboards per WorkCenter  → BLUEPRINT §5.8.3
- [ ] 22.5  Predictive maintenance RAG suggestions  → BLUEPRINT §5.8.3
- [ ] 22.6  ScrapEvent photo upload + weight-station integration  → BLUEPRINT §5.8.4
- [ ] 22.7  Bulk scrap event CSV import  → BLUEPRINT §5.8.4

**Phase 23 — QC + Packaging detail** *(post-v1)*

- [ ] 23.1  PackList entity + multi-box editor + label printing  → BLUEPRINT §5.9.3
- [ ] 23.2  Packaging verification mobile scanner workflow  → BLUEPRINT §5.9.3
- [ ] 23.3  Defect analytics (per-producer, per-item, trends)  → BLUEPRINT §5.9.4
- [ ] 23.4  Photo + multi-attachment per QualityInspection  → BLUEPRINT §5.9.2
- [ ] 23.5  Defect pattern AI alerts  → BLUEPRINT §7.4

**Phase 24 — Production scheduling engine** *(post-v1)*

- [ ] 24.1  ProductionSchedule entity + Gantt UI  → BLUEPRINT §5.8.9
- [ ] 24.2  Constraint solver (OR-Tools or custom) for optimal assignment  → BLUEPRINT §5.8.9
- [ ] 24.3  Big-board kiosk display + WebSocket live updates  → BLUEPRINT §5.8.11
- [ ] 24.4  Reschedule on disruption flow  → BLUEPRINT §5.8.9

**Phase 25 — HR depth** *(post-v1)*

- [ ] 25.1  RFID/card reader integration  → BLUEPRINT §5.12.3
- [ ] 25.2  ShiftSwap workflow  → BLUEPRINT §5.12.2
- [ ] 25.3  EmployeePerformanceReview entity + workflow  → BLUEPRINT §5.12.8
- [ ] 25.4  Certification → machine binding enforcement  → BLUEPRINT §5.12.6/7
- [ ] 25.5  HR dashboard polish  → BLUEPRINT §5.12.10

**Phase 26 — Management Reporting depth** *(post-v1)*

- [ ] 26.1  Configurable AlertRule editor UI  → BLUEPRINT §5.13.4
- [ ] 26.2  Recurring email schedule per report  → BLUEPRINT §5.13.6
- [ ] 26.3  Push notification channel  → BLUEPRINT §5.13.4
- [ ] 26.4  AI daily briefing predictive layer  → BLUEPRINT §5.13.5

**Phase 27 — Finance depth** *(post-v1)*

- [ ] 27.1  Auto FX import from central bank API  → BLUEPRINT §5.14.8
- [ ] 27.2  What-if cash flow simulation  → BLUEPRINT §5.14.6
- [ ] 27.3  Overhead allocation in cost accounting  → BLUEPRINT §5.14.4
- [ ] 27.4  AP aging with reminder workflow  → BLUEPRINT §5.14.3
- [ ] 27.5  Generic accounting export (beyond Payroll)  → BLUEPRINT §5.14

**Phase 28+ — Mobile (Flutter)**, **Phase 29+ — Customs auto-submit**, etc. (BLUEPRINT §11 out-of-v1 listed sections — each becomes its own phase when activated).

---

## §5 — Done definition per phase

### Phase 16 Done

- `tsc --noEmit` 0 errors  on frontend.
- `eslint` 0 errors, ≤2 warnings.
- No `grep -rn "localStorage\.\(set\|get\)Item.*lon\." frontend/web/src/pages` for business data (UI prefs `lon.ui.*` OK).
- No duplicate page routes wired in App.tsx (audit completed).
- All 6 Phase 16.C entities have integration tests + tenant isolation verified.
- New WMS, RBAC, MasterData CRUD tests bring total to ≥ 200 [Fact]/[Theory].
- VPS smoke screenshots in SESSION_LOG for each 16.B + 16.C task.

### Phase 17 Done

- ClientOrder hub renders on VPS at `/orders/:id` for at least 1 real ClientOrder of Teksport's biggest customer (real data).
- Full v1 acceptance loop (BLUEPRINT §1.3) executable from the hub without ever leaving it (all actions reachable via action launcher).
- Playwright E2E test for full loop passes locally + in CI.
- Domain events emitted on all critical writes (verified by `DomainEventLog` rows in DB).
- All numbering uses SEQUENCE objects (`grep -rn "DMax\|MAX.*+.*1" src/` returns only legacy migration code, no new handler usage).
- AuditLogEntry rows present for every modification of audited entities (verified by integration test that touches each entity type).
- `/admin/audit-log` page functional на VPS, filters work.
- AI helper floating button on every page; 3 recommendations live: ClientOrder hub blocked-step, Receipt variance, Razdolzuvanje pre-flight.

### Phase 18 Done

- Subcontractor user can log in on VPS.
- Sees only their producer-assigned ProductionOrders + materials (verified via Playwright + manual).
- Subcontractor cannot access master data, finance, or other producers' data (verified via direct API calls with their JWT).

### Phase 19 Done

- Speditor user can log in.
- Sees only their assigned shipments.
- Can download shipment documents (Izpratnica PDF + customs declaration).
- (Optional) Auto-email on shipment ready triggers successfully.

### Phase 20 Done

- RLS policy applied to every ITenantScoped table.
- Tampered JWT scenario (manual SQL with different TenantId) returns 0 rows.
- Pen test report archived in `docs/security/PHASE20_PENTEST.md`.
- Daily backup cron job runs unattended for 7 consecutive days.
- One restore drill performed; SESSION_LOG entry confirms data integrity post-restore.

### Phase 21 Done

- ELON Teksport DB dry-run migrates to LON Teksport DB with reconciliation discrepancies = 0 (within tolerance).
- USER_MANUAL.md reflects current hub-based UX.
- Cutover plan reviewed by user; rehearsed on staging.
- Final v1 acceptance E2E passes on VPS production environment.
- User signs off: „LON is ready to replace ELON for Teksport on `<date>`."

---

## §6 — Risks & mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Phase 17 ClientOrder migration breaks existing flows | Med | High | Feature flag the hub; old direct-page navigation remains as fallback during Phase 17 |
| Playwright E2E flakiness | High | Med | Use test data isolation (per-test tenant), explicit waits, retry policy |
| AI helper hallucinates wrong recommendation | Med | High | Strict function-calling pattern; recommendations bounded by structured data queries, not free LLM imagination; user can dismiss; log all suggestions for audit |
| RLS performance overhead on large queries | Low | Med | Benchmark in Phase 20; fallback option = combined RLS + query filter (defense in depth, predicate kept simple) |
| ELON migration takes longer than 1 day | Med | High | Phase 21.1 dry-run loop measures duration; if >8h, plan staged cutover (read-only LON parallel with ELON for 1 week) |
| User scope creep mid-Phase 17 | Med | High | New requests filed in BLUEPRINT §11 „Open" or added explicitly to Phase 18+; not absorbed into running phase |
| Critical bug found post-cutover | Med | Critical | ELON kept read-only for 1 year; downgrade path documented (export LON → re-import ELON, format mapped reverse) |

---

## §7 — Open decisions

**All Phase 17 blocking decisions resolved 2026-05-11** (see BLUEPRINT §11 „Resolved" subsection):

- ✅ **Q11.1** — Edit-in-place while no Shipments; soft-lock after first Shipment; Administrator override required for structural changes.
- ✅ **Q11.2** — Guarantee ceiling override = Administrator role only; mandatory `Reason` field in audit.
- ✅ **Q11.3** — Per-line currency with sticky-prefill from last-entered row + bulk „Смени валута на цел документ" toolbar action (BLUEPRINT §7.3.1).

**New Phase 17 task derived from Q11.3 answer:**

- [ ] 17.0 — Implement `useStickyDefaults` React hook + line-table bulk currency change action (BLUEPRINT §7.3.1). Reused by E3 (IM declaration lines), E5 (BOM lines), E8 (EX lines). Add to AGENT-PROMPTS §E0.

Future decisions surface here as they arise.

---

*End of PLAN.md v1 (2026-05-11)*
