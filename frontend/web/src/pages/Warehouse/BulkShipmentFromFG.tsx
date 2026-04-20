import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi, masterDataApi, wmsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import ArticlePicker from '../../components/common/ArticlePicker';

/**
 * P5.2.4 — Bulk shipment from FG selection.
 *
 * A filter predicate (item/batch/MRN/PO/warehouse/location) drives the FG
 * pool; one click creates a Shipment + per-balance ShipmentLines and drains
 * the inventory. Optional checkbox chains a CreateExportDeclaration call so
 * the customs side lands in the same action.
 */

type Warehouse = { id: string; code: string; name: string };
type Location = { id: string; code: string; name: string; warehouseId: string };
type Partner = { id: string; code: string; name: string };
type Procedure = { id: string; code: string; name: string; type?: number };

const BulkShipmentFromFG: React.FC = () => {
  const { t } = useTranslation();
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [locations, setLocations] = useState<Location[]>([]);
  const [partners, setPartners] = useState<Partner[]>([]);
  const [procedures, setProcedures] = useState<Procedure[]>([]);

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

  const hasFilter =
    !!itemId || !!batchNumber.trim() || !!mrn.trim() || !!sourceWarehouseId || !!locationId;

  async function submit() {
    setError(null);
    setResult(null);
    if (!hasFilter) {
      setError(t('errors.transfer.no_filter') as string);
      return;
    }
    if (createExportDecl && !procedureId) {
      setError(t('bulkShipment.procedureRequired') as string);
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
    } catch (err) {
      setError(translateError(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div style={{ maxWidth: 960, margin: '0 auto', padding: 16 }}>
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
            <input type="text" value={batchNumber} onChange={(e) => setBatchNumber(e.target.value)} style={{ width: '100%', padding: 6 }} />
          </label>
          <label>
            {t('bulkShipment.mrn')}
            <input type="text" value={mrn} onChange={(e) => setMrn(e.target.value)} style={{ width: '100%', padding: 6 }} />
          </label>
          <label>
            {t('bulkShipment.sourceWarehouse')}
            <select value={sourceWarehouseId} onChange={(e) => { setSourceWarehouseId(e.target.value); setLocationId(''); }} style={{ width: '100%', padding: 6 }}>
              <option value="">—</option>
              {warehouses.map((w) => (
                <option key={w.id} value={w.id}>{w.code} · {w.name}</option>
              ))}
            </select>
          </label>
          <label>
            {t('bulkShipment.sourceLocation')}
            <select value={locationId} onChange={(e) => setLocationId(e.target.value)} disabled={!sourceWarehouseId} style={{ width: '100%', padding: 6 }}>
              <option value="">—</option>
              {locations.filter((l) => !sourceWarehouseId || l.warehouseId === sourceWarehouseId).map((l) => (
                <option key={l.id} value={l.id}>{l.code} · {l.name}</option>
              ))}
            </select>
          </label>
        </div>
      </fieldset>

      <fieldset style={{ border: '1px solid #ddd', borderRadius: 4, padding: 12, marginTop: 12 }}>
        <legend>{t('bulkShipment.customerLegend')}</legend>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          <label>
            {t('bulkShipment.partner')}
            <select value={partnerId} onChange={(e) => setPartnerId(e.target.value)} style={{ width: '100%', padding: 6 }}>
              <option value="">—</option>
              {partners.map((p) => (
                <option key={p.id} value={p.id}>{p.code} · {p.name}</option>
              ))}
            </select>
          </label>
          <label>
            {t('bulkShipment.reference')}
            <input type="text" value={reference} onChange={(e) => setReference(e.target.value)} style={{ width: '100%', padding: 6 }} />
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
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginTop: 8 }}>
            <label>
              {t('bulkShipment.procedure')}
              <select value={procedureId} onChange={(e) => setProcedureId(e.target.value)} style={{ width: '100%', padding: 6 }}>
                <option value="">—</option>
                {procedures.map((p) => (
                  <option key={p.id} value={p.id}>{p.code} · {p.name}</option>
                ))}
              </select>
            </label>
            <label>
              {t('bulkShipment.declarationNumber')}
              <input type="text" value={declarationNumber} onChange={(e) => setDeclarationNumber(e.target.value)} style={{ width: '100%', padding: 6 }} />
            </label>
          </div>
        )}
      </fieldset>

      <div style={{ marginTop: 16 }}>
        <button onClick={submit} disabled={submitting || !hasFilter} style={{ padding: '8px 16px' }}>
          {submitting ? t('common.saving') : t('bulkShipment.commit')}
        </button>
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
