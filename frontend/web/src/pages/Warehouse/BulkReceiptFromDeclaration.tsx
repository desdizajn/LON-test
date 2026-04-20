import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi, masterDataApi, wmsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity, formatDate } from '../../utils/format';

/**
 * P5.2.3 — Bulk receipt from customs declaration.
 *
 * Pick a Registered/Submitted IM declaration, pick a landing warehouse + optional
 * location, hit Commit. The backend explodes every declaration line into a
 * receipt line and the existing CreateReceiptCommand pipeline handles MRN
 * registry increments, inflate-for-waste and LON state.
 */

type Declaration = {
  id: string;
  declarationNumber: string;
  mrn: string;
  declarationDate: string;
  status?: number;
  totalCustomsValue?: number | null;
  linesCount?: number | null;
  partnerName?: string | null;
};

type Warehouse = { id: string; code: string; name: string };
type Location = { id: string; code: string; name: string; warehouseId: string };

const BulkReceiptFromDeclaration: React.FC = () => {
  const { t } = useTranslation();
  const [declarations, setDeclarations] = useState<Declaration[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [locations, setLocations] = useState<Location[]>([]);

  const [declarationId, setDeclarationId] = useState<string>('');
  const [warehouseId, setWarehouseId] = useState<string>('');
  const [targetLocationId, setTargetLocationId] = useState<string>('');
  const [referenceNumber, setReferenceNumber] = useState<string>('');
  const [submitting, setSubmitting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<{ receiptId: string; linesCreated: number; totalQuantity: number } | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [declResp, whResp, locResp] = await Promise.all([
          customsApi.getDeclarations(),
          masterDataApi.getWarehouses(),
          masterDataApi.getLocations(),
        ]);
        if (cancelled) return;
        setDeclarations((declResp.data as Declaration[]) ?? []);
        setWarehouses((whResp.data as Warehouse[]) ?? []);
        setLocations((locResp.data as Location[]) ?? []);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const filteredLocations = useMemo(
    () => (warehouseId ? locations.filter((l) => l.warehouseId === warehouseId) : []),
    [locations, warehouseId]
  );

  const selectedDecl = useMemo(
    () => declarations.find((d) => d.id === declarationId) ?? null,
    [declarations, declarationId]
  );

  async function submit() {
    setError(null);
    setResult(null);
    if (!declarationId || !warehouseId) {
      setError(t('bulkReceipt.requiredMissing') as string);
      return;
    }
    setSubmitting(true);
    try {
      const resp = await wmsApi.bulkReceiptFromDeclaration({
        customsDeclarationId: declarationId,
        warehouseId,
        targetLocationId: targetLocationId || null,
        referenceNumber: referenceNumber || null,
      });
      const envelope = resp.data as { isSuccess: boolean; data?: { receiptId: string; linesCreated: number; totalQuantity: number }; errorMessage?: string; errorCode?: string };
      if (!envelope.isSuccess) {
        setError(translateError(envelope));
        return;
      }
      setResult(envelope.data!);
    } catch (err) {
      setError(translateError(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div style={{ maxWidth: 900, margin: '0 auto', padding: 16 }}>
      <h1>{t('bulkReceipt.title')}</h1>
      <p style={{ color: '#666', marginBottom: 24 }}>{t('bulkReceipt.subtitle')}</p>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <label>
          {t('bulkReceipt.declaration')}
          <select value={declarationId} onChange={(e) => setDeclarationId(e.target.value)} style={{ width: '100%', padding: 6 }}>
            <option value="">—</option>
            {declarations.map((d) => (
              <option key={d.id} value={d.id}>
                {d.declarationNumber} · MRN {d.mrn} · {formatDate(d.declarationDate)}
              </option>
            ))}
          </select>
        </label>
        <label>
          {t('bulkReceipt.reference')}
          <input type="text" value={referenceNumber} onChange={(e) => setReferenceNumber(e.target.value)} style={{ width: '100%', padding: 6 }} />
        </label>
        <label>
          {t('bulkReceipt.warehouse')}
          <select value={warehouseId} onChange={(e) => { setWarehouseId(e.target.value); setTargetLocationId(''); }} style={{ width: '100%', padding: 6 }}>
            <option value="">—</option>
            {warehouses.map((w) => (
              <option key={w.id} value={w.id}>{w.code} · {w.name}</option>
            ))}
          </select>
        </label>
        <label>
          {t('bulkReceipt.targetLocation')}
          <select value={targetLocationId} onChange={(e) => setTargetLocationId(e.target.value)} disabled={!warehouseId} style={{ width: '100%', padding: 6 }}>
            <option value="">{t('bulkReceipt.autoLocation')}</option>
            {filteredLocations.map((l) => (
              <option key={l.id} value={l.id}>{l.code} · {l.name}</option>
            ))}
          </select>
        </label>
      </div>

      {selectedDecl && (
        <div style={{ marginTop: 16, padding: 12, background: '#f7f7f7', borderRadius: 4 }}>
          <strong>{selectedDecl.declarationNumber}</strong> · MRN <code>{selectedDecl.mrn}</code>
          {selectedDecl.totalCustomsValue !== undefined && selectedDecl.totalCustomsValue !== null && (
            <span> · {t('bulkReceipt.customsValue')}: {formatQuantity(selectedDecl.totalCustomsValue, 2)}</span>
          )}
        </div>
      )}

      <div style={{ marginTop: 16 }}>
        <button onClick={submit} disabled={submitting || !declarationId || !warehouseId} style={{ padding: '8px 16px' }}>
          {submitting ? t('common.saving') : t('bulkReceipt.commit')}
        </button>
      </div>

      {error && (
        <div style={{ marginTop: 16, padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4 }}>
          {error}
        </div>
      )}
      {result && (
        <div style={{ marginTop: 16, padding: 12, background: '#edf7ed', color: '#265c26', borderRadius: 4 }}>
          <div>{t('bulkReceipt.successHeading')}</div>
          <div>{t('bulkReceipt.linesCreated')}: <strong>{result.linesCreated}</strong></div>
          <div>{t('bulkReceipt.totalQuantity')}: <strong>{formatQuantity(result.totalQuantity, 2)}</strong></div>
          <div style={{ fontSize: 12, color: '#666' }}>ReceiptId: <code>{result.receiptId}</code></div>
        </div>
      )}
    </div>
  );
};

export default BulkReceiptFromDeclaration;
