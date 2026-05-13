import axios from 'axios';

const API_BASE_URL = process.env.REACT_APP_API_URL || '/api';

export const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('auth_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error?.response?.status === 401) {
      localStorage.removeItem('auth_token');
      localStorage.removeItem('auth_expires_at');
      localStorage.removeItem('user');
    }
    return Promise.reject(error);
  }
);

export const analyticsApi = {
  getDashboard: () => api.get('/Analytics/dashboard'),
  getProductionKPI: (fromDate?: string, toDate?: string) => 
    api.get('/Analytics/production-kpi', { params: { fromDate, toDate } }),
  getWMSKPI: (fromDate?: string, toDate?: string) => 
    api.get('/Analytics/wms-kpi', { params: { fromDate, toDate } }),
  getGuaranteeExposure: () => api.get('/Analytics/guarantee-exposure'),
  getCustomsSummary: (fromDate?: string, toDate?: string) => 
    api.get('/Analytics/customs-summary', { params: { fromDate, toDate } }),
};

export const wmsApi = {
  // Inventory
  // Phase 17 §E6 — added warehouseId / clientOrderId / unassignedOnly / assignedProducerId
  // filters so the Podelba dialog and Materials tab can scope server-side.
  getInventory: (
    itemId?: string,
    locationId?: string,
    extras?: {
      warehouseId?: string | null;
      clientOrderId?: string | null;
      unassignedOnly?: boolean | null;
      assignedProducerId?: string | null;
    },
  ) =>
    api.get('/WMS/inventory', {
      params: {
        itemId,
        locationId,
        warehouseId: extras?.warehouseId ?? undefined,
        clientOrderId: extras?.clientOrderId ?? undefined,
        unassignedOnly: extras?.unassignedOnly ?? undefined,
        assignedProducerId: extras?.assignedProducerId ?? undefined,
      },
    }),

  // P4.3 — MozniMinusi (negative-stock reconciliation)
  getMozniMinusi: () => api.get('/WMS/inventory/mozni-minusi'),

  // P5.2.2 — one-click move of every balance for a batch to a target stage
  moveBatch: (payload: {
    batchNumber: string;
    targetStage: number;
    warehouseId?: string | null;
    targetLocationId?: string | null;
    reason?: string | null;
  }) => api.post('/WMS/inventory/move-batch', payload),

  // P5.2.7 — filter inventory + preview matches before bulk transfer
  massTransferPreview: (payload: {
    itemId?: string | null;
    batchNumber?: string | null;
    mrn?: string | null;
    sourceWarehouseId?: string | null;
    sourceLocationId?: string | null;
    qualityStatus?: number | null;
    lonProcessState?: number | null;
    targetLocationId?: string | null;
  }) => api.post('/WMS/inventory/mass-transfer/preview', payload),

  // P14.7 — bulk move N selected balances to a single target location.
  // Selection-based companion to massTransfer (which is predicate-based).
  bulkMoveBalances: (payload: {
    balanceIds: string[];
    targetLocationId: string;
    reason?: string | null;
  }) => api.post('/WMS/inventory/bulk-move-balances', payload),

  // Phase 17 §E6 — assign N balances to ONE sub-contractor producer; partial
  // quantities allowed; sources keep their remainder.
  podelbaToProducer: (payload: {
    producerId: string;
    clientOrderId?: string | null;
    reason?: string | null;
    lines: Array<{ sourceBalanceId: string; quantity: number }>;
  }) => api.post('/WMS/inventory/podelba-to-producer', payload),

  // P5.2.7 — bulk transfer every inventory row matching the filter to
  // a single explicit target location in one atomic call.
  massTransfer: (payload: {
    targetLocationId: string;
    itemId?: string | null;
    batchNumber?: string | null;
    mrn?: string | null;
    sourceWarehouseId?: string | null;
    sourceLocationId?: string | null;
    qualityStatus?: number | null;
    lonProcessState?: number | null;
    reason?: string | null;
  }) => api.post('/WMS/inventory/mass-transfer', payload),
  
  // Receipts
  // Phase 17 §E4 — `clientOrderId` filter added; joins receipt.lines.customsDeclaration.clientOrderId server-side.
  getReceipts: (params?: { page?: number; pageSize?: number; clientOrderId?: string }) =>
    api.get('/WMS/receipts', { params: params ?? { page: 1, pageSize: 20 } }),
  getReceipt: (id: string) => 
    api.get(`/WMS/receipts/${id}`),
  createReceipt: (data: any) =>
    api.post('/WMS/receipts', data),
  /**
   * P5.2.3 — bulk receipt from an existing customs declaration. Explodes
   * every declaration line into a receipt line in one atomic commit.
   */
  bulkReceiptFromDeclaration: (data: {
    customsDeclarationId: string;
    warehouseId: string;
    targetLocationId?: string | null;
    receiptDate?: string | null;
    referenceNumber?: string | null;
  }) => api.post('/WMS/receipts/bulk-from-declaration', data),
  /**
   * P5.2.4 — bulk shipment from FG selection. Runs the FG predicate, emits a
   * Shipment, drains the matched balances, and (optionally) creates an EX
   * declaration against the shared source MRN.
   */
  bulkShipmentFromFG: (data: {
    itemId?: string | null;
    batchNumber?: string | null;
    mrn?: string | null;
    locationId?: string | null;
    sourceWarehouseId?: string | null;
    productionOrderId?: string | null;
    partnerId?: string | null;
    customsProcedureId?: string | null;
    declarationNumber?: string | null;
    shipmentDate?: string | null;
    reference?: string | null;
    createExportDeclaration?: boolean;
    /** Phase 17 §E8 — stamps Shipment + chained EX declaration with this id. */
    clientOrderId?: string | null;
  }) => api.post('/WMS/shipments/bulk-from-fg', data),

  // Shipments
  // Phase 17 §E8 — accepts either the legacy positional (page, pageSize) call
  // or an object with optional clientOrderId filter for the hub Shipments tab.
  getShipments: (
    pageOrParams?: number | { page?: number; pageSize?: number; clientOrderId?: string },
    pageSize?: number,
  ) => {
    const params =
      typeof pageOrParams === 'object' && pageOrParams !== null
        ? pageOrParams
        : { page: pageOrParams ?? 1, pageSize: pageSize ?? 20 };
    return api.get('/WMS/shipments', { params });
  },
  getShipment: (id: string) => 
    api.get(`/WMS/shipments/${id}`),
  createShipment: (data: any) => 
    api.post('/WMS/shipments', data),
  
  // Pick Tasks
  getPickTasks: (status?: string) => 
    api.get('/WMS/pick-tasks', { params: { status } }),
  getPickTask: (id: string) => 
    api.get(`/WMS/pick-tasks/${id}`),
  createPickTask: (data: any) => 
    api.post('/WMS/pick-tasks', data),
  assignPickTask: (id: string, employeeId: string) => 
    api.post(`/WMS/pick-tasks/${id}/assign`, { employeeId }),
  completePickTask: (id: string, quantityPicked: number) => 
    api.post(`/WMS/pick-tasks/${id}/complete`, { quantityPicked }),
  
  // Transfers
  getTransfers: (page: number = 1, pageSize: number = 20) => 
    api.get('/WMS/transfers', { params: { page, pageSize } }),
  createTransfer: (data: any) => 
    api.post('/WMS/transfers', data),
  
  // Quality Status
  // Phase 17 §E8 — backend accepts both legacy `inventoryBalanceId` (used by
  // QcHold / BlockedInventory) and the shorter `balanceId` (used by the new
  // hub QC dialog). Optional notes carry the audit trail.
  updateQualityStatus: (data: {
    inventoryBalanceId?: string;
    balanceId?: string;
    newQualityStatus: number;
    reason?: string | null;
    notes?: string | null;
  }) => api.post('/WMS/inventory/quality-status', data),
  
  // Cycle Count
  getCycleCounts: (status?: string) => 
    api.get('/WMS/cycle-counts', { params: { status } }),
  createCycleCount: (data: any) => 
    api.post('/WMS/cycle-counts', data),
  getCycleCount: (id: string) => 
    api.get(`/WMS/cycle-counts/${id}`),
  
  // Adjustments
  createAdjustment: (data: any) =>
    api.post('/WMS/adjustments', data),
  getAdjustments: () =>
    api.get('/WMS/adjustments'),

  // P15.3 — Skart (defective-on-intake) register
  reportSkart: (data: { receiptLineId: string; skartQuantity: number; reason: string }) =>
    api.post('/WMS/skart', data),
  getSkart: (params?: { openOnly?: boolean; itemId?: string; mrn?: string }) =>
    api.get('/WMS/skart', { params }),
  resolveSkart: (id: string, data: { resolution: number; resolutionNote?: string }) =>
    api.post(`/WMS/skart/${id}/resolve`, data),
};

