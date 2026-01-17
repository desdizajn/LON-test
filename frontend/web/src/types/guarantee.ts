// Guarantee TypeScript Interfaces

export interface GuaranteeAccount {
  id: string;
  accountNumber: string;
  accountName: string;
  bankId?: string;
  bank?: any;
  totalLimit: number;
  currentBalance: number;
  availableLimit: number;
  currency: string;
  validFrom: string;
  validTo?: string;
  isActive: boolean;
  notes?: string;
  createdAt: string;
}

export interface GuaranteeLedgerEntry {
  id: string;
  guaranteeAccountId: string;
  guaranteeAccount?: GuaranteeAccount;
  entryDate: string;
  entryType: string; // 'Debit' or 'Credit'
  amount: number;
  currency: string;
  mrn?: string;
  customsDeclarationId?: string;
  description: string;
  isReleased: boolean;
  releasedDate?: string;
  expectedReleaseDate?: string;
  notes?: string;
  createdAt: string;
  createdBy: string;
}

export interface LedgerEntryFormData {
  guaranteeAccountId: string;
  entryDate: string;
  entryType: 'Debit' | 'Credit';
  amount: number;
  currency: string;
  mrn?: string;
  customsDeclarationId?: string;
  description: string;
  expectedReleaseDate?: string;
  notes?: string;
}

export interface GuaranteeExposure {
  accountId: string;
  accountName: string;
  totalLimit: number;
  totalDebit: number;
  totalCredit: number;
  currentBalance: number;
  availableLimit: number;
  currency: string;
  exposurePercentage: number;
  activeGuaranteesCount: number;
  expiringIn30Days: number;
}
