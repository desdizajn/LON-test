import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { financeApi } from '../../services/api';

export type PayrollStatus = 1 | 2 | 3;
export const PAYROLL_STATUS_LABEL: Record<PayrollStatus, string> = {
  1: 'Draft',
  2: 'Finalized',
  3: 'Exported',
};

export interface PayrollLineDto {
  id: string;
  periodId: string;
  employeeId: string;
  employeeName?: string | null;
  employeeNumber?: string | null;
  regularHours: number;
  overtimeHours: number;
  absenceHours: number;
  bonusAmount: number;
  deductionAmount: number;
  netAmount: number;
  currency: string;
}

export interface PayrollPeriodDto {
  id: string;
  tenantId: string;
  periodStart: string;
  periodEnd: string;
  status: PayrollStatus;
  exportedAt?: string | null;
  notes?: string | null;
  lines: PayrollLineDto[];
  createdAt: string;
  modifiedAt?: string | null;
}

interface Envelope<T> { isSuccess: boolean; data: T; errorMessage?: string }

export const payrollKeys = {
  all: ['finance', 'payroll'] as const,
  list: () => [...payrollKeys.all, 'list'] as const,
  byId: (id: string) => [...payrollKeys.all, id] as const,
};

export const usePayrollPeriodsQuery = () =>
  useQuery({
    queryKey: payrollKeys.list(),
    queryFn: async () => {
      const resp = await financeApi.getPayrollPeriods();
      const env = resp.data as Envelope<PayrollPeriodDto[]>;
      return env.data ?? [];
    },
  });

export const usePayrollPeriodByIdQuery = (id?: string) =>
  useQuery({
    queryKey: id ? payrollKeys.byId(id) : payrollKeys.all,
    queryFn: async () => {
      const resp = await financeApi.getPayrollPeriod(id!);
      const env = resp.data as Envelope<PayrollPeriodDto>;
      return env.data;
    },
    enabled: !!id,
  });

const invalidate = (qc: ReturnType<typeof useQueryClient>) =>
  qc.invalidateQueries({ queryKey: payrollKeys.all });

export const useCreatePayrollPeriod = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { periodStart: string; periodEnd: string; standardHoursPerDay?: number; notes?: string }) => {
      const resp = await financeApi.createPayrollPeriod(payload);
      const env = resp.data as Envelope<PayrollPeriodDto>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Create failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};

export const useUpdatePayrollLine = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...payload }: { id: string } & Omit<PayrollLineDto, 'id' | 'periodId' | 'employeeId' | 'employeeName' | 'employeeNumber'>) => {
      const resp = await financeApi.updatePayrollLine(id, { id, ...payload });
      const env = resp.data as Envelope<PayrollLineDto>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Update failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};

export const useFinalizePayrollPeriod = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const resp = await financeApi.finalizePayrollPeriod(id);
      const env = resp.data as Envelope<PayrollPeriodDto>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Finalize failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};

export const useExportPayrollPeriod = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const resp = await financeApi.exportPayrollPeriod(id);
      const env = resp.data as Envelope<PayrollPeriodDto>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Export failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};
