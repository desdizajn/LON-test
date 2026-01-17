// Production TypeScript Interfaces

export enum ProductionOrderStatus {
  Draft = 1,
  Released = 2,
  InProgress = 3,
  Completed = 4,
  Closed = 5,
  Cancelled = 6,
}

export enum OperationStatus {
  NotStarted = 1,
  InProgress = 2,
  Completed = 3,
  Skipped = 4,
}

export interface ProductionOrder {
  id: string;
  orderNumber: string;
  itemId: string;
  item?: any;
  bomId?: string;
  bom?: any;
  routingId?: string;
  routing?: any;
  orderQuantity: number;
  producedQuantity: number;
  scrapQuantity: number;
  uoMId: string;
  uoM?: any;
  status: ProductionOrderStatus;
  plannedStartDate: string;
  plannedEndDate: string;
  actualStartDate?: string;
  actualEndDate?: string;
  priority: number;
  notes?: string;
  materials?: MaterialReservation[];
  operations?: ProductionOrderOperation[];
  createdAt: string;
  createdBy: string;
}

export interface ProductionOrderFormData {
  itemId: string;
  bomId?: string;
  routingId?: string;
  orderQuantity: number;
  uoMId: string;
  plannedStartDate: string;
  plannedEndDate: string;
  priority: number;
  notes?: string;
}

export interface MaterialReservation {
  id: string;
  productionOrderId: string;
  itemId: string;
  item?: any;
  requiredQuantity: number;
  issuedQuantity: number;
  uoMId: string;
  uoM?: any;
}

export interface MaterialIssue {
  id?: string;
  productionOrderId: string;
  productionOrder?: any;
  itemId: string;
  item?: any;
  quantity: number;
  uoMId: string;
  uoM?: any;
  batchNumber: string;
  mrn?: string;
  locationId: string;
  location?: any;
  issuedByEmployeeId?: string;
  issuedByEmployee?: any;
  issueDate: string;
  notes?: string;
}

export interface MaterialIssueFormData {
  productionOrderId: string;
  itemId: string;
  quantity: number;
  uoMId: string;
  batchNumber: string;
  mrn?: string;
  locationId: string;
  issueDate: string;
  notes?: string;
}

export interface ProductionReceipt {
  id?: string;
  productionOrderId: string;
  productionOrder?: any;
  itemId: string;
  item?: any;
  quantity: number;
  uoMId: string;
  uoM?: any;
  batchNumber?: string;
  locationId: string;
  location?: any;
  receivedByEmployeeId?: string;
  receivedByEmployee?: any;
  receiptDate: string;
  qualityStatus: number;
  notes?: string;
}

export interface ProductionReceiptFormData {
  productionOrderId: string;
  itemId: string;
  quantity: number;
  uoMId: string;
  locationId: string;
  receiptDate: string;
  qualityStatus: number;
  notes?: string;
}

export interface ScrapReport {
  productionOrderId: string;
  itemId: string;
  quantity: number;
  uoMId: string;
  reason: string;
  scrapDate: string;
  notes?: string;
}

export interface ProductionOrderOperation {
  id: string;
  productionOrderId: string;
  operationNumber: number;
  operationName: string;
  workCenterId: string;
  workCenter?: any;
  setupTime: number;
  runTimePerUnit: number;
  status: OperationStatus;
  actualStartTime?: string;
  actualEndTime?: string;
  notes?: string;
}

export interface OperationExecution {
  operationId: string;
  actualStartTime?: string;
  actualEndTime?: string;
  employeeId?: string;
  notes?: string;
}
