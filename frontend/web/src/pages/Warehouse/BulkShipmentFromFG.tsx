import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi, masterDataApi, wmsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import ArticlePicker from '../../components/common/ArticlePicker';
import SearchableSelect, { SearchableOption } from '../../components/common/SearchableSelect';

/**
 * P5.2.4 — Mass shipment from filtered FG stock.
 *
 * A filter predicate (item / batch / MRN / PO / warehouse / location) drives
 * the FG pool; one click creates a Shipment + per-balance ShipmentLines and
 * drains the matched inventory. „Bulk" = one filter → N inventory rows → one
 * shipment with N lines; the preview panel makes that count visible before
 * commit. Optional checkbox chains a CreateExportDeclaration call so the
 * customs side lands atomically.
 */

type Warehouse = { id: string; code: string; name: string };
type Location = { id: string; code: string; name: string; warehouseId: string };
type Partner = { id: string; code: string; name: string };
type Procedure = { id: string; code: string; name: string; type?: number };

type InventoryRow = {
  id: string;
  itemId: string;
  item?: { code?: string; name?: string } | null;
  location?: { id?: string; code?: string; name?: string; warehouseId?: string } | null;
  locationId: string;
  batchNumber?: string | null;
  mrn?: string | null;
  quantity: number;
  uoM?: { code?: string } | null;
  qualityStatus?: number;
  lonProcessState?: number | null;
};

