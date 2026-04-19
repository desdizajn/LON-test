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
  getInventory: (itemId?: string, locationId?: string) =>
    api.get('/WMS/inventory', { params: { itemId, locationId } }),

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
  
  // Receipts
  getReceipts: (page: number = 1, pageSize: number = 20) => 
    api.get('/WMS/receipts', { params: { page, pageSize } }),
  getReceipt: (id: string) => 
    api.get(`/WMS/receipts/${id}`),
  createReceipt: (data: any) => 
    api.post('/WMS/receipts', data),
  
  // Shipments
  getShipments: (page: number = 1, pageSize: number = 20) => 
    api.get('/WMS/shipments', { params: { page, pageSize } }),
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
  updateQualityStatus: (data: any) => 
    api.post('/WMS/inventory/quality-status', data),
  
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
};

export const productionApi = {
  // Production Orders
  getOrders: (status?: string) => 
    api.get('/Production/orders', { params: { status } }),
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
};

export const customsApi = {
  // Declarations
  getDeclarations: (isCleared?: boolean) => 
    api.get('/Customs/declarations', { params: { isCleared } }),
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
};

