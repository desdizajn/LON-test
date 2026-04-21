import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

export interface SearchableOption {
  value: string;
  label: string;
  hint?: string;
}

interface Props {
  value: string;
  onChange: (value: string, option: SearchableOption | null) => void;
  options: SearchableOption[];
  placeholder?: string;
  disabled?: boolean;
  allowClear?: boolean;
  minWidth?: number | string;
  emptyMessage?: string;
  loading?: boolean;
  onOpen?: () => void;
}

/**
 * Generic searchable dropdown. Unlike ArticlePicker (which is Items-specific),
 * this takes a plain option list so callers reuse it for MRNs, batches,
 * references, partners, locations — anything list-like where text search
 * over a finite set beats a bare <select>.
 *
 * The component is controlled: caller owns the selected `value`. The input
 * displays the matched option's label while closed; typing reopens it.
 */
const SearchableSelect: React.FC<Props> = ({
  value,
  onChange,
  options,
  placeholder,
  disabled,
  allowClear = true,
  minWidth,
  emptyMessage,
  loading,
  onOpen,
}) => {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const ref = useRef<HTMLDivElement>(null);

  const selected = useMemo(() => options.find((o) => o.value === value) ?? null, [options, value]);

  useEffect(() => {
    if (!open) return;
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false);
        setQuery('');
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return options;
    return options.filter(
      (o) => o.label.toLowerCase().includes(q) || (o.hint ?? '').toLowerCase().includes(q)
    );
  }, [options, query]);

  const displayValue = open ? query : (selected?.label ?? '');

  return (
    <div ref={ref} style={{ position: 'relative', width: '100%', minWidth }}>
      <div style={{ position: 'relative' }}>
        <input
          type="text"
          value={displayValue}
          onChange={(e) => {
            setQuery(e.target.value);
            if (!open) {
              setOpen(true);
              onOpen?.();
            }
          }}
          onFocus={() => {
            if (!open) {
              setOpen(true);
              onOpen?.();
            }
          }}
          placeholder={placeholder ?? (t('common.searchPlaceholder') as string)}
          disabled={disabled}
          style={{
            width: '100%',
            padding: '6px 28px 6px 8px',
            border: '1px solid var(--border-strong, #ccc)',
            borderRadius: 4,
            background: disabled ? 'var(--ink-50, #f5f5f5)' : 'white',
          }}
        />
        {allowClear && !!value && !disabled && (
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              onChange('', null);
              setQuery('');
            }}
            aria-label={t('common.clear') as string}
            style={{
              position: 'absolute',
              right: 4,
              top: '50%',
              transform: 'translateY(-50%)',
              border: 'none',
              background: 'transparent',
              cursor: 'pointer',
              color: '#888',
              padding: '0 6px',
              fontSize: 14,
            }}
          >
            ×
          </button>
        )}
      </div>

      {open && (
        <div
          style={{
            position: 'absolute',
            top: '100%',
            left: 0,
            right: 0,
            zIndex: 50,
            maxHeight: 280,
            overflowY: 'auto',
            background: 'white',
            border: '1px solid var(--border-strong, #ccc)',
            borderRadius: 4,
            boxShadow: '0 4px 12px rgba(0,0,0,0.08)',
            marginTop: 2,
          }}
        >
          {loading && (
            <div style={{ padding: 10, color: '#888' }}>{t('common.loading')}</div>
          )}
          {!loading && filtered.length === 0 && (
            <div style={{ padding: 10, color: '#888' }}>
              {emptyMessage ?? (t('common.noResults') as string)}
            </div>
          )}
          {!loading &&
            filtered.map((o) => (
              <button
                key={o.value}
                type="button"
                onClick={() => {
                  onChange(o.value, o);
                  setOpen(false);
                  setQuery('');
                }}
                style={{
                  display: 'block',
                  width: '100%',
                  padding: '8px 12px',
                  background: o.value === value ? 'var(--taris-blue-50, #e7f2fe)' : 'white',
                  border: 'none',
                  borderTop: '1px solid #f5f5f5',
                  cursor: 'pointer',
                  textAlign: 'left',
                }}
              >
                <div style={{ fontWeight: 500 }}>{o.label}</div>
                {o.hint && <div style={{ fontSize: 12, color: '#777' }}>{o.hint}</div>}
              </button>
            ))}
        </div>
      )}
    </div>
  );
};

export default SearchableSelect;
