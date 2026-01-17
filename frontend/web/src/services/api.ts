import axios from 'axios';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5000/api';

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
  createTransfer: (data: any) => 
    api.post('/WMS/transfers', data),
  
  // Quality Status
  updateQualityStatus: (data: any) => 
    api.post('/WMS/inventory/quality-status', data),
  
  // Cycle Count
  createCycleCount: (data: any) => 
    api.post('/WMS/cycle-counts', data),
  getCycleCounts: () => 
    api.get('/WMS/cycle-counts'),
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
};

export const guaranteeApi = {
  // Accounts
  getAccounts: () => api.get('/Guarantee/accounts'),
  getAccount: (id: string) => api.get(`/Guarantee/accounts/${id}`),
  
  // Ledger
  getLedger: (accountId?: string, isReleased?: boolean) => 
    api.get('/Guarantee/ledger', { params: { accountId, isReleased } }),
  createDebit: (data: any) => 
    api.post('/Guarantee/debit', data),
  createCredit: (data: any) => 
    api.post('/Guarantee/credit', data),
  
  // Active Guarantees
  getActiveGuarantees: () => api.get('/Guarantee/active-guarantees'),
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
  getItems: (search?: string) => 
    api.get('/MasterData/items', { params: { search } }),
  getWarehouses: () => api.get('/MasterData/warehouses'),
  getLocations: (warehouseId?: string) => 
    api.get('/MasterData/locations', { params: { warehouseId } }),
  getPartners: (type?: string) => 
    api.get('/MasterData/partners', { params: { type } }),
  getEmployees: () => api.get('/MasterData/employees'),
  getWorkCenters: () => api.get('/MasterData/work-centers'),
  getUoM: () => api.get('/MasterData/uom'),
};

export const knowledgeBaseApi = {
  // RAG - Ask Questions
  ask: (question: string, context?: string) => 
    api.post('/KnowledgeBase/ask', { question, context }),
  
  // Explain Field
  explain: (field: string, context?: string) => 
    api.post('/KnowledgeBase/explain', { field, context }),
  
  // Semantic Search
  search: (query: string, topK: number = 5) => 
    api.post('/KnowledgeBase/search', { query, topK }),
  
  // Health & Stats
  getHealth: () => api.get('/KnowledgeBase/health'),
  getStats: () => api.get('/KnowledgeBase/stats'),
};
