import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { hrApi } from '../../services/api';

export interface EmployeeCertificationDto {
  id: string;
  tenantId: string;
  employeeId: string;
  employeeName?: string | null;
  employeeNumber?: string | null;
  certificationName: string;
  skillArea?: string | null;
  issuedDate: string;
  expiryDate?: string | null;
  issuingAuthority?: string | null;
  certificateNumber?: string | null;
  notes?: string | null;
  createdAt: string;
  modifiedAt?: string | null;
}

interface Envelope<T> { isSuccess: boolean; data: T; errorMessage?: string }

export const trainingKeys = {
  all: ['hr', 'certifications'] as const,
  list: (employeeId?: string) => [...trainingKeys.all, 'list', employeeId ?? 'all'] as const,
  expiring: (days: number) => [...trainingKeys.all, 'expiring', days] as const,
};

export const useCertificationsQuery = (employeeId?: string) =>
  useQuery({
    queryKey: trainingKeys.list(employeeId),
    queryFn: async () => {
      const resp = await hrApi.getCertifications(employeeId);
      const env = resp.data as Envelope<EmployeeCertificationDto[]>;
      return env.data ?? [];
    },
  });

export const useExpiringCertificationsQuery = (withinDays = 30) =>
  useQuery({
    queryKey: trainingKeys.expiring(withinDays),
    queryFn: async () => {
      const resp = await hrApi.getExpiringCertifications(withinDays);
      const env = resp.data as Envelope<EmployeeCertificationDto[]>;
      return env.data ?? [];
    },
  });

const invalidate = (qc: ReturnType<typeof useQueryClient>) =>
  qc.invalidateQueries({ queryKey: trainingKeys.all });

export interface CertMutationInput {
  employeeId: string;
  certificationName: string;
  skillArea?: string | null;
  issuedDate: string;
  expiryDate?: string | null;
  issuingAuthority?: string | null;
  certificateNumber?: string | null;
  notes?: string | null;
}

export const useCreateCertification = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (payload: CertMutationInput) => {
      const resp = await hrApi.createCertification(payload);
      const env = resp.data as Envelope<EmployeeCertificationDto>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Create failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};

export const useUpdateCertification = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...payload }: CertMutationInput & { id: string }) => {
      const resp = await hrApi.updateCertification(id, { id, ...payload });
      const env = resp.data as Envelope<EmployeeCertificationDto>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Update failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};

export const useDeleteCertification = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const resp = await hrApi.deleteCertification(id);
      const env = resp.data as Envelope<boolean>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Delete failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};
