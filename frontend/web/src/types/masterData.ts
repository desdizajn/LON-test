// Master Data TypeScript Interfaces

export enum ItemType {
  RawMaterial = 1,
  SemiFinished = 2,
  FinishedGood = 3,
  Packaging = 4,
}

export enum PartnerType {
  Supplier = 1,
  Customer = 2,
  Carrier = 3,
  Bank = 4,
}

export enum LocationType {
  Zone = 1,
  Aisle = 2,
  Rack = 3,
  Bin = 4,
}

export enum QualityStatus {
  OK = 1,
  Blocked = 2,
  Quarantine = 3,
}

export interface UoM {
  id: string;
  code: string;
  name: string;
  description?: string;
  isActive: boolean;
  createdAt?: string;
  createdBy?: string;
  updatedAt?: string;
  updatedBy?: string;
}

export interface UoMFormData {
  code: string;
  name: string;
  description?: string;
  isActive: boolean;
}

export interface Item {
  id: string;
  code: string;
  name: string;
  description?: string;
  itemType: ItemType;
  uoMId: string;
  uoM?: UoM;
  isBatchRequired: boolean;
  isMRNRequired: boolean;
  countryOfOrigin?: string;
  hsCode?: string;
  isActive: boolean;
  createdAt: string;
  createdBy: string;
  updatedAt?: string;
  updatedBy?: string;
}

export interface ItemFormData {
  code: string;
  name: string;
  description?: string;
  itemType: ItemType;
  uoMId: string;
  isBatchRequired: boolean;
  isMRNRequired: boolean;
  countryOfOrigin?: string;
  hsCode?: string;
  isActive: boolean;
}

export interface Partner {
  id: string;
  code: string;
  name: string;
  partnerType: PartnerType;
  taxNumber?: string;
  vatNumber?: string;
  eoriNumber?: string;
  address?: string;
  city?: string;
  postalCode?: string;
  country?: string;
  contactPerson?: string;
  email?: string;
  phone?: string;
  isActive: boolean;
  createdAt: string;
  createdBy: string;
  updatedAt?: string;
  updatedBy?: string;
}

export interface PartnerFormData {
  code: string;
  name: string;
  partnerType: PartnerType;
  taxNumber?: string;
  vatNumber?: string;
  eoriNumber?: string;
  address?: string;
  city?: string;
  postalCode?: string;
  country?: string;
  contactPerson?: string;
  email?: string;
  phone?: string;
  isActive: boolean;
}

export interface Warehouse {
  id: string;
  code: string;
  name: string;
  description?: string;
  address?: string;
  isActive: boolean;
  createdAt: string;
  createdBy: string;
  updatedAt?: string;
  updatedBy?: string;
}

export interface WarehouseFormData {
  code: string;
  name: string;
  description?: string;
  address?: string;
  isActive: boolean;
}

export interface Location {
  id: string;
  warehouseId: string;
  warehouse?: Warehouse;
  code: string;
  name: string;
  locationType: LocationType;
  parentLocationId?: string;
  parentLocation?: Location;
  isActive: boolean;
  createdAt: string;
  createdBy: string;
  updatedAt?: string;
  updatedBy?: string;
}

export interface LocationFormData {
  warehouseId: string;
  code: string;
  name: string;
  locationType: LocationType;
  parentLocationId?: string;
  isActive: boolean;
}

export interface Employee {
  id: string;
  employeeNumber: string;
  firstName: string;
  lastName: string;
  email?: string;
  phone?: string;
  position?: string;
  departmentId?: string;
  isActive: boolean;
}

export interface WorkCenter {
  id: string;
  code: string;
  name: string;
  description?: string;
  isActive: boolean;
}

export interface BOM {
  id: string;
  itemId: string;
  item?: Item;
  version: string;
  quantity: number;
  uoMId: string;
  uoM?: UoM;
  validFrom?: string;
  validTo?: string;
  notes?: string;
  isActive: boolean;
  lines?: BOMLine[];
  createdAt: string;
  createdBy: string;
  updatedAt?: string;
  updatedBy?: string;
}

export interface BOMLine {
  id: string;
  bomId: string;
  componentItemId: string;
  componentItem?: Item;
  quantity: number;
  uoMId: string;
  uoM?: UoM;
  scrapFactor: number;
  sequenceNumber: number;
}

export interface BOMFormData {
  itemId: string;
  version: string;
  quantity: number;
  uoMId: string;
  validFrom?: string;
  validTo?: string;
  notes?: string;
  isActive: boolean;
  lines: BOMLineFormData[];
}

export interface BOMLineFormData {
  componentItemId: string;
  quantity: number;
  uoMId: string;
  scrapFactor: number;
  sequenceNumber: number;
}

export interface Routing {
  id: string;
  itemId: string;
  item?: Item;
  version: string;
  description?: string;
  isActive: boolean;
  operations?: RoutingOperation[];
  createdAt: string;
  createdBy: string;
  updatedAt?: string;
  updatedBy?: string;
}

export interface RoutingOperation {
  id: string;
  routingId: string;
  operationNumber: number;
  workCenterId: string;
  workCenter?: WorkCenter;
  operationName: string;
  standardTime: number;
  setupTime: number;
  description?: string;
}

export interface RoutingFormData {
  itemId: string;
  version: string;
  description?: string;
  isActive: boolean;
  operations: RoutingOperationFormData[];
}

export interface RoutingOperationFormData {
  operationNumber: number;
  workCenterId: string;
  operationName: string;
  standardTime: number;
  setupTime: number;
  description?: string;
}

// API Response types
export interface ApiResponse<T> {
  data: T;
  success: boolean;
  message?: string;
  errors?: string[];
}

export interface PaginatedResponse<T> {
  data: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
