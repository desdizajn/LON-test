import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { wmsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

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
  location?: { code: string; name: string } | null;
  uoM?: { code: string; name: string } | null;
};

const QcHold: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Balance[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<'all' | 'blocked' | 'quarantine'>('all');
  const [busyId, setBusyId] = useState<string | null>(null);

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

  const filtered = useMemo(() => {
    if (filter === 'blocked') return rows.filter((r) => r.qualityStatus === 2);
    if (filter === 'quarantine') return rows.filter((r) => r.qualityStatus === 3);
    return rows;
  }, [rows, filter]);

  const counts = useMemo(() => ({
    blocked: rows.filter((r) => r.qualityStatus === 2).length,
    quarantine: rows.filter((r) => r.qualityStatus === 3).length,
  }), [rows]);

  async function release(id: string) {
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

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('qcHold.title')}</h1>
      <p style={{ color: '#666' }}>{t('qcHold.subtitle')}</p>

      <div style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'center' }}>
        <button onClick={() => setFilter('all')} style={{ padding: '6px 12px', fontWeight: filter === 'all' ? 'bold' : 'normal' }}>
          {t('qcHold.all')} ({rows.length})
        </button>
        <button onClick={() => setFilter('blocked')} style={{ padding: '6px 12px', fontWeight: filter === 'blocked' ? 'bold' : 'normal' }}>
          🚫 {t('qcHold.blocked')} ({counts.blocked})
        </button>
        <button onClick={() => setFilter('quarantine')} style={{ padding: '6px 12px', fontWeight: filter === 'quarantine' ? 'bold' : 'normal' }}>
          ⏳ {t('qcHold.quarantine')} ({counts.quarantine})
        </button>
        <button
          onClick={() =>
            exportToCsv(
              filtered,
              [
                { key: 'item', label: t('common.code') as string, get: (r) => r.item?.code ?? '' },
                { key: 'item', label: t('common.name') as string, get: (r) => r.item?.name ?? '' },
                { key: 'location', label: t('qcHold.location') as string, get: (r) => r.location?.code ?? '' },
                { key: 'batchNumber', label: 'Batch' },
                { key: 'mrn', label: 'MRN' },
                { key: 'quantity', label: t('common.quantity') as string, type: 'number' },
                { key: 'qualityStatus', label: 'Status', get: (r) => r.qualityStatus === 2 ? t('qcHold.blocked') : t('qcHold.quarantine') },
                { key: 'expiryDate', label: t('common.date') as string, type: 'date' },
              ],
              'qc-hold'
            )
          }
          disabled={filtered.length === 0}
          style={{ padding: '6px 12px', marginLeft: 'auto' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      {error && (
        <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>
          {error}
        </div>
      )}

      <div className="table-container">
        <table>
          <thead>
            <tr>
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
            {loading && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filtered.length === 0 && (
              <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>
            )}
            {!loading && filtered.map((r) => (
              <tr key={r.id}>
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
                  <button onClick={() => release(r.id)} disabled={busyId === r.id} style={{ padding: '4px 10px' }}>
                    {busyId === r.id ? t('common.saving') : t('qcHold.release')}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default QcHold;
