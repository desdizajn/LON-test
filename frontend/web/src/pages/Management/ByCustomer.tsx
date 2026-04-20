import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { managementApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P13.3 — by-customer rollup. Aggregates in one row per customer:
 *   - Open / Completed POs for the period (CustomerPartnerId-scoped).
 *   - Shipment count + total shipped qty (Shipped/Delivered only).
 *   - Invoices issued + outstanding + paid totals (Cancelled excluded).
 */

type Row = {
  customerId: string;
  customerCode: string;
  customerName: string;
  openPOs: number;
  completedPOs: number;
  producedQuantity: number;
  shipmentCount: number;
  shippedQuantity: number;
  invoicesIssued: number;
  invoicedOutstanding: number;
  invoicedPaid: number;
  currency: string;
};

type Report = {
  from: string;
  to: string;
  rows: Row[];
};

const ByCustomer: React.FC = () => {
  const { t } = useTranslation();
  const [from, setFrom] = useState(new Date(Date.now() - 180 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10));
  const [to, setTo] = useState(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<Report | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const resp = await managementApi.getByCustomer({ from, to });
      const env = resp.data as { data?: Report };
      setReport(env?.data ?? (resp.data as Report));
    } catch (err) {
      setError(translateError(err));
    } finally {
      setLoading(false);
    }
  }, [from, to]);

  useEffect(() => { load(); }, [load]);

  const filtered = useMemo(() => {
    if (!report) return [];
    const q = filter.trim().toLowerCase();
    if (!q) return report.rows;
    return report.rows.filter((r) =>
      r.customerCode.toLowerCase().includes(q) || r.customerName.toLowerCase().includes(q));
  }, [report, filter]);

  const totals = useMemo(() => {
    const t = filtered.reduce((acc, r) => ({
      producedQuantity: acc.producedQuantity + r.producedQuantity,
      shippedQuantity: acc.shippedQuantity + r.shippedQuantity,
      invoicedOutstanding: acc.invoicedOutstanding + r.invoicedOutstanding,
      invoicedPaid: acc.invoicedPaid + r.invoicedPaid,
      openPOs: acc.openPOs + r.openPOs,
      completedPOs: acc.completedPOs + r.completedPOs,
      shipmentCount: acc.shipmentCount + r.shipmentCount,
      invoicesIssued: acc.invoicesIssued + r.invoicesIssued,
    }), {
      producedQuantity: 0, shippedQuantity: 0, invoicedOutstanding: 0, invoicedPaid: 0,
      openPOs: 0, completedPOs: 0, shipmentCount: 0, invoicesIssued: 0,
    });
    return t;
  }, [filtered]);

  const exportCsv = () => {
    exportToCsv(filtered, [
      { key: 'customerCode', label: t('management.byCustomer.customerCode') },
      { key: 'customerName', label: t('management.byCustomer.customerName') },
      { key: 'openPOs', label: t('management.byCustomer.openPOs'), type: 'number', decimals: 0 },
      { key: 'completedPOs', label: t('management.byCustomer.completedPOs'), type: 'number', decimals: 0 },
      { key: 'producedQuantity', label: t('management.byCustomer.produced'), type: 'number', decimals: 2 },
      { key: 'shipmentCount', label: t('management.byCustomer.shipments'), type: 'number', decimals: 0 },
      { key: 'shippedQuantity', label: t('management.byCustomer.shipped'), type: 'number', decimals: 2 },
      { key: 'invoicedOutstanding', label: t('management.byCustomer.outstanding'), type: 'number', decimals: 2 },
      { key: 'invoicedPaid', label: t('management.byCustomer.paid'), type: 'number', decimals: 2 },
      { key: 'currency', label: t('management.byCustomer.currency') },
    ], 'by-customer');
  };

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('management.byCustomer.title')}</h1>
      <p style={{ color: '#666' }}>{t('management.byCustomer.subtitle')}</p>

      {error && (
        <div style={{ padding: 10, background: '#ffebee', color: '#c62828', marginBottom: 12, borderRadius: 4 }}>
          {error}
          <button onClick={() => setError(null)} style={{ marginLeft: 8 }}>×</button>
        </div>
      )}

      <div style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'center', flexWrap: 'wrap' }}>
        <label>{t('management.byCustomer.from')}
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} style={{ marginLeft: 4 }} />
        </label>
        <label>{t('management.byCustomer.to')}
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)} style={{ marginLeft: 4 }} />
        </label>
        <input placeholder={t('common.filter') ?? ''} value={filter} onChange={(e) => setFilter(e.target.value)} />
        <button onClick={load} disabled={loading}>{loading ? t('common.loading') : t('common.refresh')}</button>
        <button onClick={exportCsv}>{t('common.exportCsv')}</button>
      </div>

      {report && (
        <>
          <div style={{ display: 'flex', gap: 16, marginBottom: 16, padding: 12, background: '#f5f5f5', borderRadius: 4, flexWrap: 'wrap' }}>
            <div><small>{t('management.byCustomer.customers')}</small><div style={{ fontWeight: 600 }}>{filtered.length}</div></div>
            <div><small>{t('management.byCustomer.openPOs')}</small><div style={{ fontWeight: 600 }}>{totals.openPOs}</div></div>
            <div><small>{t('management.byCustomer.completedPOs')}</small><div style={{ fontWeight: 600 }}>{totals.completedPOs}</div></div>
            <div><small>{t('management.byCustomer.produced')}</small><div style={{ fontWeight: 600 }}>{formatQuantity(totals.producedQuantity, 2)}</div></div>
            <div><small>{t('management.byCustomer.shipped')}</small><div style={{ fontWeight: 600 }}>{formatQuantity(totals.shippedQuantity, 2)}</div></div>
            <div><small>{t('management.byCustomer.outstanding')}</small><div style={{ fontWeight: 600, color: '#1565c0' }}>{formatQuantity(totals.invoicedOutstanding, 2)}</div></div>
            <div><small>{t('management.byCustomer.paid')}</small><div style={{ fontWeight: 600, color: '#2e7d32' }}>{formatQuantity(totals.invoicedPaid, 2)}</div></div>
          </div>

          <table style={{ width: '100%', fontSize: 13 }}>
            <thead>
              <tr>
                <th style={{ textAlign: 'left' }}>{t('management.byCustomer.customerCode')}</th>
                <th style={{ textAlign: 'left' }}>{t('management.byCustomer.customerName')}</th>
                <th style={{ textAlign: 'right' }}>{t('management.byCustomer.openPOs')}</th>
                <th style={{ textAlign: 'right' }}>{t('management.byCustomer.completedPOs')}</th>
                <th style={{ textAlign: 'right' }}>{t('management.byCustomer.produced')}</th>
                <th style={{ textAlign: 'right' }}>{t('management.byCustomer.shipments')}</th>
                <th style={{ textAlign: 'right' }}>{t('management.byCustomer.shipped')}</th>
                <th style={{ textAlign: 'right' }}>{t('management.byCustomer.invoicesIssued')}</th>
                <th style={{ textAlign: 'right' }}>{t('management.byCustomer.outstanding')}</th>
                <th style={{ textAlign: 'right' }}>{t('management.byCustomer.paid')}</th>
                <th>{t('management.byCustomer.currency')}</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((r) => (
                <tr key={r.customerId}>
                  <td>{r.customerCode}</td>
                  <td>{r.customerName}</td>
                  <td style={{ textAlign: 'right' }}>{r.openPOs}</td>
                  <td style={{ textAlign: 'right' }}>{r.completedPOs}</td>
                  <td style={{ textAlign: 'right' }}>{formatQuantity(r.producedQuantity, 2)}</td>
                  <td style={{ textAlign: 'right' }}>{r.shipmentCount}</td>
                  <td style={{ textAlign: 'right' }}>{formatQuantity(r.shippedQuantity, 2)}</td>
                  <td style={{ textAlign: 'right' }}>{r.invoicesIssued}</td>
                  <td style={{ textAlign: 'right', color: '#1565c0' }}>{formatQuantity(r.invoicedOutstanding, 2)}</td>
                  <td style={{ textAlign: 'right', color: '#2e7d32' }}>{formatQuantity(r.invoicedPaid, 2)}</td>
                  <td>{r.currency}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
};

export default ByCustomer;
