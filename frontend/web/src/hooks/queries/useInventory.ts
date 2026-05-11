import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { wmsApi, masterDataApi } from '../../services/api';

/**
 * P16.B1 — react-query hooks for the inventory pilot.
 *
 * The page used to call `wmsApi.*` directly with hand-rolled
 * `useState + useEffect`. With this hook file, the page declares
 * its data needs and the cache handles refetching, focus refresh,
 * and invalidation after writes.
 *
 * Pattern for subsequent migrations: add a sibling file per
 * domain (`useReceipts.ts`, `useShipments.ts`, ...), reuse the
 * query keys exported here, and call `qc.invalidateQueries` on
 * mutation success.
 */

export const inventoryKeys = {
  all: ['wms', 'inventory'] as const,
  list: (filters?: { itemId?: string; locationId?: string }) =>
    [...inventoryKeys.all, 'list', filters ?? {}] as const,
  locations: ['masterData', 'locations'] as const,
};

export type InventoryRow = {
  id: string;
  itemId: string;
  item?: { id?: string; code?: string; name?: string } | null;
  location?: { id?: string; code?: string; name?: string; warehouseId?: string } | null;
  locationId: string;
  batchNumber?: string | null;
  mrn?: string | null;
  quantity: number;
  uoM?: { code?: string } | null;
  qualityStatus: number;
};

export const useInventoryQuery = (filters?: { itemId?: string; locationId?: string }) =>
  useQuery({
    queryKey: inventoryKeys.list(filters),
    queryFn: async () => {
      const resp = await wmsApi.getInventory(filters?.itemId, filters?.locationId);
      return resp.data as InventoryRow[];
    },
  });

export const useAllLocationsQuery = () =>
  useQuery({
    queryKey: inventoryKeys.locations,
    queryFn: async () => {
      const resp = await masterDataApi.getLocations();
      return (resp.data as Array<{ id: string; code: string; name: string; warehouseId: string }>) ?? [];
    },
    staleTime: 5 * 60_000,
  });

const invalidateInventory = (qc: ReturnType<typeof useQueryClient>) =>
  qc.invalidateQueries({ queryKey: inventoryKeys.all });

export const useReceiptCreate = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: any) => wmsApi.createReceipt(payload).then((r) => r.data),
    onSuccess: () => invalidateInventory(qc),
  });
};

export const useTransferCreate = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: any) => wmsApi.createTransfer(payload).then((r) => r.data),
    onSuccess: () => invalidateInventory(qc),
  });
};

export const useShipmentCreate = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: any) => wmsApi.createShipment(payload).then((r) => r.data),
    onSuccess: () => invalidateInventory(qc),
  });
};

export const useCycleCountCreate = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: any) => wmsApi.createCycleCount(payload).then((r) => r.data),
    onSuccess: () => invalidateInventory(qc),
  });
};

export const useAdjustmentCreate = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: any) => wmsApi.createAdjustment(payload).then((r) => r.data),
    onSuccess: () => invalidateInventory(qc),
  });
};

export const useQualityStatusChange = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: {
      inventoryBalanceId: string;
      newQualityStatus: number;
      reason?: string;
    }) => wmsApi.updateQualityStatus(payload).then((r) => r.data),
    onSuccess: () => invalidateInventory(qc),
  });
};

export const useMoveBatch = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: {
      batchNumber: string;
      targetStage: number;
      warehouseId?: string | null;
      targetLocationId?: string | null;
      reason?: string | null;
    }) => wmsApi.moveBatch(payload).then((r) => r.data),
    onSuccess: () => invalidateInventory(qc),
  });
};

export const useBulkMoveBalances = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: {
      balanceIds: string[];
      targetLocationId: string;
      reason?: string | null;
    }) => wmsApi.bulkMoveBalances(payload).then((r) => r.data),
    onSuccess: () => invalidateInventory(qc),
  });
};
