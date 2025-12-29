import { create } from 'zustand';
import type { Item, Partner, Warehouse, UoM } from '../types/masterData';

interface MasterDataStore {
  // Items
  items: Item[];
  setItems: (items: Item[]) => void;
  addItem: (item: Item) => void;
  updateItem: (id: string, item: Item) => void;
  removeItem: (id: string) => void;

  // Partners
  partners: Partner[];
  setPartners: (partners: Partner[]) => void;
  addPartner: (partner: Partner) => void;
  updatePartner: (id: string, partner: Partner) => void;
  removePartner: (id: string) => void;

  // Warehouses
  warehouses: Warehouse[];
  setWarehouses: (warehouses: Warehouse[]) => void;
  addWarehouse: (warehouse: Warehouse) => void;
  updateWarehouse: (id: string, warehouse: Warehouse) => void;
  removeWarehouse: (id: string) => void;

  // UoM
  uoms: UoM[];
  setUoMs: (uoms: UoM[]) => void;
  addUoM: (uom: UoM) => void;
  updateUoM: (id: string, uom: UoM) => void;
  removeUoM: (id: string) => void;
}

export const useMasterDataStore = create<MasterDataStore>((set) => ({
  // Items
  items: [],
  setItems: (items) => set({ items }),
  addItem: (item) => set((state) => ({ items: [...state.items, item] })),
  updateItem: (id, item) =>
    set((state) => ({
      items: state.items.map((i) => (i.id === id ? item : i)),
    })),
  removeItem: (id) =>
    set((state) => ({
      items: state.items.filter((i) => i.id !== id),
    })),

  // Partners
  partners: [],
  setPartners: (partners) => set({ partners }),
  addPartner: (partner) => set((state) => ({ partners: [...state.partners, partner] })),
  updatePartner: (id, partner) =>
    set((state) => ({
      partners: state.partners.map((p) => (p.id === id ? partner : p)),
    })),
  removePartner: (id) =>
    set((state) => ({
      partners: state.partners.filter((p) => p.id !== id),
    })),

  // Warehouses
  warehouses: [],
  setWarehouses: (warehouses) => set({ warehouses }),
  addWarehouse: (warehouse) =>
    set((state) => ({ warehouses: [...state.warehouses, warehouse] })),
  updateWarehouse: (id, warehouse) =>
    set((state) => ({
      warehouses: state.warehouses.map((w) => (w.id === id ? warehouse : w)),
    })),
  removeWarehouse: (id) =>
    set((state) => ({
      warehouses: state.warehouses.filter((w) => w.id !== id),
    })),

  // UoM
  uoms: [],
  setUoMs: (uoms) => set({ uoms }),
  addUoM: (uom) => set((state) => ({ uoms: [...state.uoms, uom] })),
  updateUoM: (id, uom) =>
    set((state) => ({
      uoms: state.uoms.map((u) => (u.id === id ? uom : u)),
    })),
  removeUoM: (id) =>
    set((state) => ({
      uoms: state.uoms.filter((u) => u.id !== id),
    })),
}));
