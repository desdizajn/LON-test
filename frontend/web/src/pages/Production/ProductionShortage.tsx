import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { productionApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P8.5 — Material shortage across active production orders.
 *
 * Consumes `GET /Production/shortage` — server-side aggregate of
 * ProductionOrderMaterial rows for POs in Draft/Released/InProgress.
 * Per material: sum of (Required - Issued) across all POs, minus
 * available inventory (OK quality + Imported/null process-state).
 * Surfaces only materials where deficit > 0.
 */

type PoRef = {
  productionOrderId: string;
  orderNumber: string;
  plannedStartDate: string;
  plannedEndDate: string;
  remainingRequirement: number;
};

type Row = {
  itemId: string;
  itemCode: string;
  itemName: string;
  uoMId: string;
  uoMCode: string;
  totalRequiredRemaining: number;
  totalAvailable: number;
  deficit: number;
  affectedOrders: PoRef[];
};

type Report = {
  rows: Row[];
  totalActiveOrders: number;
  materialsShort: number;
};

const ProductionShortage: React.FC = () => {
  const { t } = useTranslation();
  const [report, setReport] = useState<Report | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const resp = await productionApi.getShortage();
        if (cancelled) return;
        const envelope = resp.data as { isSuccess?: boolean; data?: Report };
        setReport(envelope?.data ?? (resp.data as Report));
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const rows = useMemo(() => report?.rows ?? [], [report]);

  const totals = useMemo(() => {
    return rows.reduce(
      (acc, r) => {
        acc.required += r.totalRequiredRemaining;
        acc.available += r.totalAvailable;
        acc.deficit += r.deficit;
        return acc;
      },
      { required: 0, available: 0, deficit: 0 }
    );
  }, [rows]);

  const toggle = (id: string) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('productionShortage.title')}</h1>
      <p style={{ color: '#666' }}>{t('productionShortage.subtitle')}</p>

      <div style={{ display: 'flex', gap: 24, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, alignItems: 'center' }}>
        <div><small>{t('productionShortage.activeOrders')}</small><div style={{ fontWeight: 600 }}>{report?.totalActiveOrders ?? 0}</div></div>
        <div><small>{t('productionShortage.materialsShort')}</small><div style={{ fontWeight: 600, color: rows.length > 0 ? '#c62828' : '#2e7d32' }}>{rows.length}</div></div>
        <div><small>{t('productionShortage.totalDeficit')}</small><div style={{ fontWeight: 600, color: '#c62828' }}>{formatQuantity(totals.deficit)}</div></div>
        <button
          onClick={() =>
            exportToCsv(
              rows,
              [
                { key: 'itemCode', label: 'ItemCode' },
                { key: 'itemName', label: 'ItemName' },
                { key: 'uoMCode', label: 'UoM' },
                { key: 'totalRequiredRemaining', label: 'RequiredRemaining', type: 'number' },
                { key: 'totalAvailable', label: 'Available', type: 'number' },
                { key: 'deficit', label: 'Deficit', type: 'number' },
                { key: 'affectedPOs', label: 'AffectedPOs', get: (r) => r.affectedOrders.map((p) => p.orderNumber).join(' ') },
              ],
              'production-shortage'
            )
          }
          disabled={rows.length === 0}
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
              <th style={{ width: 30 }}></th>
              <th>{t('productionShortage.item')}</th>
              <th>{t('productionShortage.uom')}</th>
              <th>{t('productionShortage.required')}</th>
              <th>{t('productionShortage.available')}</th>
              <th>{t('productionShortage.deficit')}</th>
              <th>{t('productionShortage.affectedPOs')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && rows.length === 0 && (
              <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20, color: '#2e7d32' }}>{t('productionShortage.noShortage')}</td></tr>
            )}
            {!loading && rows.map((r) => {
              const isOpen = expanded.has(r.itemId);
              return (
                <React.Fragment key={r.itemId}>
                  <tr onClick={() => toggle(r.itemId)} style={{ cursor: 'pointer' }}>
                    <td>{isOpen ? '▼' : '▶'}</td>
                    <td><strong>{r.itemCode}</strong> {r.itemName}</td>
                    <td>{r.uoMCode}</td>
                    <td>{formatQuantity(r.totalRequiredRemaining)}</td>
                    <td>{formatQuantity(r.totalAvailable)}</td>
                    <td style={{ color: '#c62828', fontWeight: 600 }}>{formatQuantity(r.deficit)}</td>
                    <td>{r.affectedOrders.length}</td>
                  </tr>
                  {isOpen && r.affectedOrders.map((p) => (
                    <tr key={p.productionOrderId} style={{ background: '#fafafa' }}>
                      <td></td>
                      <td colSpan={3}>
                        <Link to={`/production/orders?order=${p.productionOrderId}`}><code>{p.orderNumber}</code></Link>
                      </td>
                      <td>{formatQuantity(p.remainingRequirement)} {r.uoMCode}</td>
                      <td colSpan={2}>{formatDate(p.plannedStartDate)} → {formatDate(p.plannedEndDate)}</td>
                    </tr>
                  ))}
                </React.Fragment>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default ProductionShortage;