// Phase 17 §E6 — AI helper "smart suggestion" surface. Stub today (deterministic
// heuristics in `SuggestionsController`); §E10 swaps in `AiAssistantService`.
export const suggestionsApi = {
  producer: (clientOrderId?: string | null) =>
    api.get('/Suggestions/producer', { params: { clientOrderId } }),
};

// Phase 17 §E7.6 (D5) — DeliveryNote (legacy „Propratnica") CRUD. Most rows
// are auto-created in `Draft` by parent commits (MaterialIssue / Shipment);
// these helpers wire up the operator-facing list + detail.
export const logisticsApi = {
  getDeliveryNotes: (params?: {
    type?: number | null;
    status?: number | null;
    partnerId?: string | null;
    from?: string | null;
    to?: string | null;
    page?: number;
    pageSize?: number;
  }) => api.get('/Logistics/delivery-notes', { params }),
  getDeliveryNote: (id: string) =>
    api.get(`/Logistics/delivery-notes/${id}`),
  updateDeliveryNote: (id: string, payload: {
    driverName?: string | null;
    vehicleRegistration?: string | null;
    remarks?: string | null;
    dispatchDate?: string | null;
  }) => api.put(`/Logistics/delivery-notes/${id}`, payload),
  confirmDeliveryNote: (id: string) =>
    api.post(`/Logistics/delivery-notes/${id}/confirm`),
  cancelDeliveryNote: (id: string, reason: string | null) =>
    api.post(`/Logistics/delivery-notes/${id}/cancel`, { reason }),
};