const BulkShipmentFromFG: React.FC = () => {
  const { t } = useTranslation();
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [locations, setLocations] = useState<Location[]>([]);
  const [partners, setPartners] = useState<Partner[]>([]);
  const [procedures, setProcedures] = useState<Procedure[]>([]);
  const [inventory, setInventory] = useState<InventoryRow[]>([]);
  const [loadingInventory, setLoadingInventory] = useState<boolean>(false);

  const [itemId, setItemId] = useState<string>('');
  const [batchNumber, setBatchNumber] = useState<string>('');
  const [mrn, setMrn] = useState<string>('');
  const [sourceWarehouseId, setSourceWarehouseId] = useState<string>('');
  const [locationId, setLocationId] = useState<string>('');
  const [partnerId, setPartnerId] = useState<string>('');
  const [createExportDecl, setCreateExportDecl] = useState<boolean>(false);
  const [procedureId, setProcedureId] = useState<string>('');
  const [declarationNumber, setDeclarationNumber] = useState<string>('');
  const [reference, setReference] = useState<string>('');
  const [submitting, setSubmitting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<any>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [wh, loc, prt, pr] = await Promise.all([
          masterDataApi.getWarehouses(),
          masterDataApi.getLocations(),
          masterDataApi.getPartners(),
          customsApi.getProcedures(),
        ]);
        if (cancelled) return;
        setWarehouses((wh.data as Warehouse[]) ?? []);
        setLocations((loc.data as Location[]) ?? []);
        setPartners((prt.data as Partner[]) ?? []);
        setProcedures(((pr.data as Procedure[]) ?? []).filter((p) => p.code === '3151' || p.type === 2));
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  // Load inventory once so client-side preview + MRN/batch dropdowns have
  // real data to work with. Refresh is manual — if the user lands here after
  // a receipt on another tab, they hit „Refresh" below.
  const loadInventory = async () => {
    setLoadingInventory(true);
    try {
      const resp = await wmsApi.getInventory();
      setInventory((resp.data as InventoryRow[]) ?? []);
    } catch (err) {
      setError(translateError(err));
    } finally {
      setLoadingInventory(false);
    }
  };

  useEffect(() => {
    loadInventory();
  }, []);

  // Candidate MRNs / batches / references derived from inventory. Keeps the
  // dropdowns honest — only values that actually match real stock appear.
  const mrnOptions = useMemo<SearchableOption[]>(() => {
    const set = new Map<string, number>();
    inventory.forEach((r) => {
      if (r.quantity > 0 && r.mrn) {
        set.set(r.mrn, (set.get(r.mrn) ?? 0) + r.quantity);
      }
    });
    return Array.from(set.entries())
      .sort((a, b) => a[0].localeCompare(b[0]))
      .map(([m, qty]) => ({
        value: m,
        label: m,
        hint: t('bulkShipment.mrnHint', { count: Number(qty.toFixed(2)) }) as string,
      }));
  }, [inventory, t]);

  const batchOptions = useMemo<SearchableOption[]>(() => {
    const set = new Map<string, { qty: number; items: Set<string> }>();
    inventory.forEach((r) => {
      if (r.quantity > 0 && r.batchNumber) {
        const e = set.get(r.batchNumber) ?? { qty: 0, items: new Set<string>() };
        e.qty += r.quantity;
        if (r.item?.code) e.items.add(r.item.code);
        set.set(r.batchNumber, e);
      }
    });
    return Array.from(set.entries())
      .sort((a, b) => a[0].localeCompare(b[0]))
      .map(([b, info]) => ({
        value: b,
        label: b,
        hint: `${Array.from(info.items).slice(0, 3).join(', ')} · ${info.qty.toFixed(2)}`,
      }));
  }, [inventory]);

  const partnerOptions = useMemo<SearchableOption[]>(
    () =>
      partners.map((p) => ({
        value: p.id,
        label: p.name,
        hint: p.code,
      })),
    [partners]
  );

  const warehouseOptions = useMemo<SearchableOption[]>(
    () => warehouses.map((w) => ({ value: w.id, label: w.name, hint: w.code })),
    [warehouses]
  );

  const locationOptions = useMemo<SearchableOption[]>(
    () =>
      locations
        .filter((l) => !sourceWarehouseId || l.warehouseId === sourceWarehouseId)
        .map((l) => ({ value: l.id, label: l.name, hint: l.code })),
    [locations, sourceWarehouseId]
  );

  const procedureOptions = useMemo<SearchableOption[]>(
    () => procedures.map((p) => ({ value: p.id, label: p.name, hint: p.code })),
    [procedures]
  );

  const hasFilter =
    !!itemId || !!batchNumber.trim() || !!mrn.trim() || !!sourceWarehouseId || !!locationId;

  // Client-side preview: same predicate as the backend command (minus PO +
  // partnerId which are carriers, not filters). Shows the user exactly how
  // many balances the one click will drain.
  const preview = useMemo(() => {
    if (!hasFilter) return { rows: [] as InventoryRow[], totalQty: 0, mrns: new Set<string>() };
    const rows = inventory.filter((r) => {
      if (r.quantity <= 0) return false;
      if (itemId && r.itemId !== itemId) return false;
      if (batchNumber.trim() && r.batchNumber !== batchNumber.trim()) return false;
      if (mrn.trim() && (r.mrn ?? '').toUpperCase() !== mrn.trim().toUpperCase()) return false;
      if (locationId && r.locationId !== locationId) return false;
      if (sourceWarehouseId && r.location?.warehouseId !== sourceWarehouseId) return false;
      return true;
    });
    const mrns = new Set(rows.map((r) => r.mrn ?? '').filter(Boolean));
    const totalQty = rows.reduce((s, r) => s + r.quantity, 0);
    return { rows, totalQty, mrns };
  }, [inventory, itemId, batchNumber, mrn, locationId, sourceWarehouseId, hasFilter]);

  const exportBlocked = createExportDecl && preview.mrns.size !== 1;

  async function submit() {
    setError(null);
    setResult(null);
    if (!hasFilter) {
      setError(t('errors.transfer.no_filter') as string);
      return;
    }
    if (preview.rows.length === 0) {
      setError(t('bulkShipment.noMatches') as string);
      return;
    }
    if (createExportDecl && !procedureId) {
      setError(t('bulkShipment.procedureRequired') as string);
      return;
    }
    if (exportBlocked) {
      setError(t('bulkShipment.exportMultiMrn', { count: preview.mrns.size }) as string);
      return;
    }
    setSubmitting(true);
    try {
      const resp = await wmsApi.bulkShipmentFromFG({
        itemId: itemId || null,
        batchNumber: batchNumber || null,
        mrn: mrn || null,
        locationId: locationId || null,
        sourceWarehouseId: sourceWarehouseId || null,
        partnerId: partnerId || null,
        customsProcedureId: createExportDecl ? procedureId : null,
        declarationNumber: declarationNumber || null,
        reference: reference || null,
        createExportDeclaration: createExportDecl,
      });
      const env = resp.data as { isSuccess: boolean; data?: any; errorMessage?: string; errorCode?: string };
      if (!env.isSuccess) {
        setError(translateError(env));
        return;
      }
      setResult(env.data);
      // Refresh so next attempt reflects drained stock.
      loadInventory();
    } catch (err) {
      setError(translateError(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div style={{ maxWidth: 1040, margin: '0 auto', padding: 16 }}>
      <h1>{t('bulkShipment.title')}</h1>
      <p style={{ color: '#666', marginBottom: 24 }}>{t('bulkShipment.subtitle')}</p>

      <fieldset style={{ border: '1px solid #ddd', borderRadius: 4, padding: 12 }}>
        <legend>{t('bulkShipment.filterLegend')}</legend>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          <label>
            {t('bulkShipment.item')}
            <ArticlePicker value={null} onChange={(v) => setItemId(v?.id ?? '')} />
          </label>
          <label>
            {t('bulkShipment.batch')}
            <SearchableSelect
              value={batchNumber}
              onChange={(v) => setBatchNumber(v)}
              options={batchOptions}
              placeholder={t('bulkShipment.batchPlaceholder') as string}
              loading={loadingInventory}
              emptyMessage={t('bulkShipment.noBatches') as string}
            />
          </label>
          <label>
            {t('bulkShipment.mrn')}
            <SearchableSelect
              value={mrn}
              onChange={(v) => setMrn(v)}
              options={mrnOptions}
              placeholder={t('bulkShipment.mrnPlaceholder') as string}
              loading={loadingInventory}
              emptyMessage={t('bulkShipment.noMrns') as string}
            />
          </label>
          <label>
            {t('bulkShipment.sourceWarehouse')}
            <SearchableSelect
              value={sourceWarehouseId}
              onChange={(v) => {
                setSourceWarehouseId(v);
                setLocationId('');
              }}
              options={warehouseOptions}
              placeholder={t('bulkShipment.warehousePlaceholder') as string}
            />
          </label>
          <label>
            {t('bulkShipment.sourceLocation')}
            <SearchableSelect
              value={locationId}
              onChange={(v) => setLocationId(v)}
              options={locationOptions}
              disabled={!sourceWarehouseId}
              placeholder={t('bulkShipment.locationPlaceholder') as string}
            />
          </label>
        </div>
        <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 8 }}>
          <button
            type="button"
            onClick={loadInventory}
            disabled={loadingInventory}
            style={{ padding: '4px 10px', fontSize: 12 }}
          >
            {loadingInventory ? t('common.loading') : t('bulkShipment.refreshStock')}
          </button>
        </div>
      </fieldset>

      {/* Preview panel — answers „why is this bulk if I only pick one item?" */}
      <div
        style={{
          marginTop: 12,
          border: '1px solid var(--taris-blue-200, #bedcfa)',
          background: hasFilter
            ? preview.rows.length > 0
              ? 'var(--taris-blue-50, #e7f2fe)'
              : 'var(--warning-bg, #fff7e6)'
            : 'var(--ink-50, #f8fafc)',
          borderRadius: 6,
          padding: 12,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
          <strong>{t('bulkShipment.previewTitle')}</strong>
          {!hasFilter && <span style={{ color: '#666' }}>{t('bulkShipment.previewEmpty')}</span>}
          {hasFilter && preview.rows.length === 0 && (
            <span style={{ color: '#a85200' }}>{t('bulkShipment.noMatches')}</span>
          )}
          {hasFilter && preview.rows.length > 0 && (
            <>
              <span>
                {t('bulkShipment.previewCount', { count: preview.rows.length })}
              </span>
              <span>
                · {t('bulkShipment.previewTotal', { qty: formatQuantity(preview.totalQty, 2) })}
              </span>
              <span>
                · {t('bulkShipment.previewMrns', { count: preview.mrns.size })}
              </span>
            </>
          )}
        </div>
        {preview.rows.length > 0 && (
          <div style={{ marginTop: 10, maxHeight: 180, overflowY: 'auto' }}>
            <table style={{ width: '100%', fontSize: 12 }}>
              <thead>
                <tr>
                  <th style={{ textAlign: 'left' }}>{t('bulkShipment.preview.item')}</th>
                  <th style={{ textAlign: 'left' }}>{t('bulkShipment.preview.location')}</th>
                  <th style={{ textAlign: 'left' }}>{t('bulkShipment.preview.batch')}</th>
                  <th style={{ textAlign: 'left' }}>MRN</th>
                  <th style={{ textAlign: 'right' }}>{t('bulkShipment.preview.qty')}</th>
                </tr>
              </thead>
              <tbody>
                {preview.rows.slice(0, 50).map((r) => (
                  <tr key={r.id}>
                    <td>{r.item?.code ?? '-'} · {r.item?.name ?? ''}</td>
                    <td>{r.location?.code ?? '-'}</td>
                    <td>{r.batchNumber ?? '-'}</td>
                    <td>{r.mrn ?? '-'}</td>
                    <td style={{ textAlign: 'right' }}>
                      {r.quantity.toFixed(2)} {r.uoM?.code ?? ''}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {preview.rows.length > 50 && (
              <div style={{ color: '#888', fontSize: 11, marginTop: 4 }}>
                {t('bulkShipment.previewTruncated', { count: preview.rows.length - 50 })}
              </div>
            )}
          </div>
        )}
      </div>

      <fieldset style={{ border: '1px solid #ddd', borderRadius: 4, padding: 12, marginTop: 12 }}>
        <legend>{t('bulkShipment.customerLegend')}</legend>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          <label>
            {t('bulkShipment.partner')}
            <SearchableSelect
              value={partnerId}
              onChange={(v) => setPartnerId(v)}
              options={partnerOptions}
              placeholder={t('bulkShipment.partnerPlaceholder') as string}
            />
          </label>
          <label>
            {t('bulkShipment.reference')}
            <input
              type="text"
              value={reference}
              onChange={(e) => setReference(e.target.value)}
              placeholder={t('bulkShipment.referencePlaceholder') as string}
              style={{ width: '100%', padding: 6 }}
            />
          </label>
        </div>
      </fieldset>

      <fieldset style={{ border: '1px solid #ddd', borderRadius: 4, padding: 12, marginTop: 12 }}>
        <legend>{t('bulkShipment.customsLegend')}</legend>
        <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <input type="checkbox" checked={createExportDecl} onChange={(e) => setCreateExportDecl(e.target.checked)} />
          {t('bulkShipment.createEx')}
        </label>
        {createExportDecl && (
          <>
            {exportBlocked && (
              <div style={{ marginTop: 8, padding: 8, background: '#fff1f0', border: '1px solid #ffa39e', borderRadius: 4, color: '#a8071a', fontSize: 13 }}>
                {t('bulkShipment.exportMultiMrn', { count: preview.mrns.size })}
              </div>
            )}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginTop: 8 }}>
              <label>
                {t('bulkShipment.procedure')}
                <SearchableSelect
                  value={procedureId}
                  onChange={(v) => setProcedureId(v)}
                  options={procedureOptions}
                  placeholder={t('bulkShipment.procedurePlaceholder') as string}
                />
              </label>
              <label>
                {t('bulkShipment.declarationNumber')}
                <input type="text" value={declarationNumber} onChange={(e) => setDeclarationNumber(e.target.value)} style={{ width: '100%', padding: 6 }} />
              </label>
            </div>
          </>
        )}
      </fieldset>

      <div style={{ marginTop: 16, display: 'flex', gap: 10, alignItems: 'center' }}>
        <button
          onClick={submit}
          disabled={submitting || !hasFilter || preview.rows.length === 0 || exportBlocked}
          style={{ padding: '8px 16px' }}
        >
          {submitting
            ? t('common.saving')
            : preview.rows.length > 0
              ? (t('bulkShipment.commitWithCount', { count: preview.rows.length }) as string)
              : t('bulkShipment.commit')}
        </button>
        {preview.rows.length > 0 && !submitting && (
          <span style={{ color: '#555', fontSize: 13 }}>
            {t('bulkShipment.aboutToShip', {
              count: preview.rows.length,
              qty: formatQuantity(preview.totalQty, 2),
            })}
          </span>
        )}
      </div>

      {error && (
        <div style={{ marginTop: 16, padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4 }}>
          {error}
        </div>
      )}
      {result && (
        <div style={{ marginTop: 16, padding: 12, background: '#edf7ed', color: '#265c26', borderRadius: 4 }}>
          <div>{t('bulkShipment.successHeading')}</div>
          <div>{t('bulkShipment.shipmentNumber')}: <code>{result.shipmentNumber}</code></div>
          <div>{t('bulkShipment.linesCreated')}: <strong>{result.linesCreated}</strong></div>
          <div>{t('bulkShipment.totalQuantity')}: <strong>{formatQuantity(result.totalQuantity, 2)}</strong></div>
          {result.exportDeclarationId && (
            <div>{t('bulkShipment.exportDeclarationId')}: <code>{result.exportDeclarationId}</code></div>
          )}
        </div>
      )}
    </div>
  );
};

export default BulkShipmentFromFG;
