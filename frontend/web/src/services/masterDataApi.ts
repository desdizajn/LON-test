import axios from 'axios';
import type {
  Item,
  ItemFormData,
  Partner,
  PartnerFormData,
  Warehouse,
  WarehouseFormData,
  Location,
  LocationFormData,
  UoM,
  BOM,
  BOMFormData,
  Routing,
  RoutingFormData,
  WorkCenter,
  Employee,
} from '../types/masterData';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5000/api';

// Items API
export const itemsApi = {
  getAll: () => axios.get<Item[]>(`${API_BASE_URL}/MasterData/items`),
  getById: (id: string) => axios.get<Item>(`${API_BASE_URL}/MasterData/items/${id}`),
  create: (data: ItemFormData) => axios.post<Item>(`${API_BASE_URL}/MasterData/items`, data),
  update: (id: string, data: ItemFormData) =>
    axios.put<Item>(`${API_BASE_URL}/MasterData/items/${id}`, data),
  delete: (id: string) => axios.delete(`${API_BASE_URL}/MasterData/items/${id}`),
  search: (searchTerm: string) =>
    axios.get<Item[]>(`${API_BASE_URL}/MasterData/items/search?q=${searchTerm}`),
};

// Partners API
export const partnersApi = {
  getAll: () => axios.get<Partner[]>(`${API_BASE_URL}/MasterData/partners`),
  getById: (id: string) => axios.get<Partner>(`${API_BASE_URL}/MasterData/partners/${id}`),
  create: (data: PartnerFormData) =>
    axios.post<Partner>(`${API_BASE_URL}/MasterData/partners`, data),
  update: (id: string, data: PartnerFormData) =>
    axios.put<Partner>(`${API_BASE_URL}/MasterData/partners/${id}`, data),
  delete: (id: string) => axios.delete(`${API_BASE_URL}/MasterData/partners/${id}`),
  search: (searchTerm: string) =>
    axios.get<Partner[]>(`${API_BASE_URL}/MasterData/partners/search?q=${searchTerm}`),
};

// Warehouses API
export const warehousesApi = {
  getAll: () => axios.get<Warehouse[]>(`${API_BASE_URL}/MasterData/warehouses`),
  getById: (id: string) => axios.get<Warehouse>(`${API_BASE_URL}/MasterData/warehouses/${id}`),
  create: (data: WarehouseFormData) =>
    axios.post<Warehouse>(`${API_BASE_URL}/MasterData/warehouses`, data),
  update: (id: string, data: WarehouseFormData) =>
    axios.put<Warehouse>(`${API_BASE_URL}/MasterData/warehouses/${id}`, data),
  delete: (id: string) => axios.delete(`${API_BASE_URL}/MasterData/warehouses/${id}`),
};

// Locations API
export const locationsApi = {
  getAll: (warehouseId?: string) => {
    const url = warehouseId
      ? `${API_BASE_URL}/MasterData/locations?warehouseId=${warehouseId}`
      : `${API_BASE_URL}/MasterData/locations`;
    return axios.get<Location[]>(url);
  },
  getById: (id: string) => axios.get<Location>(`${API_BASE_URL}/MasterData/locations/${id}`),
  create: (data: LocationFormData) =>
    axios.post<Location>(`${API_BASE_URL}/MasterData/locations`, data),
  update: (id: string, data: LocationFormData) =>
    axios.put<Location>(`${API_BASE_URL}/MasterData/locations/${id}`, data),
  delete: (id: string) => axios.delete(`${API_BASE_URL}/MasterData/locations/${id}`),
};

// UoM API
export const uomApi = {
  getAll: () => axios.get<UoM[]>(`${API_BASE_URL}/MasterData/uom`),
  getById: (id: string) => axios.get<UoM>(`${API_BASE_URL}/MasterData/uom/${id}`),
  create: (data: Partial<UoM>) => axios.post<UoM>(`${API_BASE_URL}/MasterData/uom`, data),
  update: (id: string, data: Partial<UoM>) =>
    axios.put<UoM>(`${API_BASE_URL}/MasterData/uom/${id}`, data),
  delete: (id: string) => axios.delete(`${API_BASE_URL}/MasterData/uom/${id}`),
};

// BOMs API
export const bomsApi = {
  getAll: () => axios.get<BOM[]>(`${API_BASE_URL}/MasterData/boms`),
  getById: (id: string) => axios.get<BOM>(`${API_BASE_URL}/MasterData/boms/${id}`),
  getByItem: (itemId: string) =>
    axios.get<BOM[]>(`${API_BASE_URL}/MasterData/boms/item/${itemId}`),
  create: (data: BOMFormData) => axios.post<BOM>(`${API_BASE_URL}/MasterData/boms`, data),
  update: (id: string, data: BOMFormData) =>
    axios.put<BOM>(`${API_BASE_URL}/MasterData/boms/${id}`, data),
  delete: (id: string) => axios.delete(`${API_BASE_URL}/MasterData/boms/${id}`),
};

// Routings API
export const routingsApi = {
  getAll: () => axios.get<Routing[]>(`${API_BASE_URL}/MasterData/routings`),
  getById: (id: string) => axios.get<Routing>(`${API_BASE_URL}/MasterData/routings/${id}`),
  getByItem: (itemId: string) =>
    axios.get<Routing[]>(`${API_BASE_URL}/MasterData/routings/item/${itemId}`),
  create: (data: RoutingFormData) =>
    axios.post<Routing>(`${API_BASE_URL}/MasterData/routings`, data),
  update: (id: string, data: RoutingFormData) =>
    axios.put<Routing>(`${API_BASE_URL}/MasterData/routings/${id}`, data),
  delete: (id: string) => axios.delete(`${API_BASE_URL}/MasterData/routings/${id}`),
};

// Work Centers API
export const workCentersApi = {
  getAll: () => axios.get<WorkCenter[]>(`${API_BASE_URL}/MasterData/workcenters`),
  getById: (id: string) => axios.get<WorkCenter>(`${API_BASE_URL}/MasterData/workcenters/${id}`),
  create: (data: Partial<WorkCenter>) =>
    axios.post<WorkCenter>(`${API_BASE_URL}/MasterData/workcenters`, data),
  update: (id: string, data: Partial<WorkCenter>) =>
    axios.put<WorkCenter>(`${API_BASE_URL}/MasterData/workcenters/${id}`, data),
  delete: (id: string) => axios.delete(`${API_BASE_URL}/MasterData/workcenters/${id}`),
};

// Employees API
export const employeesApi = {
  getAll: () => axios.get<Employee[]>(`${API_BASE_URL}/MasterData/employees`),
  getById: (id: string) => axios.get<Employee>(`${API_BASE_URL}/MasterData/employees/${id}`),
  create: (data: Partial<Employee>) =>
    axios.post<Employee>(`${API_BASE_URL}/MasterData/employees`, data),
  update: (id: string, data: Partial<Employee>) =>
    axios.put<Employee>(`${API_BASE_URL}/MasterData/employees/${id}`, data),
  delete: (id: string) => axios.delete(`${API_BASE_URL}/MasterData/employees/${id}`),
};

export default {
  items: itemsApi,
  partners: partnersApi,
  warehouses: warehousesApi,
  locations: locationsApi,
  uom: uomApi,
  boms: bomsApi,
  routings: routingsApi,
  workCenters: workCentersApi,
  employees: employeesApi,
};