export const productionApi = {
  // Production Orders
  // Phase 17 §E5 — clientOrderId filter added so the hub Production tab can list POs.
  getOrders: (params?: { status?: string; clientOrderId?: string }) =>
    api.get('/Production/orders', { params }),
  getOrder: (id: string) => 
    api.get(`/Production/orders/${id}`),
  createOrder: (data: any) => 
    api.post('/Production/orders', data),
  updateOrderStatus: (id: string, status: number) => 
    api.put(`/Production/orders/${id}/status`, { status }),
  
  // Material Issues
  getMaterialIssues: (orderId: string) => 
    api.get(`/Production/orders/${orderId}/material-issues`),
  createMaterialIssue: (data: any) => 
    api.post('/Production/material-issues', data),
  
  // Production Receipts
  getReceipts: (orderId: string) =>
    api.get(`/Production/orders/${orderId}/receipts`),
  createProductionReceipt: (data: any) =>
    api.post('/Production/receipts', data),
  /**
   * Phase 17 §E7 — hits the canonical controller route
   * `POST /api/Production/orders/{id}/receipts`. The legacy
   * `createProductionReceipt` above hits `/Production/receipts` which doesn't
   * match the controller; left in place for backwards compat with the
   * standalone ProductionReceiptForm.
   */
  createReceiptForOrder: (orderId: string, payload: {
    receiptDate: string;
    itemId: string;
    quantity: number;
    scrapQuantity?: number | null;
    uoMId: string;
    locationId: string;
    batchNumber: string;
    qualityStatus?: number | null;
    receivedByEmployeeId?: string | null;
  }) => api.post(`/Production/orders/${orderId}/receipts`, payload),
  
  // Scrap Report
  reportScrap: (data: any) => 
    api.post('/Production/scrap', data),
  
  // BOMs
  getBOMs: (itemId?: string) =>
    api.get('/Production/boms', { params: { itemId } }),

  // Operations
  updateOperation: (id: string, data: any) =>
    api.put(`/Production/operations/${id}`, data),

  // P5.2.6 — Release PO (Draft → Released; expands BOM + Routing)
  releaseOrder: (id: string) =>
    api.post(`/Production/orders/${id}/release`),

  // P5.2.1 — Bulk issue all remaining materials (FEFO auto-pick)
  issueAllMaterials: (id: string, issueDate: string, issuedByEmployeeId?: string) =>
    api.post(`/Production/orders/${id}/issues/bulk`, { issueDate, issuedByEmployeeId }),

  // P8.5 — Material shortage aggregate for active production orders
  getShortage: () => api.get('/Production/shortage'),
};

// P11.1–P11.5 — machine operations (state, downtime, maintenance)
export const machinesApi = {
  // State events
  logState: (machineId: string, payload: {
    state: number;
    changedAt?: string | null;
    changedByEmployeeId?: string | null;
    notes?: string | null;
  }) => api.post(`/Machines/${machineId}/state-events`, payload),
  getCurrentStates: () => api.get('/Machines/current-states'),

  // Downtime
  logDowntime: (payload: {
    machineId: string;
    start: string;
    end?: string | null;
    category: number;
    reason: string;
    costImpact?: number | null;
    reportedByEmployeeId?: string | null;
  }) => api.post('/Machines/downtime', payload),
  closeDowntime: (id: string, end: string) =>
    api.post(`/Machines/downtime/${id}/close`, { end }),
  getDowntime: (params?: { machineId?: string; from?: string; to?: string }) =>
    api.get('/Machines/downtime', { params }),
  getDowntimePareto: (params?: { from?: string; to?: string }) =>
    api.get('/Machines/downtime/pareto', { params }),

  // Maintenance schedules
  createSchedule: (payload: {
    machineId: string;
    taskDescription: string;
    intervalDays: number;
    lastDone?: string | null;
    nextDue?: string | null;
  }) => api.post('/Machines/maintenance-schedules', payload),
  updateSchedule: (id: string, payload: {
    taskDescription: string;
    intervalDays: number;
    lastDone?: string | null;
    nextDue: string;
    isActive: boolean;
  }) => api.put(`/Machines/maintenance-schedules/${id}`, payload),
  getSchedules: (activeOnly: boolean = true) =>
    api.get('/Machines/maintenance-schedules', { params: { activeOnly } }),

  // Maintenance work orders
  createWorkOrder: (payload: {
    machineId: string;
    scheduleId?: string | null;
    scheduledDate: string;
    technicianEmployeeId?: string | null;
    taskDescription?: string | null;
    notes?: string | null;
    costImpact?: number | null;
  }) => api.post('/Machines/maintenance-work-orders', payload),
  completeWorkOrder: (id: string, payload: {
    completedAt?: string | null;
    notes?: string | null;
    costImpact?: number | null;
  }) => api.post(`/Machines/maintenance-work-orders/${id}/complete`, payload),
  getWorkOrders: (params?: { machineId?: string; openOnly?: boolean }) =>
    api.get('/Machines/maintenance-work-orders', { params }),
};

