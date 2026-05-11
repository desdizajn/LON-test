import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { financeApi } from '../../services/api';

export type CostRateScope = 1 | 2 | 3 | 4 | 5;

export const COST_RATE_SCOPE_LABEL: Record<CostRateScope, string> = {
  1: 'Machine',
  2: 'Operator',
  3: 'Shift',
  4: 'Operation',
  5: 'WorkCenter',
};

export interface CostRateDto {
  id: string;
  tenantId: string;
  scope: CostRateScope;
  scopeId?: string | null;
  costPerHour?: number | null;
  costPerUnit?: number | null;
  currency: string;
  validFrom: string;
  validTo?: string | null;
  notes?: string | null;
  createdAt: string;
  modifiedAt?: string | null;
}

interface Envelope<T> { isSuccess: boolean; data: T; errorMessage?: string }

export const costRateKeys = {
  all: ['finance', 'cost-rates'] as const,
  list: (scope?: CostRateScope) => [...costRateKeys.all, 'list', scope ?? 'all'] as const,
};

export const useCostRatesQuery = (scope?: CostRateScope) =>
  useQuery({
    queryKey: costRateKeys.list(scope),
    queryFn: async () => {
      const resp = await financeApi.getCostRates(scope);
      const env = resp.data as Envelope<CostRateDto[]>;
      return env.data ?? [];
    },
  });

const invalidate = (qc: ReturnType<typeof useQueryClient>) =>
  qc.invalidateQueries({ queryKey: costRateKeys.all });

export interface CostRateMutationInput {
  scope: CostRateScope;
  scopeId?: string | null;
  costPerHour?: number | null;
  costPerUnit?: number | null;
  currency: string;
  validFrom: string;
  validTo?: string | null;
  notes?: string | null;
}

export const useCreateCostRate = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (payload: CostRateMutationInput) => {
      const resp = await financeApi.createCostRate(payload);
      const env = resp.data as Envelope<CostRateDto>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Create failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};

export const useUpdateCostRate = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...payload }: CostRateMutationInput & { id: string }) => {
      const resp = await financeApi.updateCostRate(id, { id, ...payload });
      const env = resp.data as Envelope<CostRateDto>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Update failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};

export const useDeleteCostRate = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const resp = await financeApi.deleteCostRate(id);
      const env = resp.data as Envelope<boolean>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Delete failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};
