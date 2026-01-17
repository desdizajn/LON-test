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
  getInventory: (itemId?: string, locationId?: string) => 
    api.get('/WMS/inventory', { params: { itemId, locationId } }),
  getReceipts: (page: number = 1, pageSize: number = 20) => 
    api.get('/WMS/receipts', { params: { page, pageSize } }),
  getShipments: (page: number = 1, pageSize: number = 20) => 
    api.get('/WMS/shipments', { params: { page, pageSize } }),
  getPickTasks: (status?: string) => 
    api.get('/WMS/pick-tasks', { params: { status } }),
};

export const productionApi = {
  getOrders: (status?: string) => 
    api.get('/Production/orders', { params: { status } }),
  getOrder: (id: string) => 
    api.get(`/Production/orders/${id}`),
  getMaterialIssues: (orderId: string) => 
    api.get(`/Production/orders/${orderId}/material-issues`),
  getReceipts: (orderId: string) => 
    api.get(`/Production/orders/${orderId}/receipts`),
  getBOMs: (itemId?: string) => 
    api.get('/Production/boms', { params: { itemId } }),
};

export const customsApi = {
  getDeclarations: (isCleared?: boolean) => 
    api.get('/Customs/declarations', { params: { isCleared } }),
  getDeclaration: (id: string) => 
    api.get(`/Customs/declarations/${id}`),
  getProcedures: () => 
    api.get('/Customs/procedures'),
  getMRNRegistry: (mrn?: string, isActive?: boolean) => 
    api.get('/Customs/mrn-registry', { params: { mrn, isActive } }),
  getMRNByNumber: (mrn: string) => 
    api.get(`/Customs/mrn-registry/${mrn}`),
};

export const guaranteeApi = {
  getAccounts: () => api.get('/Guarantee/accounts'),
  getAccount: (id: string) => api.get(`/Guarantee/accounts/${id}`),
  getLedger: (accountId?: string, isReleased?: boolean) => 
    api.get('/Guarantee/ledger', { params: { accountId, isReleased } }),
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