// P9.1/9.6 — Finished Goods simple queries
export const finishedGoodsApi = {
  getAwaitingPack: () => api.get('/FinishedGoods/awaiting-pack'),
  getPackagingStock: () => api.get('/FinishedGoods/packaging-stock'),
};

// P10.1/10.2/10.5 — HR operations (attendance, absences, operator assignments)
export const hrApi = {
  // Attendance
  clockIn: (payload: { employeeId: string; at?: string | null }) =>
    api.post('/Hr/attendance/clock-in', payload),
  clockOut: (payload: { employeeId: string; at?: string | null }) =>
    api.post('/Hr/attendance/clock-out', payload),
  getAttendanceToday: (day?: string) =>
    api.get('/Hr/attendance/today', { params: { day } }),
  getAttendanceHistory: (params?: { employeeId?: string; from?: string; to?: string }) =>
    api.get('/Hr/attendance', { params }),

  // Absences
  createAbsence: (payload: {
    employeeId: string;
    from: string;
    to: string;
    type: number;
    reason?: string | null;
  }) => api.post('/Hr/absences', payload),
  decideAbsence: (id: string, approve: boolean) =>
    api.post(`/Hr/absences/${id}/decide`, { approve }),
  getAbsences: (params?: { employeeId?: string; pendingOnly?: boolean }) =>
    api.get('/Hr/absences', { params }),

  // Operator-machine assignments
  createAssignment: (payload: {
    employeeId: string;
    machineId: string;
    validFrom: string;
    validTo?: string | null;
    notes?: string | null;
  }) => api.post('/Hr/assignments', payload),
  endAssignment: (id: string, validTo: string) =>
    api.post(`/Hr/assignments/${id}/end`, { validTo }),
  getAssignments: (params?: { employeeId?: string; machineId?: string; activeOnly?: boolean }) =>
    api.get('/Hr/assignments', { params }),

  // P16.C2 — certifications
  getCertifications: (employeeId?: string) =>
    api.get('/Hr/certifications', { params: { employeeId } }),
  getExpiringCertifications: (withinDays = 30) =>
    api.get('/Hr/certifications/expiring', { params: { withinDays } }),
  createCertification: (data: any) =>
    api.post('/Hr/certifications', data),
  updateCertification: (id: string, data: any) =>
    api.put(`/Hr/certifications/${id}`, data),
  deleteCertification: (id: string) =>
    api.delete(`/Hr/certifications/${id}`),
};

// Phase 17 §E1 — ClientOrder CRUD (consumed by §E2 hub UI)
export const clientOrdersApi = {
  list: (params?: {
    status?: number;
    customerPartnerId?: string;
    fromDate?: string;
    toDate?: string;
    includeCancelled?: boolean;
  }) => api.get('/ClientOrders', { params }),
  get: (id: string) => api.get(`/ClientOrders/${id}`),
  create: (payload: {
    customerPartnerId: string;
    lonAuthorizationId: string;
    customerOrderReference?: string | null;
    orderDate?: string | null;
    requestedShipDate?: string | null;
    notes?: string | null;
  }) => api.post('/ClientOrders', payload),
  update: (
    id: string,
    payload: {
      customerOrderReference?: string | null;
      requestedShipDate?: string | null;
      notes?: string | null;
    },
  ) => api.put(`/ClientOrders/${id}`, { id, ...payload }),
  cancel: (id: string, reason: string) =>
    api.post(`/ClientOrders/${id}/cancel`, { id, reason }),
  // Phase 17 §E5 — add a ClientOrderFinishedGood row from the hub BOM dialog.
  addFinishedGood: (id: string, payload: {
    itemId: string;
    quantity: number;
    uoMId: string;
    bomId?: string | null;
    unitPriceForeign?: number | null;
    currency?: string | null;
    notes?: string | null;
  }) => api.post(`/ClientOrders/${id}/finished-goods`, { clientOrderId: id, ...payload }),
  /**
   * Phase 17 §E8 — list of finished-goods (defined on this ClientOrder) joined
   * with their current shippable InventoryBalance rows (non-Blocked, qty>0).
   * Powers the EX wizard's FG picker.
   */
  getAvailableFinishedGoods: (id: string) =>
    api.get(`/ClientOrders/${id}/available-fgs`),
};

