import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { managementApi } from '../../services/api';

/**
 * P16.C1 — react-query hooks for the unified RiskRegisterItem entity.
 *
 * Kind=1 (Risk) backs `/management/risks` (OpenRisks.tsx).
 * Kind=2 (Escalation) backs `/management/escalations`.
 */

export type RiskKind = 1 | 2;
export type RiskSeverity = 1 | 2 | 3 | 4;
export type RiskStatus = 1 | 2 | 3 | 4 | 5 | 6;

export interface RiskRegisterItemDto {
  id: string;
  tenantId: string;
  kind: RiskKind;
  title: string;
  category?: string | null;
  severity: RiskSeverity;
  status: RiskStatus;
  owner?: string | null;
  mitigation?: string | null;
  resolution?: string | null;
  dueDate?: string | null;
  reviewDate?: string | null;
  createdAt: string;
  modifiedAt?: string | null;
}

interface Envelope<T> { isSuccess: boolean; data: T; errorMessage?: string }

export const riskKeys = {
  all: ['management', 'risks'] as const,
  list: (kind?: RiskKind) => [...riskKeys.all, 'list', kind ?? 'all'] as const,
};

export const useRisksQuery = (kind?: RiskKind) =>
  useQuery({
    queryKey: riskKeys.list(kind),
    queryFn: async () => {
      const resp = await managementApi.getRisks(kind);
      const env = resp.data as Envelope<RiskRegisterItemDto[]>;
      return env.data ?? [];
    },
  });

const invalidate = (qc: ReturnType<typeof useQueryClient>) =>
  qc.invalidateQueries({ queryKey: riskKeys.all });

export interface RiskMutationInput {
  kind?: RiskKind;
  title: string;
  category?: string | null;
  severity: RiskSeverity;
  status: RiskStatus;
  owner?: string | null;
  mitigation?: string | null;
  resolution?: string | null;
  dueDate?: string | null;
  reviewDate?: string | null;
}

export const useCreateRisk = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (payload: RiskMutationInput) => {
      const resp = await managementApi.createRisk(payload);
      const env = resp.data as Envelope<RiskRegisterItemDto>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Create failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};

export const useUpdateRisk = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...payload }: RiskMutationInput & { id: string }) => {
      const resp = await managementApi.updateRisk(id, { id, ...payload });
      const env = resp.data as Envelope<RiskRegisterItemDto>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Update failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};

export const useDeleteRisk = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const resp = await managementApi.deleteRisk(id);
      const env = resp.data as Envelope<boolean>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Delete failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};
