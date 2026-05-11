import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { financeApi } from '../../services/api';

export type SupplierInvoiceProjectedStatus = 1 | 2 | 3 | 4; // Open / Paid / Cancelled / Overdue (derived)

export const SUPPLIER_INVOICE_STATUS_LABEL: Record<SupplierInvoiceProjectedStatus, string> = {
  1: 'Open',
  2: 'Paid',
  3: 'Cancelled',
  4: 'Overdue',
};

export interface SupplierInvoiceDto {
  id: string;
  tenantId: string;
  number: string;
  supplierPartnerId: string;
  supplierCode?: string | null;
  supplierName?: string | null;
  invoiceDate: string;
  dueDate: string;
  amount: number;
  currency: string;
  status: SupplierInvoiceProjectedStatus;
  paidDate?: string | null;
  notes?: string | null;
  createdAt: string;
  modifiedAt?: string | null;
}

interface Envelope<T> { isSuccess: boolean; data: T; errorMessage?: string }

export const supplierInvoiceKeys = {
  all: ['finance', 'supplier-invoices'] as const,
  list: (status?: SupplierInvoiceProjectedStatus) => [...supplierInvoiceKeys.all, 'list', status ?? 'all'] as const,
};

export const useSupplierInvoicesQuery = (status?: SupplierInvoiceProjectedStatus) =>
  useQuery({
    queryKey: supplierInvoiceKeys.list(status),
    queryFn: async () => {
      const resp = await financeApi.getSupplierInvoices(status);
      const env = resp.data as Envelope<SupplierInvoiceDto[]>;
      return env.data ?? [];
    },
  });

const invalidate = (qc: ReturnType<typeof useQueryClient>) =>
  qc.invalidateQueries({ queryKey: supplierInvoiceKeys.all });

export interface SupplierInvoiceCreateInput {
  number: string;
  supplierPartnerId: string;
  invoiceDate: string;
  dueDate: string;
  amount: number;
  currency: string;
  notes?: string | null;
}

export interface SupplierInvoiceUpdateInput extends SupplierInvoiceCreateInput {
  status: 1 | 2 | 3; // persisted statuses only (Overdue is derived)
  paidDate?: string | null;
}

export const useCreateSupplierInvoice = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (payload: SupplierInvoiceCreateInput) => {
      const resp = await financeApi.createSupplierInvoice(payload);
      const env = resp.data as Envelope<SupplierInvoiceDto>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Create failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};

export const useUpdateSupplierInvoice = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...payload }: SupplierInvoiceUpdateInput & { id: string }) => {
      const resp = await financeApi.updateSupplierInvoice(id, { id, ...payload });
      const env = resp.data as Envelope<SupplierInvoiceDto>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Update failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};

export const useDeleteSupplierInvoice = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const resp = await financeApi.deleteSupplierInvoice(id);
      const env = resp.data as Envelope<boolean>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Delete failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};