// P13.1 / P13.3 / P13.5 — Management KPIs (on-time, by-customer, alerts)
// P16.C1 — risks/escalations CRUD
export const managementApi = {
  getOnTime: (params?: { from?: string; to?: string }) =>
    api.get('/Management/on-time', { params }),
  getByCustomer: (params?: { from?: string; to?: string }) =>
    api.get('/Management/by-customer', { params }),
  getAlerts: () => api.get('/Management/alerts'),
  // Risks / Escalations
  getRisks: (kind?: 1 | 2) =>
    api.get('/Management/risks', { params: { kind } }),
  getRisk: (id: string) =>
    api.get(`/Management/risks/${id}`),
  createRisk: (data: any) =>
    api.post('/Management/risks', data),
  updateRisk: (id: string, data: any) =>
    api.put(`/Management/risks/${id}`, data),
  deleteRisk: (id: string) =>
    api.delete(`/Management/risks/${id}`),
};

// P12.2 / P12.3 — Finance (contracts, rate cards, invoices)
export const financeApi = {
  // Contracts
  getContracts: (params?: { partnerId?: string; activeOnly?: boolean }) =>
    api.get('/Finance/contracts', { params }),
  getContract: (id: string) => api.get(`/Finance/contracts/${id}`),
  createContract: (payload: {
    number: string;
    partnerId: string;
    validFrom: string;
    validTo?: string | null;
    paymentTermsDays: number;
    currency?: string | null;
    notes?: string | null;
    rateCard?: Array<{
      rateType: number;
      itemId?: string | null;
      operationCode?: string | null;
      ratePerUnit: number;
      currency?: string | null;
      validFrom: string;
      validTo?: string | null;
      notes?: string | null;
    }>;
  }) => api.post('/Finance/contracts', payload),
  updateContract: (id: string, payload: {
    validTo?: string | null;
    paymentTermsDays: number;
    isActive: boolean;
    notes?: string | null;
  }) => api.put(`/Finance/contracts/${id}`, payload),
  upsertRate: (contractId: string, payload: {
    entryId?: string | null;
    rateType: number;
    itemId?: string | null;
    operationCode?: string | null;
    ratePerUnit: number;
    currency?: string | null;
    validFrom: string;
    validTo?: string | null;
    notes?: string | null;
  }) => api.post(`/Finance/contracts/${contractId}/rates`, payload),
  deleteRate: (contractId: string, entryId: string) =>
    api.delete(`/Finance/contracts/${contractId}/rates/${entryId}`),

  // Invoices
  getInvoices: (params?: {
    partnerId?: string;
    status?: number;
    from?: string;
    to?: string;
  }) => api.get('/Finance/invoices', { params }),
  getInvoice: (id: string) => api.get(`/Finance/invoices/${id}`),
  createInvoice: (payload: {
    partnerId: string;
    contractId?: string | null;
    issueDate?: string | null;
    dueDate?: string | null;
    currency?: string | null;
    notes?: string | null;
    lines?: Array<{
      description: string;
      itemId?: string | null;
      relatedProductionOrderId?: string | null;
      relatedShipmentId?: string | null;
      quantity: number;
      unitPrice: number;
    }>;
  }) => api.post('/Finance/invoices', payload),
  addLine: (id: string, payload: {
    description: string;
    itemId?: string | null;
    relatedProductionOrderId?: string | null;
    relatedShipmentId?: string | null;
    quantity: number;
    unitPrice: number;
  }) => api.post(`/Finance/invoices/${id}/lines`, payload),
  removeLine: (id: string, lineId: string) =>
    api.delete(`/Finance/invoices/${id}/lines/${lineId}`),
  generateFromPo: (payload: {
    productionOrderId: string;
    contractId?: string | null;
    overrideUnitPrice?: number | null;
    issueDate?: string | null;
  }) => api.post('/Finance/invoices/generate-from-po', payload),
  issue: (id: string) => api.post(`/Finance/invoices/${id}/issue`),
  markPaid: (id: string, paidAt?: string | null) =>
    api.post(`/Finance/invoices/${id}/mark-paid`, { paidAt: paidAt ?? null }),
  cancel: (id: string, reason?: string | null) =>
    api.post(`/Finance/invoices/${id}/cancel`, { reason: reason ?? null }),

  // P16.C3.a — cost rates
  getCostRates: (scope?: 1 | 2 | 3 | 4 | 5) =>
    api.get('/Finance/cost-rates', { params: { scope } }),
  createCostRate: (data: any) =>
    api.post('/Finance/cost-rates', data),
  updateCostRate: (id: string, data: any) =>
    api.put(`/Finance/cost-rates/${id}`, data),
  deleteCostRate: (id: string) =>
    api.delete(`/Finance/cost-rates/${id}`),

  // P16.C3.b — payroll periods
  getPayrollPeriods: () =>
    api.get('/Finance/payroll-periods'),
  getPayrollPeriod: (id: string) =>
    api.get(`/Finance/payroll-periods/${id}`),
  createPayrollPeriod: (data: any) =>
    api.post('/Finance/payroll-periods', data),
  updatePayrollLine: (id: string, data: any) =>
    api.put(`/Finance/payroll-periods/lines/${id}`, data),
  finalizePayrollPeriod: (id: string) =>
    api.post(`/Finance/payroll-periods/${id}/finalize`),
  exportPayrollPeriod: (id: string) =>
    api.post(`/Finance/payroll-periods/${id}/export`),

  // P16.C3.c — supplier invoices
  getSupplierInvoices: (status?: 1 | 2 | 3 | 4) =>
    api.get('/Finance/supplier-invoices', { params: { status } }),
  getSupplierInvoice: (id: string) =>
    api.get(`/Finance/supplier-invoices/${id}`),
  createSupplierInvoice: (data: any) =>
    api.post('/Finance/supplier-invoices', data),
  updateSupplierInvoice: (id: string, data: any) =>
    api.put(`/Finance/supplier-invoices/${id}`, data),
  deleteSupplierInvoice: (id: string) =>
    api.delete(`/Finance/supplier-invoices/${id}`),
};

