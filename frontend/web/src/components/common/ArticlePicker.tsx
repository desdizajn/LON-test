import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { itemsApi, ArticlePickerGroup, ArticlePickerVariant } from '../../services/masterDataApi';

interface Props {
  value: string | null;
  onChange: (variant: ArticlePickerVariant | null) => void;
  placeholder?: string;
  disabled?: boolean;
  autoFocus?: boolean;
}

/**
 * P5.3.4 — article picker that surfaces "A"-suffix tariff variants beside the
 * base code. Groups by normalised base so the user sees both options and
 * picks the right tariff deliberately.
 */
const ArticlePicker: React.FC<Props> = ({ value, onChange, placeholder, disabled, autoFocus }) => {
  const { t } = useTranslation();
  const [query, setQuery] = useState('');
  const [groups, setGroups] = useState<ArticlePickerGroup[]>([]);
  const [loading, setLoading] = useState(false);
  const [open, setOpen] = useState(false);
  const [selectedCode, setSelectedCode] = useState<string | null>(value);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setSelectedCode(value);
  }, [value]);

  useEffect(() => {
    if (!open) return;
    const handler = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  useEffect(() => {
    let cancelled = false;
    const controller = new AbortController();
    if (!open) return () => controller.abort();
    const debounce = setTimeout(async () => {
      setLoading(true);
      try {
        const resp = await itemsApi.articlePicker(query, 20);
        if (!cancelled) setGroups(resp.data);
      } catch {
        if (!cancelled) setGroups([]);
      } finally {
        if (!cancelled) setLoading(false);
      }
    }, 180);
    return () => {
      cancelled = true;
      clearTimeout(debounce);
      controller.abort();
    };
  }, [query, open]);

  const hasResults = useMemo(() => groups.some((g) => g.variants.length > 0), [groups]);

  return (
    <div ref={containerRef} style={{ position: 'relative' }}>
      <input
        type="text"
        value={open ? query : (selectedCode ?? '')}
        onChange={(e) => {
          setQuery(e.target.value);
          if (!open) setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        placeholder={placeholder ?? t('articlePicker.placeholder') ?? 'Article code'}
        disabled={disabled}
        autoFocus={autoFocus}
        style={{ width: '100%', padding: '6px 8px', border: '1px solid #ccc', borderRadius: 4 }}
      />
      {open && (
        <div
          style={{
            position: 'absolute',
            top: '100%',
            left: 0,
            right: 0,
            zIndex: 50,
            maxHeight: 360,
            overflowY: 'auto',
            background: 'white',
            border: '1px solid #ccc',
            borderRadius: 4,
            boxShadow: '0 4px 8px rgba(0,0,0,0.08)',
          }}
        >
          {loading && (
            <div style={{ padding: 8, color: '#888' }}>{t('common.loading')}</div>
          )}
          {!loading && !hasResults && (
            <div style={{ padding: 8, color: '#888' }}>
              {query.trim().length === 0 ? t('articlePicker.typePrompt') : t('articlePicker.noResults')}
            </div>
          )}
          {!loading &&
            groups.map((g) => (
              <div key={g.baseCode} style={{ borderBottom: '1px solid #eee' }}>
                <div
                  style={{
                    padding: '4px 8px',
                    background: '#f8f8f8',
                    fontSize: 11,
                    color: '#666',
                    textTransform: 'uppercase',
                  }}
                >
                  {t('articlePicker.base')}: {g.baseCode}
                </div>
                {g.variants.map((v) => (
                  <button
                    type="button"
                    key={v.id}
                    onClick={() => {
                      setSelectedCode(v.code);
                      setOpen(false);
                      setQuery('');
                      onChange(v);
                    }}
                    style={{
                      display: 'flex',
                      flexDirection: 'column',
                      alignItems: 'flex-start',
                      gap: 2,
                      width: '100%',
                      padding: '8px 12px',
                      background: 'white',
                      border: 'none',
                      borderTop: '1px solid #f5f5f5',
                      cursor: 'pointer',
                      textAlign: 'left',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <strong>{v.code}</strong>
                      {v.isASuffix && (
                        <span
                          title={t('articlePicker.aSuffixHint') as string}
                          style={{
                            background: '#ffb020',
                            color: '#000',
                            borderRadius: 3,
                            padding: '1px 6px',
                            fontSize: 10,
                            fontWeight: 'bold',
                          }}
                        >
                          A
                        </span>
                      )}
                    </div>
                    <div style={{ fontSize: 12, color: '#555' }}>{v.name}</div>
                    <div style={{ fontSize: 11, color: '#888' }}>
                      {v.hsCode && `${t('articlePicker.tariff')}: ${v.hsCode}`}
                      {v.hsCode && v.countryOfOrigin && ' · '}
                      {v.countryOfOrigin && `${t('articlePicker.origin')}: ${v.countryOfOrigin}`}
                    </div>
                  </button>
                ))}
              </div>
            ))}
        </div>
      )}
    </div>
  );
};

export default ArticlePicker;
