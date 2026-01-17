// Customs & Trade TypeScript Interfaces

export enum DeclarationStatus {
  Draft = 1,
  Submitted = 2,
  Cleared = 3,
  Rejected = 4,
}

export interface CustomsProcedure {
  id: string;
  code: string;
  name: string;
  description?: string;
  requiresGuarantee: boolean;
  guaranteePercentage: number;
  isActive: boolean;
}

export interface CustomsDeclaration {
  id?: string;
  declarationNumber?: string;
  declarationDate: string;
  customsProcedureId: string;
  customsProcedure?: CustomsProcedure;
  mrn?: string;
  
  // General Information (Boxes 1-14)
  box1_DeclarationType?: string;
  box2_Exporter?: string;
  box3_Forms?: string;
  box5_ItemsCount?: number;
  box8_ConsigneeId?: string;
  box14_Declarant?: string;
  
  // Transport & Location (Boxes 15-29)
  box15a_ExportCountry?: string;
  box17a_DestinationCountry?: string;
  box18_IdentityMeans?: string;
  box19_Container?: string;
  box20_DeliveryTerms?: string;
  box21_IdentityMeansOfBorder?: string;
  box22_Currency?: string;
  box25_TransportMode?: string;
  box26_InlandTransportMode?: string;
  box29_CustomsOffice?: string;
  
  // Financial (Boxes 45-47)
  box45_Adjustment?: number;
  totalCustomsValue: number;
  totalDuty: number;
  totalVAT: number;
  
  // Other
  box44_AdditionalInfo?: string;
  isCleared: boolean;
  clearedDate?: string;
  dueDate?: string;
  
  items: CustomsDeclarationItem[];
  documents?: CustomsDocument[];
  notes?: string;
  createdAt?: string;
  createdBy?: string;
}

export interface CustomsDeclarationFormData {
  declarationDate: string;
  customsProcedureId: string;
  box1_DeclarationType?: string;
  box2_Exporter?: string;
  box8_ConsigneeId?: string;
  box14_Declarant?: string;
  box15a_ExportCountry?: string;
  box17a_DestinationCountry?: string;
  box18_IdentityMeans?: string;
  box20_DeliveryTerms?: string;
  box22_Currency?: string;
  box25_TransportMode?: string;
  box29_CustomsOffice?: string;
  items: CustomsDeclarationItemFormData[];
  notes?: string;
}

export interface CustomsDeclarationItem {
  id?: string;
  itemNumber: number;
  itemId: string;
  item?: any;
  hsCode: string;
  description: string;
  quantity: number;
  uoMId: string;
  uoM?: any;
  batchNumber?: string;
  mrn?: string;
  countryOfOrigin: string;
  customsValue: number;
  dutyRate: number;
  dutyAmount: number;
  vatRate: number;
  vatAmount: number;
  netMass?: number;
  grossMass?: number;
}

export interface CustomsDeclarationItemFormData {
  itemNumber: number;
  itemId: string;
  hsCode: string;
  description: string;
  quantity: number;
  uoMId: string;
  batchNumber?: string;
  mrn?: string;
  countryOfOrigin: string;
  customsValue: number;
  dutyRate: number;
  vatRate: number;
  netMass?: number;
  grossMass?: number;
}

export interface MRNRegistry {
  id: string;
  mrn: string;
  customsDeclarationId?: string;
  customsDeclaration?: CustomsDeclaration;
  procedureCode: string;
  registrationDate: string;
  dueDate?: string;
  isActive: boolean;
  totalDutyAmount: number;
  guaranteeAmount: number;
  usedAmount: number;
  availableAmount: number;
  currency: string;
  notes?: string;
  batches?: MRNBatchLink[];
}

export interface MRNBatchLink {
  batchNumber: string;
  itemCode: string;
  itemName: string;
  quantity: number;
  usedQuantity: number;
}

export interface CustomsDocument {
  id?: string;
  customsDeclarationId?: string;
  documentType: string;
  documentNumber: string;
  documentDate: string;
  fileName?: string;
  filePath?: string;
  notes?: string;
}

export interface DocumentUpload {
  customsDeclarationId?: string;
  documentType: string;
  documentNumber: string;
  documentDate: string;
  file: File;
  notes?: string;
}