export const customsApi = {
  // Declarations
  getDeclarations: (params?: { isCleared?: boolean; clientOrderId?: string }) =>
    api.get('/Customs/declarations', { params }),
  getDeclaration: (id: string) => 
    api.get(`/Customs/declarations/${id}`),
  createDeclaration: (data: any) => 
    api.post('/Customs/declarations', data),
  updateDeclaration: (id: string, data: any) => 
    api.put(`/Customs/declarations/${id}`, data),
  
  // Procedures
  getProcedures: () =>
    api.get('/Customs/procedures'),

  // LON Authorizations (Одобренија за IM 4200 / 5100)
  getLONAuthorizations: (activeOnly: boolean = true) =>
    api.get('/Customs/lon-authorizations', { params: { activeOnly } }),

  // MRN Registry
  getMRNRegistry: (mrn?: string, isActive?: boolean) => 
    api.get('/Customs/mrn-registry', { params: { mrn, isActive } }),
  getMRNByNumber: (mrn: string) => 
    api.get(`/Customs/mrn-registry/${mrn}`),
  
  // Documents
  uploadDocument: (formData: FormData) =>
    api.post('/Customs/documents', formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    }),

  // P4.1 — Zaverka (customs certification)
  certifyDeclaration: (id: string, zaverkaNumber: string, zaverkaDate: string) =>
    api.post(`/Customs/declarations/${id}/certify`, { zaverkaNumber, zaverkaDate }),

  // P4.2 — PEE060 monthly Zadolzuvanje/Razdolzuvanje XML download
  generatePee060: (authorizationId: string, from: string, to: string) =>
    api.get('/Customs/pee/060', {
      params: { authorizationId, from, to },
      responseType: 'blob',
    }),

  // P2.6c + P4.6 — Waste declaration with optional 4 slots + Zaguba
  createWasteDeclaration: (data: any) =>
    api.post('/Customs/declarations/waste', data),

  // P2.6a — Re-export (EX / 3151)
  createExportDeclaration: (data: any) =>
    api.post('/Customs/declarations/export', data),

  // P2.6b — Return (6121) — reverses a prior EX
  createReturnDeclaration: (data: any) =>
    api.post('/Customs/declarations/return', data),
};

// Phase 17 §E8.5 (D4) — CommercialInvoice (customs commercial invoice that
// accompanies an EX). Distinct from sales `Invoice` (Teksport billing customer
// for processing labor) — see BLUEPRINT §3.2.1.
export const commercialInvoicesApi = {
  getList: (params?: {
    clientOrderId?: string;
    consigneePartnerId?: string;
    status?: number;
    from?: string;
    to?: string;
    page?: number;
    pageSize?: number;
  }) => api.get('/Customs/commercial-invoices', { params }),
  getById: (id: string) => api.get(`/Customs/commercial-invoices/${id}`),
  create: (data: any) => api.post('/Customs/commercial-invoices', data),
  update: (id: string, data: any) =>
    api.put(`/Customs/commercial-invoices/${id}`, data),
  remove: (id: string) => api.delete(`/Customs/commercial-invoices/${id}`),
  issue: (id: string) => api.post(`/Customs/commercial-invoices/${id}/issue`),
  cancel: (id: string, reason?: string) =>
    api.post(`/Customs/commercial-invoices/${id}/cancel`, { reason }),
  suggestFromShipment: (shipmentId: string) =>
    api.post('/Customs/commercial-invoices/suggest-from-shipment', null, {
      params: { shipmentId },
    }),
  pdfUrl: (id: string) =>
    `${(api.defaults.baseURL ?? '').replace(/\/$/, '')}/Customs/commercial-invoices/${id}/pdf`,
};

