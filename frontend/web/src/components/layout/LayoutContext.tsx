import React, { createContext, useCallback, useContext, useEffect, useState } from 'react';

/**
 * Shared UI state for the app shell:
 *   • mobileNavOpen — drawer open/closed on < 900px breakpoints.
 *
 * Exposed through a context so the TopBar's hamburger and the Sidebar's
 * close handler both live in the same source of truth without prop-drilling
 * through ProtectedLayout.
 */
type LayoutState = {
  mobileNavOpen: boolean;
  openMobileNav: () => void;
  closeMobileNav: () => void;
  toggleMobileNav: () => void;
};

const LayoutContext = createContext<LayoutState | null>(null);

export const LayoutProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [mobileNavOpen, setMobileNavOpen] = useState(false);

  const openMobileNav = useCallback(() => setMobileNavOpen(true), []);
  const closeMobileNav = useCallback(() => setMobileNavOpen(false), []);
  const toggleMobileNav = useCallback(() => setMobileNavOpen((v) => !v), []);

  // Close on Escape for accessibility.
  useEffect(() => {
    if (!mobileNavOpen) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setMobileNavOpen(false); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [mobileNavOpen]);

  // Lock body scroll while drawer is open so the backdrop feels solid.
  useEffect(() => {
    if (mobileNavOpen) {
      const prev = document.body.style.overflow;
      document.body.style.overflow = 'hidden';
      return () => { document.body.style.overflow = prev; };
    }
  }, [mobileNavOpen]);

  return (
    <LayoutContext.Provider value={{ mobileNavOpen, openMobileNav, closeMobileNav, toggleMobileNav }}>
      {children}
    </LayoutContext.Provider>
  );
};

export const useLayout = (): LayoutState => {
  const ctx = useContext(LayoutContext);
  if (!ctx) throw new Error('useLayout must be used inside LayoutProvider');
  return ctx;
};
