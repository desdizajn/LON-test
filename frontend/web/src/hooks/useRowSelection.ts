import { useCallback, useMemo, useState } from 'react';

/**
 * Row-selection helper. `keys` is the current visible keyset (e.g. filtered
 * ids); on filter change we prune selections that no longer apply. The hook
 * exposes Set membership + batch toggles for header-level select-all.
 */
export function useRowSelection<T>(rows: T[], getKey: (row: T) => string) {
  const [selected, setSelected] = useState<Set<string>>(new Set());

  const visibleKeys = useMemo(() => new Set(rows.map(getKey)), [rows, getKey]);

  const prunedSelected = useMemo(() => {
    const pruned = new Set<string>();
    selected.forEach((k) => {
      if (visibleKeys.has(k)) pruned.add(k);
    });
    return pruned;
  }, [selected, visibleKeys]);

  const isSelected = useCallback((key: string) => prunedSelected.has(key), [prunedSelected]);

  const toggle = useCallback((key: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }, []);

  const allVisibleSelected = useMemo(
    () => rows.length > 0 && rows.every((r) => prunedSelected.has(getKey(r))),
    [rows, prunedSelected, getKey]
  );

  const someVisibleSelected = useMemo(
    () => !allVisibleSelected && rows.some((r) => prunedSelected.has(getKey(r))),
    [rows, prunedSelected, getKey, allVisibleSelected]
  );

  const toggleAllVisible = useCallback(() => {
    setSelected((prev) => {
      const next = new Set(prev);
      const everyVisible = rows.every((r) => next.has(getKey(r)));
      if (everyVisible) {
        rows.forEach((r) => next.delete(getKey(r)));
      } else {
        rows.forEach((r) => next.add(getKey(r)));
      }
      return next;
    });
  }, [rows, getKey]);

  const clear = useCallback(() => setSelected(new Set()), []);

  const selectedRows = useMemo(
    () => rows.filter((r) => prunedSelected.has(getKey(r))),
    [rows, prunedSelected, getKey]
  );

  return {
    selected: prunedSelected,
    selectedKeys: Array.from(prunedSelected),
    selectedRows,
    isSelected,
    toggle,
    toggleAllVisible,
    allVisibleSelected,
    someVisibleSelected,
    clear,
    count: prunedSelected.size,
  };
}