export const guaranteeApi = {
  // Accounts
  getAccounts: () => api.get('/Guarantee/accounts'),
  getAccount: (id: string) => api.get(`/Guarantee/accounts/${id}`),
  createAccount: (data: any) => api.post('/Guarantee/accounts', data),
  updateAccount: (id: string, data: any) => api.put(`/Guarantee/accounts/${id}`, data),
  deleteAccount: (id: string) => api.delete(`/Guarantee/accounts/${id}`),
  
  // Ledger
  getLedger: (accountId?: string, isReleased?: boolean) => 
    api.get('/Guarantee/ledger', { params: { accountId, isReleased } }),
  createLedgerEntry: (data: any) => 
    api.post('/Guarantee/ledger', data),
  releaseEntry: (id: string) => 
    api.put(`/Guarantee/ledger/${id}/release`),
  createDebit: (data: any) => 
    api.post('/Guarantee/debit', data),
  createCredit: (data: any) => 
    api.post('/Guarantee/credit', data),
  
  // Active Guarantees
  getActiveGuarantees: () => api.get('/Guarantee/active-guarantees'),

  // P4.4 — Traffic-light indicator per guarantee account
  getTrafficLights: () => api.get('/Guarantee/accounts/traffic-light'),
};

export const traceabilityApi = {
  traceForward: (batchNumber?: string, mrn?: string) => 
    api.get('/Traceability/trace-forward', { params: { batchNumber, mrn } }),
  traceBackward: (batchNumber?: string, mrn?: string) => 
    api.get('/Traceability/trace-backward', { params: { batchNumber, mrn } }),
  getGenealogy: (batchNumber: string) => 
    api.get(`/Traceability/genealogy/${batchNumber}`),
  traceFullPath: (batchNumber: string) => 
    api.get('/Traceability/trace-full', { params: { batchNumber } }),
};

export const masterDataApi = {
  // Items
  getItems: (search?: string) => 
    api.get('/MasterData/items', { params: { search } }),
  getItem: (id: string) => 
    api.get(`/MasterData/items/${id}`),
  createItem: (data: any) => 
    api.post('/MasterData/items', data),
  updateItem: (id: string, data: any) => 
    api.put(`/MasterData/items/${id}`, data),
  deleteItem: (id: string) => 
    api.delete(`/MasterData/items/${id}`),
  
  // Warehouses
  getWarehouses: () => api.get('/MasterData/warehouses'),
  getWarehouse: (id: string) => 
    api.get(`/MasterData/warehouses/${id}`),
  createWarehouse: (data: any) => 
    api.post('/MasterData/warehouses', data),
  updateWarehouse: (id: string, data: any) => 
    api.put(`/MasterData/warehouses/${id}`, data),
  deleteWarehouse: (id: string) => 
    api.delete(`/MasterData/warehouses/${id}`),
  
  // Locations
  getLocations: (warehouseId?: string) => 
    api.get('/MasterData/locations', { params: { warehouseId } }),
  getLocation: (id: string) => 
    api.get(`/MasterData/locations/${id}`),
  createLocation: (data: any) => 
    api.post('/MasterData/locations', data),
  updateLocation: (id: string, data: any) => 
    api.put(`/MasterData/locations/${id}`, data),
  deleteLocation: (id: string) => 
    api.delete(`/MasterData/locations/${id}`),
  
  // Partners
  getPartners: (type?: string) => 
    api.get('/MasterData/partners', { params: { type } }),
  getPartner: (id: string) => 
    api.get(`/MasterData/partners/${id}`),
  createPartner: (data: any) => 
    api.post('/MasterData/partners', data),
  updatePartner: (id: string, data: any) => 
    api.put(`/MasterData/partners/${id}`, data),
  deletePartner: (id: string) => 
    api.delete(`/MasterData/partners/${id}`),
  
  // Employees
  getEmployees: () => api.get('/MasterData/employees'),
  
  // Work Centers
  getWorkCenters: () => api.get('/MasterData/workcenters'),
  getWorkCenter: (id: string) => 
    api.get(`/MasterData/workcenters/${id}`),
  createWorkCenter: (data: any) => 
    api.post('/MasterData/workcenters', data),
  updateWorkCenter: (id: string, data: any) => 
    api.put(`/MasterData/workcenters/${id}`, data),
  deleteWorkCenter: (id: string) => 
    api.delete(`/MasterData/workcenters/${id}`),
  
  // Machines
  getMachines: (workCenterId?: string) => 
    api.get('/MasterData/machines', { params: { workCenterId } }),
  getMachine: (id: string) => 
    api.get(`/MasterData/machines/${id}`),
  createMachine: (data: any) => 
    api.post('/MasterData/machines', data),
  updateMachine: (id: string, data: any) => 
    api.put(`/MasterData/machines/${id}`, data),
  deleteMachine: (id: string) => 
    api.delete(`/MasterData/machines/${id}`),
  
  // UoM
  getUoM: () => api.get('/MasterData/uom'),
  createUoM: (data: any) => 
    api.post('/MasterData/uom', data),
  updateUoM: (id: string, data: any) => 
    api.put(`/MasterData/uom/${id}`, data),
  deleteUoM: (id: string) => 
    api.delete(`/MasterData/uom/${id}`),
};


