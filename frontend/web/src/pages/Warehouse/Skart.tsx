import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { wmsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity, formatDate } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P15.3 — Skart (defective-on-intake) register. Lists every Skart report
 * for the current tenant with an inline Resolve action. Creating a new
 * Skart is initiated from the parent Receipt detail (separate P15.3.1
 * wire-up) so the operator doesn't have to hunt for the correct receipt
 * line from a generic dropdown.
 */

type SkartRow = {
  id: string;
  skartNumber: string;
  reportedAt: string;
  receiptLineId: string;
  receiptNumber?: string | null;
  itemId: string;
  itemCode: string;
  itemName: string;
  batchNumber?: string | null;
  mrn?: string | null;
  skartQuantity: number;
  uoMId: string;
  uoMCode: string;
  reason: string;
  resolution: number;
  resolvedAt?: string | null;
  resolutionNote?: string | null;
};

const RESOLUTION_LABELS: Record<number, string> = {
  0: 'skart.resolution.open',
  1: 'skart.resolution.returnedToSupplier',
  2: 'skart.resolution.destroyed',
  3: 'skart.resolution.acceptedAtDiscount',
};

const Skart: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<SkartRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [filter, setFilter] = useState<'open' | 'all'>('open');
  const [search, setSearch] = useState('');
  const [resolving, setResolving] = useState<SkartRow | null>(null);
  const [resolveChoice, setResolveChoice] = useState(1);
  const [resolveNote, setResolveNote] = useState('');
  const [saving, setSaving] = useState(false);

  async function load() {
    setLoading(true);
    try {
      const resp = await wmsApi.getSkart({ openOnly: filter === 'open' });
      setRows((resp.data as SkartRow[]) ?? []);
    } catch (err) {
      toast.error(translateError(err));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filter]);

  const filtered = useMemo(() => {
    const s = search.trim().toLowerCase();
    if (!s) return rows;
    return rows.filter(
      (r) =>
        r.skartNumber.toLowerCase().includes(s) ||
        r.itemCode.toLowerCase().includes(s) ||
        r.itemName.toLowerCase().includes(s) ||
        (r.batchNumber || '').toLowerCase().includes(s) ||
        (r.mrn || '').toLowerCase().includes(s) ||
        r.reason.toLowerCase().includes(s)
    );
  }, [rows, search]);

  const openResolve = (row: SkartRow) => {
    setResolving(row);
    setResolveChoice(1);
    setResolveNote('');
  };

  const submitResolve = async () => {
    if (!resolving) return;
    setSaving(true);
    try {
      await wmsApi.resolveSkart(resolving.id, {
        resolution: resolveChoice,
        resolutionNote: resolveNote || undefined,
      });
      toast.success(t('skart.resolveSuccess', 'Шкартот е затворен.'));
      setResolving(null);
      load();
    } catch (err) {
      toast.error(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  const exportCsv = () => {
    exportToCsv(
      filtered,
      [
        { key: 'skartNumber', label: 'SkartNumber' },
        { key: 'reportedAt', label: 'ReportedAt', type: 'date' },
        { key: 'receiptNumber', label: 'Receipt' },
        { key: 'itemCode', label: 'ItemCode' },
        { key: 'itemName', label: 'ItemName' },
        { key: 'batchNumber', label: 'Batch' },
        { key: 'mrn', label: 'MRN' },
        { key: 'skartQuantity', label: 'Qty', type: 'number', decimals: 4 },
        { key: 'uoMCode', label: 'UoM' },
        { key: 'reason', label: 'Reason' },
        { key: 'resolution', label: 'Resolution', get: (r) => t(RESOLUTION_LABELS[r.resolution] || '') },
        { key: 'resolvedAt', label: 'ResolvedAt', type: 'date' },
        { key: 'resolutionNote', label: 'Note' },
      ],
      'skart'
    );
  };

  return (
    <div style={{ padding: 20 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 20 }}>
        <div>
          <h1 style={{ margin: 0 }}>{t('skart.title', 'Шкарт регистар')}</h1>
          <div style={{ color: '#666', fontSize: 13, marginTop: 5 }}>
            {t(
              'skart.subtitle',
              'Дефект на прием (legacy FakturaU5Skart). Количината е блокирана на истата локација со QualityStatus=Blocked — не влегува во производство додека не е решено.'
            )}
          </div>
        </div>
        <button onClick={exportCsv} className="btn btn-outline">
          {t('common.exportCsv', 'Извоз CSV')}
        </button>
      </div>

      <div style={{ display: 'flex', gap: 15, marginBottom: 15, alignItems: 'center', flexWrap: 'wrap' }}>
        <input
          placeholder={t('skart.searchPlaceholder', 'Барај по број, код, batch, MRN, причина...') as string}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          style={{ flex: 1, minWidth: 240, padding: 8, border: '1px solid #ccc', borderRadius: 4 }}
        />
        <select value={filter} onChange={(e) => setFilter(e.target.value as any)} style={{ padding: 8 }}>
          <option value="open">{t('skart.filterOpen', 'Само отворени')}</option>
          <option value="all">{t('skart.filterAll', 'Сите')}</option>
        </select>
      </div>

      {loading && <div className="loading">{t('common.loading')}</div>}
      {!loading && filtered.length === 0 && (
        <div style={{ padding: 40, textAlign: 'center', color: '#888' }}>
          {t('skart.empty', 'Нема пријавен шкарт.')}
        </div>
      )}
      {!loading && filtered.length > 0 && (
        <table className="data-table">
          <thead>
            <tr>
              <th>{t('skart.col.number', 'Број')}</th>
              <th>{t('skart.col.date', 'Датум')}</th>
              <th>{t('skart.col.receipt', 'Прием')}</th>
              <th>{t('skart.col.item', 'Артикл')}</th>
              <th>{t('skart.col.batch', 'Batch')}</th>
              <th>{t('skart.col.mrn', 'MRN')}</th>
              <th style={{ textAlign: 'right' }}>{t('skart.col.qty', 'Колич.')}</th>
              <th>{t('skart.col.reason', 'Причина')}</th>
              <th>{t('skart.col.resolution', 'Решение')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((r) => {
              const isOpen = r.resolution === 0;
              return (
                <tr key={r.id}>
                  <td><code>{r.skartNumber}</code></td>
                  <td>{formatDate(r.reportedAt)}</td>
                  <td>{r.receiptNumber || <span style={{ color: '#aaa' }}>—</span>}</td>
                  <td>
                    <div style={{ fontWeight: 600 }}>{r.itemCode}</div>
                    <div style={{ fontSize: 12, color: '#888' }}>{r.itemName}</div>
                  </td>
                  <td>{r.batchNumber || <span style={{ color: '#aaa' }}>—</span>}</td>
                  <td>{r.mrn || <span style={{ color: '#aaa' }}>—</span>}</td>
                  <td style={{ textAlign: 'right', fontFamily: 'monospace' }}>
                    {formatQuantity(r.skartQuantity)} {r.uoMCode}
                  </td>
                  <td style={{ maxWidth: 240 }}>{r.reason}</td>
                  <td>
                    <span
                      style={{
                        padding: '3px 8px',
                        borderRadius: 10,
                        fontSize: 12,
                        background: isOpen ? '#fff3cd' : '#d4edda',
                        color: isOpen ? '#856404' : '#155724',
                      }}
                    >
                      {t(RESOLUTION_LABELS[r.resolution] || '')}
                    </span>
                    {r.resolvedAt && (
                      <div style={{ fontSize: 11, color: '#888', marginTop: 3 }}>
                        {formatDate(r.resolvedAt)}
                      </div>
                    )}
                  </td>
                  <td>
                    {isOpen && (
                      <button className="btn btn-sm btn-primary" onClick={() => openResolve(r)}>
                        {t('skart.resolve', 'Затвори')}
                      </button>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}

      {resolving && (
        <div
          onClick={() => !saving && setResolving(null)}
          style={{
            position: 'fixed',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            background: 'rgba(0,0,0,0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
          }}
        >
          <div
            onClick={(e) => e.stopPropagation()}
            style={{
              background: 'white',
              padding: 25,
              borderRadius: 8,
              minWidth: 420,
              maxWidth: 500,
            }}
          >
            <h3 style={{ marginTop: 0 }}>
              {t('skart.resolveTitle', 'Решение на шкарт')}: <code>{resolving.skartNumber}</code>
            </h3>
            <div style={{ fontSize: 13, color: '#666', marginBottom: 15 }}>
              {resolving.itemCode} · {formatQuantity(resolving.skartQuantity)} {resolving.uoMCode}
            </div>
            <div style={{ marginBottom: 15 }}>
              <label style={{ display: 'block', marginBottom: 5 }}>
                {t('skart.resolveAction', 'Избери исход')}:
              </label>
              <select
                value={resolveChoice}
                onChange={(e) => setResolveChoice(Number(e.target.value))}
                style={{ width: '100%', padding: 8 }}
              >
                <option value={1}>{t('skart.resolution.returnedToSupplier', 'Враќање кон добавувач')}</option>
                <option value={2}>{t('skart.resolution.destroyed', 'Уништено (scrappage)')}</option>
                <option value={3}>{t('skart.resolution.acceptedAtDiscount', 'Прифатено со попуст')}</option>
              </select>
            </div>
            <div style={{ marginBottom: 15 }}>
              <label style={{ display: 'block', marginBottom: 5 }}>
                {t('skart.resolveNote', 'Забелешка')} ({t('common.optional', 'опционално')}):
              </label>
              <textarea
                rows={3}
                value={resolveNote}
                onChange={(e) => setResolveNote(e.target.value)}
                style={{ width: '100%', padding: 8, border: '1px solid #ccc', borderRadius: 4 }}
                placeholder={t('skart.resolveNotePlaceholder', 'Debit note #, датум на враќање, итн.') as string}
              />
            </div>
            <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end' }}>
              <button className="btn btn-outline" onClick={() => setResolving(null)} disabled={saving}>
                {t('common.cancel', 'Откажи')}
              </button>
              <button className="btn btn-primary" onClick={submitResolve} disabled={saving}>
                {saving ? t('common.saving', 'Се зачувува...') : t('skart.confirmResolve', 'Потврди')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default Skart;
