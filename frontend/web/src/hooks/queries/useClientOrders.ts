import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { clientOrdersApi } from '../../services/api';

/**
 * Phase 17 §E2 — react-query hooks for ClientOrder.
 *
 * Backed by /api/ClientOrders (§E1 handlers). Used by:
 *  - /orders (list page)         → useClientOrders
 *  - /orders/:id (hub page)      → useClientOrder
 *  - create / edit / cancel      → mutations below
 */

export type ClientOrderStatus = 0 | 1 | 2 | 3 | 4 | 99;
// 0=Draft 1=Active 2=Producing 3=Shipped 4=Closed 99=Cancelled

export interface ClientOrderSummaryDto {
  id: string;
  orderNumber: string;
  customerPartnerId: string;
  customerPartnerName?: string | null;
  lonAuthorizationId: string;
  lonAuthorizationNumber?: string | null;
  orderDate: string;
  requestedShipDate?: string | null;
  status: ClientOrderStatus;
  statusName: string;
  finishedGoodsCount: number;
  declarationsCount: number;
  productionOrdersCount: number;
  shipmentsCount: number;
}

export interface ClientOrderFinishedGoodDto {
  id: string;
  itemId: string;
  itemCode?: string | null;
  itemName?: string | null;
  quantity: number;
  uoMId: string;
  uoMCode?: string | null;
  bomId?: string | null;
  unitPriceForeign?: number | null;
  currency: string;
  notes?: string | null;
}

export interface ClientOrderDto {
  id: string;
  orderNumber: string;
  customerPartnerId: string;
  customerPartnerName?: string | null;
  lonAuthorizationId: string;
  lonAuthorizationNumber?: string | null;
  customerOrderReference?: string | null;
  orderDate: string;
  requestedShipDate?: string | null;
  status: ClientOrderStatus;
  statusName: string;
  notes?: string | null;
  cancellationReason?: string | null;
  createdAt: string;
  createdBy: string;
  finishedGoods: ClientOrderFinishedGoodDto[];
}

interface Envelope<T> {
  isSuccess: boolean;
  data: T;
  errorMessage?: string;
}

export const clientOrderKeys = {
  all: ['clientOrders'] as const,
  list: (filters?: ListFilters) => [...clientOrderKeys.all, 'list', filters ?? {}] as const,
  detail: (id: string) => [...clientOrderKeys.all, 'detail', id] as const,
};

export interface ListFilters {
  status?: ClientOrderStatus;
  customerPartnerId?: string;
  fromDate?: string;
  toDate?: string;
  includeCancelled?: boolean;
}

export const useClientOrders = (filters?: ListFilters) =>
  useQuery({
    queryKey: clientOrderKeys.list(filters),
    queryFn: async () => {
      const resp = await clientOrdersApi.list(filters);
      const env = resp.data as Envelope<ClientOrderSummaryDto[]>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Failed to load orders');
      return env.data ?? [];
    },
  });

export const useClientOrder = (id: string | undefined) =>
  useQuery({
    queryKey: id ? clientOrderKeys.detail(id) : ['clientOrders', 'detail', 'none'],
    queryFn: async () => {
      if (!id) return null;
      const resp = await clientOrdersApi.get(id);
      const env = resp.data as Envelope<ClientOrderDto>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Failed to load order');
      return env.data;
    },
    enabled: !!id,
  });

const invalidate = (qc: ReturnType<typeof useQueryClient>) =>
  qc.invalidateQueries({ queryKey: clientOrderKeys.all });

export interface CreateClientOrderInput {
  customerPartnerId: string;
  lonAuthorizationId: string;
  customerOrderReference?: string | null;
  orderDate?: string | null;
  requestedShipDate?: string | null;
  notes?: string | null;
}

export const useCreateClientOrder = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (payload: CreateClientOrderInput) => {
      const resp = await clientOrdersApi.create(payload);
      const env = resp.data as Envelope<string>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Create failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};

export interface UpdateClientOrderInput {
  customerOrderReference?: string | null;
  requestedShipDate?: string | null;
  notes?: string | null;
}

export const useUpdateClientOrder = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...payload }: UpdateClientOrderInput & { id: string }) => {
      const resp = await clientOrdersApi.update(id, payload);
      const env = resp.data as Envelope<string>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Update failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};

export const useCancelClientOrder = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, reason }: { id: string; reason: string }) => {
      const resp = await clientOrdersApi.cancel(id, reason);
      const env = resp.data as Envelope<string>;
      if (!env.isSuccess) throw new Error(env.errorMessage || 'Cancel failed');
      return env.data;
    },
    onSuccess: () => invalidate(qc),
  });
};