export const knowledgeBaseApi = {
  // RAG - Ask Questions
  ask: (question: string, context?: string) =>
    api.post('/KnowledgeBase/ask', { question, context }),

  // Explain Field
  explain: (field: string, context?: string) =>
    api.post('/KnowledgeBase/explain', { concept: field, context }),

  // Semantic Search
  search: (query: string, topK: number = 5) =>
    api.post('/KnowledgeBase/search', { query, topK }),

  // Health & Stats
  getHealth: () => api.get('/KnowledgeBase/health'),
  getStats: () => api.get('/KnowledgeBase/stats'),

  // Browse KB Data
  getTariffCodes: (search?: string, page: number = 1, pageSize: number = 20) =>
    api.get('/KnowledgeBase/tariff-codes', { params: { search, page, pageSize } }),

  getRegulations: (search?: string, page: number = 1, pageSize: number = 20) =>
    api.get('/KnowledgeBase/regulations', { params: { search, page, pageSize } }),

  getCodeLists: (listType?: string, search?: string) =>
    api.get('/KnowledgeBase/code-lists', { params: { listType, search } }),

  /** Phase 17 §E7.5 — create a CodeListItem inline from a dropdown. */
  createCodeListItem: (payload: {
    listType: string;
    code: string;
    descriptionMK: string;
    descriptionEN?: string | null;
    sortOrder?: number;
  }) => api.post('/KnowledgeBase/code-lists/items', payload),

  getValidationRules: (fieldName?: string) =>
    api.get('/KnowledgeBase/validation-rules', { params: { fieldName } }),
};

// P5.1 — generic importer wizard API. Every call is tenant-scoped via the
// usual JWT filter; responses follow the { isSuccess, data, errorMessage }
// envelope. Multipart upload uses a separate axios config so the global
// Content-Type: application/json default doesn't override the boundary.
export const importApi = {
  uploadSession: (file: File, targetEntity?: string, partnerContextId?: string) => {
    const form = new FormData();
    form.append('file', file);
    return api.post('/Import/sessions', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      params: { targetEntity, partnerContextId },
    });
  },
  getSession: (id: string) => api.get(`/Import/sessions/${id}`),
  listSessions: (take: number = 50) => api.get('/Import/sessions', { params: { take } }),

  getTargets: () => api.get('/Import/targets'),
  getTarget: (name: string) => api.get(`/Import/targets/${encodeURIComponent(name)}`),

  applyMapping: (id: string, payload: {
    mapping: { columns: Array<{ sourceHeader: string; targetField?: string | null; ignore: boolean }> };
    targetEntity: string;
    partnerContextId?: string | null;
    saveAsProfileLabel?: string | null;
  }) => api.put(`/Import/sessions/${id}/mapping`, payload),

  suggestProfiles: (targetEntity: string, partnerContextId?: string) =>
    api.get('/Import/mapping-profiles', { params: { targetEntity, partnerContextId } }),
  deleteProfile: (id: string) => api.delete(`/Import/mapping-profiles/${id}`),

  setDefaults: (id: string, defaults: { values: Record<string, string | null | undefined> }) =>
    api.put(`/Import/sessions/${id}/defaults`, { defaults }),
  setTransforms: (id: string, transforms: { columns: Array<{ sourceHeader: string; rules: string[] }> }) =>
    api.put(`/Import/sessions/${id}/transforms`, { transforms }),
  previewTransformed: (id: string, take: number = 20) =>
    api.get(`/Import/sessions/${id}/preview-transformed`, { params: { take } }),

  dryRun: (id: string) => api.post(`/Import/sessions/${id}/dry-run`),
  commit: (id: string) => api.post(`/Import/sessions/${id}/commit`),

  // P6.34 — KW12 preset: single xlsx → 3 pre-configured sessions (Items/CustomsDeclarations/Receipts).
  uploadKw12Preset: (file: File) => {
    const form = new FormData();
    form.append('file', file);
    return api.post('/Import/presets/kw12', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
};

// P6.30/P6.31 — admin-only item backfill + per-item import-attributes drill-in.
export const itemsAdminApi = {
  backfillBaseVariants: (dryRun: boolean = true) =>
    api.post(`/MasterData/items/backfill-base-variants`, null, { params: { dryRun } }),
  getImportAttributes: (id: string) =>
    api.get(`/MasterData/items/${id}/import-attributes`),
};

