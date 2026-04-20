import { useCallback, useEffect, useState } from 'react';
import { api } from '../services/api';

/**
 * P5.3.5 — per-user recent-values cache hook.
 *
 * Usage:
 * ```tsx
 * const { recent, record } = useFieldHistory('receipt.supplier');
 * ...
 * <input list={`fh-receipt.supplier`} value={v} onChange={e => setV(e.target.value)} />
 * <datalist id={`fh-receipt.supplier`}>
 *   {recent.map(r => <option key={r.value} value={r.value} />)}
 * </datalist>
 * ...
 * await save();       // form submit
 * record(v);          // upsert into recent
 * ```
 */

export type RecentValue = { value: string; lastUsedAt: string; usageCount: number };

export function useFieldHistory(fieldKey: string, limit: number = 10) {
  const [recent, setRecent] = useState<RecentValue[]>([]);

  const load = useCallback(async () => {
    if (!fieldKey) return;
    try {
      const r = await api.get('/UserPrefs/field-history', {
        params: { fieldKey, limit },
      });
      const items: any[] = r.data?.data ?? r.data ?? [];
      setRecent(
        (Array.isArray(items) ? items : []).map((x) => ({
          value: x.value,
          lastUsedAt: x.lastUsedAt,
          usageCount: x.usageCount,
        })),
      );
    } catch {
      setRecent([]);
    }
  }, [fieldKey, limit]);

  useEffect(() => {
    load();
  }, [load]);

  const record = useCallback(
    async (value: string) => {
      const trimmed = (value ?? '').trim();
      if (!fieldKey || !trimmed) return;
      try {
        await api.post('/UserPrefs/field-history', { fieldKey, value: trimmed });
        // Optimistic local reorder: move the recorded value to the front.
        setRecent((prev) => {
          const without = prev.filter((p) => p.value !== trimmed);
          return [
            { value: trimmed, lastUsedAt: new Date().toISOString(), usageCount: 1 },
            ...without,
          ].slice(0, limit);
        });
      } catch {
        /* best-effort — recent-values aren't critical to form submission */
      }
    },
    [fieldKey, limit],
  );

  return { recent, record, refresh: load };
}

export default useFieldHistory;
