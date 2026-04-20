import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { masterDataApi, wmsApi } from '../../services/api';
import { useFieldHistory } from '../../hooks/useFieldHistory';

/**
 * P5.2.7 — Mass location change page.
 *
 * Two-step flow: filter + preview (non-destructive) → confirm + commit.
 * Filter criteria OR together into the backend predicate; at least one is
 * required (guarded client-side AND server-side so we never accidentally
 * transfer every balance in the tenant).
 */

type Warehouse = { id: string; name: string };
type Location = { id: string; code: string; name: string; type?: number; warehouseId: string; isActive: boolean };
type Item = { id: string; code: string; name: string };

type PreviewRow = {
  balanceId: string;
  itemId: string;
  itemCode?: string | null;
  itemName?: string | null;
  locationId: string;
  locationCode?: string | null;
  batchNumber?: string | null;
  mrn?: string | null;
  quantity: number;
  qualityStatus: number;
  lonProcessState?: number | null;
};

type PreviewResult = { balancesMatched: number; totalQuantity: number; rows: PreviewRow[] };
type CommitResult = {
  balancesMoved: number;
  totalQuantityMoved: number;
  targetLocationId: string;
  movements: Array<{ movementNumber: string; quantity: number }>;
};

const MassTransfer: React.FC = () => {
  const { t } = useTranslation();
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [locations, setLocations] = useState<Location[]>([]);
  const [items, setItems] = useState<Item[]>([]);

  // Filter state
  const [itemId, setItemId] = useState<string>('');
  const [batchNumber, setBatchNumber] = useState<string>('');
  const [mrn, setMrn] = useState<string>('');
  const [sourceWarehouseId, setSourceWarehouseId] = useState<string>('');
  const [sourceLocationId, setSourceLocationId] = useState<string>('');
  const [qualityStatus, setQualityStatus] = useState<string>('');
  const [lonProcessState, setLonProcessState] = useState<string>('');

  // Target + reason
  const [targetLocationId, setTargetLocationId] = useState<string>('');
  const [reason, setReason] = useState<string>('');
  const { recent: recentReasons, record: recordReason } = useFieldHistory('massTransfer.reason');

  // UX state
  const [preview, setPreview] = useState<PreviewResult | null>(null);
  const [loadingPreview, setLoadingPreview] = useState(false);
  const [committing, setCommitting] = useState(false);
  const [committed, setCommitted] = useState<CommitResult | null>(null);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    masterDataApi.getWarehouses().then((r) => {
      const list: Warehouse[] = r.data?.data ?? r.data ?? [];
      setWarehouses(Array.isArray(list) ? list : []);
    });
    masterDataApi.getLocations().then((r) => {
      const list: Location[] = r.data?.data ?? r.data ?? [];
      setLocations(Array.isArray(list) ? list : []);
    });
    masterDataApi.getItems().then((r) => {
      const list: Item[] = r.data?.data ?? r.data ?? [];
      setItems(Array.isArray(list) ? list.slice(0, 500) : []); // basic cap
    });
  }, []);

  const visibleLocations = useMemo(() => {
    if (!sourceWarehouseId) return locations;
    return locations.filter((l) => l.warehouseId === sourceWarehouseId);
  }, [locations, sourceWarehouseId]);

  const hasFilter =
    !!itemId || !!batchNumber.trim() || !!mrn.trim() ||
    !!sourceWarehouseId || !!sourceLocationId ||
    !!qualityStatus || !!lonProcessState;

  const canPreview = hasFilter;
  const canCommit = !!targetLocationId && preview && preview.balancesMatched > 0;

  const buildPayload = () => ({
    itemId: itemId || null,
    batchNumber: batchNumber.trim() || null,
    mrn: mrn.trim() || null,
    sourceWarehouseId: sourceWarehouseId || null,
    sourceLocationId: sourceLocationId || null,
    qualityStatus: qualityStatus ? Number(qualityStatus) : null,
    lonProcessState: lonProcessState ? Number(lonProcessState) : null,
    targetLocationId: targetLocationId || null,
  });

  const runPreview = async () => {
    setErr(null);
    setCommitted(null);
    setLoadingPreview(true);
    try {
      const r = await wmsApi.massTransferPreview(buildPayload());
      const data = r.data?.data ?? r.data;
      setPreview(data as PreviewResult);
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || e?.message || 'Preview failed');
      setPreview(null);
    } finally {
      setLoadingPreview(false);
    }
  };

  const runCommit = async () => {
    if (!targetLocationId) return;
    const summary = preview ? `${preview.balancesMatched} × ${preview.totalQuantity}` : '?';
    if (!window.confirm(t('massTransfer.confirmPrompt', { summary }) as string)) return;

    setErr(null);
    setCommitting(true);
    try {
      const r = await wmsApi.massTransfer({
        ...buildPayload(),
        targetLocationId,
        reason: reason || null,
      } as any);
      const data = r.data?.data ?? r.data;
      setCommitted(data as CommitResult);
      setPreview(null);
      if (reason.trim()) recordReason(reason); // P5.3.5 — recent values
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || e?.message || 'Commit failed');
    } finally {
      setCommitting(false);
    }
  };

  return (
    <div style={{ padding: 20, maxWidth: 1100, margin: '0 auto' }}>
      <h2>🔀 {t('massTransfer.title', 'Масовен премин на локации')}</h2>
      <p style={{ color: '#666', marginTop: -6 }}>
        {t('massTransfer.hint', 'Филтрирај инвентар по било кој критериум и префрли ги сите балансирани редови во една таргет локација во еден атомичен повик.')}
      </p>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, background: '#f7f7f7', padding: 14, borderRadius: 8 }}>
        <label>
          {t('massTransfer.item', 'Артикал')}
          <select value={itemId} onChange={(e) => setItemId(e.target.value)} style={{ width: '100%', padding: 6 }}>
            <option value="">— {t('common.any', 'било кој')} —</option>
            {items.map((i) => (
              <option key={i.id} value={i.id}>
                {i.code} — {i.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          {t('massTransfer.batch', 'Batch')}
          <input type="text" value={batchNumber} onChange={(e) => setBatchNumber(e.target.value)} style={{ width: '100%', padding: 6 }} />
        </label>
        <label>
          MRN
          <input type="text" value={mrn} onChange={(e) => setMrn(e.target.value)} style={{ width: '100%', padding: 6 }} />
        </label>
        <label>
          {t('massTransfer.sourceWarehouse', 'Од магацин')}
          <select
            value={sourceWarehouseId}
            onChange={(e) => {
              setSourceWarehouseId(e.target.value);
              setSourceLocationId(''); // reset dependent
            }}
            style={{ width: '100%', padding: 6 }}
          >
            <option value="">— {t('common.any', 'било кој')} —</option>
            {warehouses.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          {t('massTransfer.sourceLocation', 'Од локација')}
          <select value={sourceLocationId} onChange={(e) => setSourceLocationId(e.target.value)} style={{ width: '100%', padding: 6 }}>
            <option value="">— {t('common.any', 'било која')} —</option>
            {visibleLocations.filter((l) => l.isActive).map((l) => (
              <option key={l.id} value={l.id}>
                {l.code} — {l.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          {t('massTransfer.quality', 'Квалитет')}
          <select value={qualityStatus} onChange={(e) => setQualityStatus(e.target.value)} style={{ width: '100%', padding: 6 }}>
            <option value="">— {t('common.any', 'било кој')} —</option>
            <option value="1">OK</option>
            <option value="2">Blocked</option>
            <option value="3">Quarantine</option>
          </select>
        </label>
        <label>
          {t('massTransfer.lonState', 'LON состојба')}
          <select value={lonProcessState} onChange={(e) => setLonProcessState(e.target.value)} style={{ width: '100%', padding: 6 }}>
            <option value="">— {t('common.any', 'било која')} —</option>
            <option value="1">Imported</option>
            <option value="2">InProduction</option>
            <option value="3">Exported</option>
            <option value="4">Waste</option>
          </select>
        </label>
      </div>

      <div style={{ marginTop: 14, display: 'flex', gap: 12, alignItems: 'flex-end' }}>
        <label style={{ flex: 2 }}>
          {t('massTransfer.target', 'Таргет локација')}*
          <select value={targetLocationId} onChange={(e) => setTargetLocationId(e.target.value)} style={{ width: '100%', padding: 6 }}>
            <option value="">— {t('common.select', 'избери')} —</option>
            {locations.filter((l) => l.isActive).map((l) => (
              <option key={l.id} value={l.id}>
                {l.code} — {l.name}
              </option>
            ))}
          </select>
        </label>
        <label style={{ flex: 2 }}>
          {t('massTransfer.reason', 'Причина')} ({t('common.optional', 'optional')})
          <input
            type="text"
            list="fh-massTransfer-reason"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            style={{ width: '100%', padding: 6 }}
          />
          <datalist id="fh-massTransfer-reason">
            {recentReasons.map((r) => (
              <option key={r.value} value={r.value} />
            ))}
          </datalist>
        </label>
        <button
          onClick={runPreview}
          disabled={!canPreview || loadingPreview}
          style={{ padding: '8px 16px' }}
        >
          {loadingPreview ? '…' : t('massTransfer.preview', 'Preview')}
        </button>
        <button
          onClick={runCommit}
          disabled={!canCommit || committing}
          style={{ padding: '8px 16px', background: '#2b6cb0', color: '#fff' }}
        >
          {committing ? '…' : t('massTransfer.commit', 'Commit')}
        </button>
      </div>

      {err && (
        <div style={{ background: '#fde', color: '#b00020', padding: 10, borderRadius: 4, marginTop: 14 }}>
          {err}
        </div>
      )}

      {preview && (
        <div style={{ marginTop: 20 }}>
          <h3>
            {t('massTransfer.previewHeader', 'Преглед')}: {preview.balancesMatched} × {preview.totalQuantity}
          </h3>
          {preview.balancesMatched === 0 ? (
            <p style={{ color: '#666' }}>{t('massTransfer.noMatches', 'Нема редови кои одговараат на филтерот.')}</p>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
              <thead>
                <tr style={{ background: '#eee' }}>
                  <th style={th}>{t('massTransfer.item', 'Артикал')}</th>
                  <th style={th}>{t('massTransfer.sourceLocation', 'Локација')}</th>
                  <th style={th}>Batch</th>
                  <th style={th}>MRN</th>
                  <th style={{ ...th, textAlign: 'right' }}>{t('massTransfer.qty', 'Кол.')}</th>
                </tr>
              </thead>
              <tbody>
                {preview.rows.map((row) => (
                  <tr key={row.balanceId}>
                    <td style={td}>
                      {row.itemCode ?? ''} {row.itemName ? `— ${row.itemName}` : ''}
                    </td>
                    <td style={td}>{row.locationCode ?? row.locationId}</td>
                    <td style={td}>{row.batchNumber ?? ''}</td>
                    <td style={td}>{row.mrn ?? ''}</td>
                    <td style={{ ...td, textAlign: 'right' }}>{row.quantity}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {committed && (
        <div style={{ marginTop: 20, background: '#e8f5e9', padding: 14, borderRadius: 6 }}>
          <strong>✅ {t('massTransfer.doneHeader', 'Извршено')}.</strong>{' '}
          {committed.balancesMoved} {t('massTransfer.rowsMoved', 'редови преминати')}, {t('massTransfer.totalQty', 'вкупно')}: {committed.totalQuantityMoved}.{' '}
          {t('massTransfer.movementsCount', 'Движења')}: {committed.movements.length}.
        </div>
      )}
    </div>
  );
};

const th: React.CSSProperties = { padding: '6px 8px', textAlign: 'left', borderBottom: '1px solid #ccc' };
const td: React.CSSProperties = { padding: '6px 8px', borderBottom: '1px solid #eee' };

export default MassTransfer;
