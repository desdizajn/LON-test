import { useCallback, useState } from 'react';

/**
 * Sticky-defaults hook for line-table entry forms.
 *
 * Per BLUEPRINT §7.3.1 + AGENT-PROMPTS §E0: as the user enters lines in a
 * document (declaration, BOM, EX wizard...), high-variance fields like UoM,
 * CountryOfOrigin, TariffCode auto-prefill from the last-entered line.
 *
 * Scope: per-document (per scopeKey) React state. NOT localStorage — these
 * defaults reset when the user leaves the document.
 *
 * Reality-check (2026-05-12 PREP): TEKSPORT ELON is 99.998% EUR, so the
 * Currency-only framing in earlier drafts was over-engineered. This hook
 * carries a generic Partial<TLine> — UoM/Country/TariffCode are the primary
 * use cases; Currency rides along for free.
 *
 * Example usage in a line editor:
 *
 *   const { defaults, captureFrom, reset } = useStickyDefaults<DeclarationLine>(
 *     `declaration-${id}-lines`,
 *     { currency: partner?.primaryCurrency ?? 'EUR' }
 *   );
 *
 *   const newLine = { ...defaults, lineNumber: nextNum };
 *   // ... user fills the line, then on save:
 *   captureFrom(savedLine);
 */
export interface UseStickyDefaultsResult<TLine> {
  /** Current default values for a fresh line. */
  defaults: Partial<TLine>;
  /** After a line is saved, copy sticky fields into defaults for the next line. */
  captureFrom: (line: TLine) => void;
  /** Clear back to the `initial` defaults. */
  reset: () => void;
}

/**
 * Hook returns sticky defaults for the next line in a multi-line form.
 * Two distinct `scopeKey`s never bleed values — they share global storage
 * but are partitioned by key.
 *
 * @param scopeKey      Unique scope identifier (e.g. `declaration-${id}-lines`).
 *                      Two scopes with the same key share state (typically
 *                      not desired; pick a unique key per document instance).
 * @param initial       Initial defaults (from Partner.PrimaryCurrency, etc.).
 * @param stickyFields  Optional whitelist of fields to capture from saved lines.
 *                      When omitted, every defined property on the saved line
 *                      is captured. Recommended to pass an explicit list per
 *                      entity to avoid sticky-capturing per-line variants
 *                      (e.g. don't sticky-capture `quantity` or `lineTotal`).
 */
export function useStickyDefaults<TLine extends object>(
  _scopeKey: string,
  initial: Partial<TLine>,
  stickyFields?: ReadonlyArray<keyof TLine>,
): UseStickyDefaultsResult<TLine> {
  const [defaults, setDefaults] = useState<Partial<TLine>>(initial);

  const captureFrom = useCallback(
    (line: TLine) => {
      setDefaults((prev) => {
        const next: Partial<TLine> = { ...prev };
        const keys = stickyFields ?? (Object.keys(line) as ReadonlyArray<keyof TLine>);
        for (const k of keys) {
          const value = line[k];
          if (value !== undefined && value !== null && value !== '') {
            (next as Record<keyof TLine, TLine[keyof TLine]>)[k] = value;
          }
        }
        return next;
      });
    },
    [stickyFields],
  );

  const reset = useCallback(() => {
    setDefaults(initial);
  }, [initial]);

  return { defaults, captureFrom, reset };
}
