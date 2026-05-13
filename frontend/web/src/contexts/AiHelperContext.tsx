import React, { createContext, useContext, useMemo, useState, useCallback } from 'react';

/**
 * Phase 17 §E10 — page-set context for the floating AI helper drawer.
 *
 * Each page that wants the recommendations tab to light up calls
 * `useSetAiContext({ entityType, entityId })` on mount; the AiHelperButton
 * (mounted globally in App.tsx) reads the value and fetches structured
 * recommendations from POST /api/Ai/recommendations.
 */
export interface AiContextValue {
  entityType: string | null;
  entityId: string | null;
}

interface AiHelperContextShape extends AiContextValue {
  setContext: (next: AiContextValue) => void;
  clearContext: () => void;
}

const AiHelperContext = createContext<AiHelperContextShape>({
  entityType: null,
  entityId: null,
  setContext: () => {},
  clearContext: () => {},
});

export const AiHelperContextProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [state, setState] = useState<AiContextValue>({ entityType: null, entityId: null });

  const setContext = useCallback((next: AiContextValue) => {
    setState(prev => {
      if (prev.entityType === next.entityType && prev.entityId === next.entityId) return prev;
      return next;
    });
  }, []);

  const clearContext = useCallback(() => {
    setState(prev => {
      if (prev.entityType === null && prev.entityId === null) return prev;
      return { entityType: null, entityId: null };
    });
  }, []);

  const value = useMemo<AiHelperContextShape>(
    () => ({ entityType: state.entityType, entityId: state.entityId, setContext, clearContext }),
    [state.entityType, state.entityId, setContext, clearContext],
  );

  return <AiHelperContext.Provider value={value}>{children}</AiHelperContext.Provider>;
};

export const useAiHelperContext = (): AiHelperContextShape => useContext(AiHelperContext);

/**
 * Page-level helper: declare the entity that drives recommendations.
 * Set both fields to null to clear (defaults to clearing on unmount).
 */
export const useSetAiContext = (entityType: string | null, entityId: string | null): void => {
  const { setContext, clearContext } = useAiHelperContext();
  React.useEffect(() => {
    if (entityType && entityId) {
      setContext({ entityType, entityId });
    } else {
      clearContext();
    }
    return () => clearContext();
  }, [entityType, entityId, setContext, clearContext]);
};
