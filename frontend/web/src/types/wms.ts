// WMS TypeScript Interfaces

export enum QualityStatus {
  OK = 1,
  Blocked = 2,
  Quarantine = 3,
}

export enum PickTaskStatus {
  Pending = 1,
  Assigned = 2,
  InProgress = 3,
  Completed = 4,
  Cancelled = 5,
}

export interface ReceiptLine {
  id?: string;
  itemId: string;
  item?: any;
  quantity: number;
  uoMId: string;
  uoM?: any;
  batchNumber?: string;
  mrn?: string;
  locationId: string;
  location?: any;
  qualityStatus: QualityStatus;
  expiryDate?: string;
  notes?: string;
}

export interface Receipt {
  id?: string;
  receiptNumber?: string;
  receiptDate: string;
  supplierId?: string;
  supplier?: any;
  warehouseId: string;
  warehouse?: any;
  referenceNumber?: string;
  notes?: string;
  lines: ReceiptLine[];
  createdAt?: string;
  createdBy?: string;
}

export interface ReceiptFormData {
  receiptDate: string;
  supplierId?: string;
  warehouseId: string;
  referenceNumber?: string;
  notes?: string;
  lines: ReceiptLine[];
}

export interface TransferLine {
  id?: string;
  itemId: string;
  item?: any;
  quantity: number;
  uoMId: string;
  uoM?: any;
  batchNumber?: string;
  mrn?: string;
  fromLocationId: string;
  fromLocation?: any;
  toLocationId: string;
  toLocation?: any;
  qualityStatus: QualityStatus;
}

export interface Transfer {
  id?: string;
  transferNumber?: string;
  transferDate: string;
  fromWarehouseId: string;
  fromWarehouse?: any;
  toWarehouseId: string;
  toWarehouse?: any;
  notes?: string;
  lines: TransferLine[];
  createdAt?: string;
  createdBy?: string;
}

export interface TransferFormData {
  transferDate: string;
  fromWarehouseId: string;
  toWarehouseId: string;
  notes?: string;
  lines: TransferLine[];
}

export interface ShipmentLine {
  id?: string;
  itemId: string;
  item?: any;
  quantity: number;
  uoMId: string;
  uoM?: any;
  batchNumber?: string;
  mrn?: string;
  locationId: string;
  location?: any;
}

export interface Shipment {
  id?: string;
  shipmentNumber?: string;
  shipmentDate: string;
  customerId?: string;
  customer?: any;
  warehouseId: string;
  warehouse?: any;
  carrierId?: string;
  carrier?: any;
  trackingNumber?: string;
  notes?: string;
  lines: ShipmentLine[];
  createdAt?: string;
  createdBy?: string;
}

export interface ShipmentFormData {
  shipmentDate: string;
  customerId?: string;
  warehouseId: string;
  carrierId?: string;
  trackingNumber?: string;
  notes?: string;
  lines: ShipmentLine[];
}

export interface PickTask {
  id: string;
  pickTaskNumber: string;
  itemId: string;
  item?: any;
  quantity: number;
  uoMId: string;
  uoM?: any;
  batchNumber?: string;
  mrn?: string;
  locationId: string;
  location?: any;
  toLocationId?: string;
  toLocation?: any;
  assignedToEmployeeId?: string;
  assignedToEmployee?: any;
  status: PickTaskStatus;
  priority: number;
  notes?: string;
  createdAt: string;
  completedAt?: string;
}

export interface InventoryBalance {
  id: string;
  itemId: string;
  item?: any;
  locationId: string;
  location?: any;
  batchNumber?: string;
  mrn?: string;
  quantity: number;
  uoMId: string;
  uoM?: any;
  qualityStatus: QualityStatus;
  lastMovementDate?: string;
}

export interface QualityStatusUpdate {
  inventoryBalanceId: string;
  newQualityStatus: QualityStatus;
  reason: string;
  notes?: string;
}

export interface CycleCount {
  id?: string;
  cycleCountNumber?: string;
  countDate: string;
  locationId: string;
  location?: any;
  countedByEmployeeId: string;
  countedByEmployee?: any;
  status: string;
  lines: CycleCountLine[];
  notes?: string;
}

export interface CycleCountLine {
  id?: string;
  itemId: string;
  item?: any;
  batchNumber?: string;
  mrn?: string;
  systemQuantity: number;
  countedQuantity: number;
  variance: number;
  uoMId: string;
  uoM?: any;
}
