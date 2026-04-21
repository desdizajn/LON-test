import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { wmsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';
import SearchableSelect, { SearchableOption } from '../../components/common/SearchableSelect';
import BulkActionBar, { BulkAction } from '../../components/common/BulkActionBar';
import { useRowSelection } from '../../hooks/useRowSelection';

/**
 * P7.2 — "Блокирано од QC": InventoryBalance rows with QualityStatus != OK.
 *
 * Shares data with the legacy BlockedInventory report but adds a focused
 * release action and quarantine/rejected split. Release flips QualityStatus
 * back to OK via the existing POST /wms/inventory/quality-status endpoint.
 */

type Balance = {
  id: string;
  itemId: string;
  locationId: string;
  batchNumber?: string | null;
  mrn?: string | null;
  quantity: number;
  qualityStatus: number;
  expiryDate?: string | null;
  item?: { code: string; name: string } | null;
  location?: { id?: string; code: string; name: string } | null;
  uoM?: { code: string; name: string } | null;
};

const QcHold: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Balance[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<'all' | 'blocked' | 'quarantine'>('all');
  const [itemSearch, setItemSearch] = useState('');
  const [fLocation, setFLocation] = useState('');
  const [fBatch, setFBatch] = useState('');
  const [fMrn, setFMrn] = useState('');
  const [busyId, setBusyId] = useState<string | null>(null);
  const [bulkModal, setBulkModal] = useState<null | { reason: string }>(null);
  const [bulkRunning, setBulkRunning] = useState(false);

  async function load() {
    setLoading(true);
    try {
      const resp = await wmsApi.getInventory();
      const all = (resp.data as Balance[]) ?? [];
      setRows(all.filter((r) => r.qualityStatus !== 1 && r.quantity > 0));
    } catch (err) {
      setError(translateError(err));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
  }, []);

  const locationOptions = useMemo<SearchableOption[]>(() => {
    const set = new Map<string, string>();
    rows.forEach((r) => {
      if (r.location?.id && r.location?.code) {
        set.set(r.location.id, `${r.location.code}${r.location.name ? ' · ' + r.location.name : ''}`);
      }
    });
    return Array.from(set.entries()).sort((a, b) => a[1].localeCompare(b[1])).map(([id, label]) => ({ value: id, label }));
  }, [rows]);

  const batchOptions = useMemo<SearchableOption[]>(() => {
    const s = new Set<string>();
    rows.forEach((r) => r.batchNumber && s.add(r.batchNumber));
    return Array.from(s).sort().map((b) => ({ value: b, label: b }));
  }, [rows]);

  const mrnOptions = useMemo<SearchableOption[]>(() => {
    const s = new Set<string>();
    rows.forEach((r) => r.mrn && s.add(r.mrn));
    return Array.from(s).sort().map((m) => ({ value: m, label: m }));
  }, [rows]);

  const filtered = useMemo(() => {
    const iq = itemSearch.trim().toLowerCase();
    return rows.filter((r) => {
      if (filter === 'blocked' && r.qualityStatus !== 2) return false;
      if (filter === 'quarantine' && r.qualityStatus !== 3) return false;
      if (iq) {
        const hay = `${r.item?.code ?? ''} ${r.item?.name ?? ''}`.toLowerCase();
        if (!hay.includes(iq)) return false;
      }
      if (fLocation && r.location?.id !== fLocation) return false;
      if (fBatch && r.batchNumber !== fBatch) return false;
      if (fMrn && r.mrn !== fMrn) return false;
      return true;
    });
  }, [rows, filter, itemSearch, fLocation, fBatch, fMrn]);

  const counts = useMemo(() => ({
    blocked: rows.filter((r) => r.qualityStatus === 2).length,
    quarantine: rows.filter((r) => r.qualityStatus === 3).length,
  }), [rows]);

  const selection = useRowSelection(filtered, (r) => r.id);
  const selectedQty = selection.selectedRows.reduce((s, r) => s + r.quantity, 0);

  async function releaseOne(id: string) {
    if (!window.confirm(t('qcHold.releaseConfirm') as string)) return;
    setBusyId(id);
    try {
      await wmsApi.updateQualityStatus({
        inventoryBalanceId: id,
        newQualityStatus: 1,
        reason: 'Released from QC hold',
        notes: 'Released via /warehouse/qc-hold',
      });
      await load();
    } catch (err) {
      setError(translateError(err));
    } finally {
      setBusyId(null);
    }
  }

  async function runBulkRelease(reason: string) {
    const picked = selection.selectedRows;
    if (picked.length === 0) return;
    setBulkRunning(true);
    let ok = 0;
    let failed = 0;
    const firstError: string[] = [];
    for (const r of picked) {
      try {
        await wmsApi.updateQualityStatus({
          inventoryBalanceId: r.id,
          newQualityStatus: 1,
          reason,
          notes: 'Released via /warehouse/qc-hold bulk release',
        });
        ok++;
      } catch (err: any) {
        failed++;
        if (firstError.length === 0) firstError.push(translateError(err));
      }
    }
    setBulkRunning(false);
    setBulkModal(null);
    selection.clear();
    await load();
    if (failed === 0) {
      toast.success(t('qcHold.bulkReleaseSuccess', { count: ok }) as string);
    } else {
      toast.error(t('qcHold.bulkReleasePartial', { ok, failed, first: firstError[0] ?? '' }) as string);
    }
  }

  const bulkActions: BulkAction[] = [
    {
      key: 'release',
      label: t('qcHold.bulkReleaseAction') as string,
      variant: 'primary',
      onClick: () => setBulkModal({ reason: '' }),
    },
    {
      key: 'export',
      label: t('common.exportExcel') as string,
      onClick: () =>
        exportToCsv(
          selection.selectedRows,
          [
            { key: 'item', label: t('common.code') as string, get: (r: Balance) => r.item?.code ?? '' },
            { key: 'item', label: t('common.name') as string, get: (r: Balance) => r.item?.name ?? '' },
            { key: 'location', label: t('qcHold.location') as string, get: (r: Balance) => r.location?.code ?? '' },
            { key: 'batchNumber', label: 'Batch' },
            { key: 'mrn', label: 'MRN' },
            { key: 'quantity', label: t('common.quantity') as string, type: 'number' },
            { key: 'qualityStatus', label: 'Status', get: (r: Balance) => r.qualityStatus === 2 ? t('qcHold.blocked') as string : t('qcHold.quarantine') as string },
          ],
          'qc-hold-selected'
        ),
    },
  ];

  const clearAllFilters = () => {
    setItemSearch('');
    setFLocation('');
    setFBatch('');
    setFMrn('');
  };
  const hasAnyFilter = !!itemSearch || !!fLocation || !!fBatch || !!fMrn;

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('qcHold.title')}</h1>
      <p style={{ color: '#666' }}>{t('qcHold.subtitle')}</p>

      <div style={{ display: 'flex', gap: 8, marginBottom: 10, flexWrap: 'wrap' }}>
        <button onClick={() => setFilter('all')} style={{ padding: '6px 12px', fontWeight: filter === 'all' ? 'bold' : 'normal' }}>
          {t('qcHold.all')} ({rows.length})
        </button>
        <button onClick={() => setFilter('blocked')} style={{ padding: '6px 12px', fontWeight: filter === 'blocked' ? 'bold' : 'normal' }}>
          🚫 {t('qcHold.blocked')} ({counts.blocked})
        </button>
        <button onClick={() => setFilter('quarantine')} style={{ padding: '6px 12px', fontWeight: filter === 'quarantine' ? 'bold' : 'normal' }}>
          ⏳ {t('qcHold.quarantine')} ({counts.quarantine})
        </button>
      </div>

      <div style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
        gap: 8,
        padding: 10,
        background: 'var(--ink-50, #f8fafc)',
        border: '1px solid var(--border, #e5e7eb)',
        borderRadius: 6,
        marginBottom: 10,
      }}>
        <input type="text" value={itemSearch} onChange={(e) => setItemSearch(e.target.value)} placeholder={t('inventory.filters.itemPlaceholder') as string} style={{ padding: 6 }} />
        <SearchableSelect value={fLocation} onChange={(v) => setFLocation(v)} options={locationOptions} placeholder={t('inventory.filters.locationPlaceholder') as string} />
        <SearchableSelect value={fBatch} onChange={(v) => setFBatch(v)} options={batchOptions} placeholder={t('inventory.filters.batchPlaceholder') as string} />
        <SearchableSelect value={fMrn} onChange={(v) => setFMrn(v)} options={mrnOptions} placeholder={t('inventory.filters.mrnPlaceholder') as string} />
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <span style={{ fontSize: 12, color: '#666' }}>{t('inventory.filters.showing', { count: filtered.length, total: rows.length })}</span>
          {hasAnyFilter && <button type="button" onClick={clearAllFilters} style={{ padding: '4px 10px', fontSize: 12 }}>{t('inventory.filters.clear')}</button>}
          <button
            type="button"
            onClick={() => exportToCsv(filtered,
              [
                { key: 'item', label: t('common.code') as string, get: (r: Balance) => r.item?.code ?? '' },
                { key: 'item', label: t('common.name') as string, get: (r: Balance) => r.item?.name ?? '' },
                { key: 'location', label: t('qcHold.location') as string, get: (r: Balance) => r.location?.code ?? '' },
                { key: 'batchNumber', label: 'Batch' },
                { key: 'mrn', label: 'MRN' },
                { key: 'quantity', label: t('common.quantity') as string, type: 'number' },
                { key: 'qualityStatus', label: 'Status', get: (r: Balance) => r.qualityStatus === 2 ? t('qcHold.blocked') as string : t('qcHold.quarantine') as string },
              ],
              'qc-hold'
            )}
            disabled={filtered.length === 0}
            style={{ padding: '4px 10px', fontSize: 12, marginLeft: 'auto' }}
          >{t('common.exportExcel')}</button>
        </div>
      </div>

      <BulkActionBar
        selectedCount={selection.count}
        totalCount={filtered.length}
        actions={bulkActions}
        onClearSelection={selection.clear}
        summary={selection.count > 0 ? (t('inventory.bulkSummary', { qty: selectedQty.toFixed(2) }) as string) : undefined}
      />

      {error && (
        <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>
          {error}
        </div>
      )}

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th style={{ width: 34 }}>
                <input
                  type="checkbox"
                  checked={selection.allVisibleSelected}
                  ref={(el) => { if (el) el.indeterminate = selection.someVisibleSelected; }}
                  onChange={selection.toggleAllVisible}
                  aria-label={t('bulkActions.selectAll') as string}
                />
              </th>
              <th>{t('common.code')}</th>
              <th>{t('common.name')}</th>
              <th>{t('qcHold.location')}</th>
              <th>Batch</th>
              <th>MRN</th>
              <th>{t('common.quantity')}</th>
              <th>Status</th>
              <th>{t('common.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={9} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filtered.length === 0 && (
              <tr><td colSpan={9} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{hasAnyFilter ? t('inventory.filters.noResults') : t('common.noData')}</td></tr>
            )}
            {!loading && filtered.map((r) => (
              <tr
                key={r.id}
                style={{ background: selection.isSelected(r.id) ? 'var(--taris-blue-50, #e7f2fe)' : undefined }}
              >
                <td>
                  <input
                    type="checkbox"
                    checked={selection.isSelected(r.id)}
                    onChange={() => selection.toggle(r.id)}
                    aria-label={t('bulkActions.selectRow') as string}
                  />
                </td>
                <td><code>{r.item?.code ?? '-'}</code></td>
                <td>{r.item?.name ?? '-'}</td>
                <td>{r.location?.code ?? '-'}</td>
                <td>{r.batchNumber ?? '-'}</td>
                <td>{r.mrn ?? '-'}</td>
                <td><strong>{formatQuantity(r.quantity)} {r.uoM?.code}</strong></td>
                <td>
                  <span style={{ padding: '2px 8px', borderRadius: 3, background: r.qualityStatus === 2 ? '#fdecea' : '#fff4e0', color: r.qualityStatus === 2 ? '#c00' : '#c60' }}>
                    {r.qualityStatus === 2 ? t('qcHold.blocked') : t('qcHold.quarantine')}
                  </span>
                </td>
                <td>
                  <button onClick={() => releaseOne(r.id)} disabled={busyId === r.id} style={{ padding: '4px 10px' }}>
                    {busyId === r.id ? t('common.saving') : t('qcHold.release')}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {bulkModal && (
        <div
          onClick={() => !bulkRunning && setBulkModal(null)}
          style={{ position: 'fixed', inset: 0, background: 'rgba(15,23,42,0.4)', zIndex: 200, display: 'flex', alignItems: 'center', justifyContent: 'center' }}
        >
          <div
            onClick={(e) => e.stopPropagation()}
            style={{ background: 'white', borderRadius: 8, padding: 20, minWidth: 380, maxWidth: 480, boxShadow: '0 10px 30px rgba(15,23,42,0.2)' }}
          >
            <h3 style={{ marginTop: 0 }}>{t('qcHold.bulkReleaseTitle')}</h3>
            <p style={{ color: '#555' }}>{t('qcHold.bulkReleaseIntro', { count: selection.count, qty: selectedQty.toFixed(2) })}</p>
            <label style={{ display: 'block', marginTop: 12 }}>
              <div style={{ fontSize: 12, color: '#666', marginBottom: 4 }}>{t('inventory.bulkQc.reasonLabel')}</div>
              <textarea
                value={bulkModal.reason}
                onChange={(e) => setBulkModal({ reason: e.target.value })}
                rows={3}
                style={{ width: '100%', padding: 6 }}
                placeholder={t('inventory.bulkQc.reasonPlaceholder') as string}
                disabled={bulkRunning}
              />
            </label>
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 16 }}>
              <button type="button" onClick={() => setBulkModal(null)} disabled={bulkRunning} style={{ padding: '6px 14px' }}>{t('common.cancel')}</button>
              <button
                type="button"
                onClick={() => runBulkRelease(bulkModal.reason.trim())}
                disabled={bulkRunning || !bulkModal.reason.trim()}
                style={{ padding: '6px 14px', background: 'var(--taris-blue-500, #1e88e5)', color: 'white', border: 'none', borderRadius: 4 }}
              >
                {bulkRunning ? t('common.saving') : t('inventory.bulkQc.confirm')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default QcHold;
