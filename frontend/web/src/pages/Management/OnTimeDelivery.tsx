import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { managementApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate } from '../../utils/format';

/**
 * P13.1 — On-time delivery report.
 *
 * For each shipment in the period, joins `ShipmentLine.BatchNumber →
 * ProductionReceipt.BatchNumber → PO.PlannedEndDate`. Buckets: on-time
 * (ShipmentDate ≤ planned), 1-7d late, >7d late, unknown (no PO linkage).
 *
 * The per-customer rollup excludes the Unknown bucket from the denominator
 * so the % doesn't drift just because a shipment's trace is incomplete.
 */

type ShipmentRow = {
  shipmentId: string;
  shipmentNumber: string;
  shipmentDate: string;
  customerId: string | null;
  customerCode: string | null;
  customerName: string | null;
  plannedEndDate: string | null;
  daysLate: number | null;
  bucket: number;
};

type CustomerRow = {
  customerId: string | null;
  customerName: string;
  totalShipments: number;
  onTime: number;
  late1To7: number;
  lateOver7: number;
  unknown: number;
  onTimePercentage: number;
};

type Report = {
  from: string;
  to: string;
  shipments: ShipmentRow[];
  byCustomer: CustomerRow[];
  overall: CustomerRow;
};

const BUCKET_LABEL: Record<number, { key: string; bg: string; color: string }> = {
  1: { key: 'onTime', bg: '#c8e6c9', color: '#2e7d32' },
  2: { key: 'late1To7', bg: '#fff3e0', color: '#ef6c00' },
  3: { key: 'lateOver7', bg: '#ffcdd2', color: '#c62828' },
  99: { key: 'unknown', bg: '#e0e0e0', color: '#616161' },
};

const OnTimeDelivery: React.FC = () => {
  const { t } = useTranslation();
  const [from, setFrom] = useState(new Date(Date.now() - 90 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10));
  const [to, setTo] = useState(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<Report | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const resp = await managementApi.getOnTime({ from, to });
      const env = resp.data as { data?: Report };
      setReport(env?.data ?? (resp.data as Report));
    } catch (err) {
      setError(translateError(err));
    } finally {
      setLoading(false);
    }
  }, [from, to]);

  useEffect(() => { load(); }, [load]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('management.onTime.title')}</h1>
      <p style={{ color: '#666' }}>{t('management.onTime.subtitle')}</p>

      {error && (
        <div style={{ padding: 10, background: '#ffebee', color: '#c62828', marginBottom: 12, borderRadius: 4 }}>
          {error}
          <button onClick={() => setError(null)} style={{ marginLeft: 8 }}>×</button>
        </div>
      )}

      <div style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'center' }}>
        <label>{t('common.date')} {t('management.onTime.from')}
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} style={{ marginLeft: 4 }} />
        </label>
        <label>{t('management.onTime.to')}
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)} style={{ marginLeft: 4 }} />
        </label>
        <button onClick={load} disabled={loading}>{loading ? t('common.loading') : t('common.refresh')}</button>
      </div>

      {report && (
        <>
          <div style={{ display: 'flex', gap: 16, marginBottom: 16, padding: 12, background: '#f5f5f5', borderRadius: 4, flexWrap: 'wrap' }}>
            <div>
              <small>{t('management.onTime.overall')}</small>
              <div style={{ fontWeight: 700, fontSize: 24, color: report.overall.onTimePercentage >= 90 ? '#2e7d32' : report.overall.onTimePercentage >= 75 ? '#ef6c00' : '#c62828' }}>
                {report.overall.onTimePercentage.toFixed(1)}%
              </div>
              <small style={{ color: '#666' }}>{report.overall.totalShipments} {t('management.onTime.shipments')}</small>
            </div>
            <div><small>{t('management.onTime.buckets.onTime')}</small><div style={{ fontWeight: 600, color: '#2e7d32' }}>{report.overall.onTime}</div></div>
            <div><small>{t('management.onTime.buckets.late1To7')}</small><div style={{ fontWeight: 600, color: '#ef6c00' }}>{report.overall.late1To7}</div></div>
            <div><small>{t('management.onTime.buckets.lateOver7')}</small><div style={{ fontWeight: 600, color: '#c62828' }}>{report.overall.lateOver7}</div></div>
            <div><small>{t('management.onTime.buckets.unknown')}</small><div style={{ fontWeight: 600, color: '#616161' }}>{report.overall.unknown}</div></div>
          </div>

          <h3>{t('management.onTime.byCustomer')}</h3>
          <table style={{ width: '100%', fontSize: 13, marginBottom: 20 }}>
            <thead>
              <tr>
                <th style={{ textAlign: 'left' }}>{t('management.onTime.customer')}</th>
                <th style={{ textAlign: 'right' }}>{t('management.onTime.shipments')}</th>
                <th style={{ textAlign: 'right', color: '#2e7d32' }}>{t('management.onTime.buckets.onTime')}</th>
                <th style={{ textAlign: 'right', color: '#ef6c00' }}>{t('management.onTime.buckets.late1To7')}</th>
                <th style={{ textAlign: 'right', color: '#c62828' }}>{t('management.onTime.buckets.lateOver7')}</th>
                <th style={{ textAlign: 'right', color: '#616161' }}>{t('management.onTime.buckets.unknown')}</th>
                <th style={{ textAlign: 'right' }}>%</th>
              </tr>
            </thead>
            <tbody>
              {report.byCustomer.map((c) => (
                <tr key={c.customerId ?? c.customerName}>
                  <td>{c.customerName}</td>
                  <td style={{ textAlign: 'right' }}>{c.totalShipments}</td>
                  <td style={{ textAlign: 'right' }}>{c.onTime}</td>
                  <td style={{ textAlign: 'right' }}>{c.late1To7}</td>
                  <td style={{ textAlign: 'right' }}>{c.lateOver7}</td>
                  <td style={{ textAlign: 'right' }}>{c.unknown}</td>
                  <td style={{ textAlign: 'right', fontWeight: 600, color: c.onTimePercentage >= 90 ? '#2e7d32' : c.onTimePercentage >= 75 ? '#ef6c00' : '#c62828' }}>
                    {c.onTimePercentage.toFixed(1)}%
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <h3>{t('management.onTime.shipments')} ({report.shipments.length})</h3>
          <table style={{ width: '100%', fontSize: 13 }}>
            <thead>
              <tr>
                <th>{t('management.onTime.shipmentNumber')}</th>
                <th>{t('management.onTime.shippedOn')}</th>
                <th>{t('management.onTime.customer')}</th>
                <th>{t('management.onTime.plannedEnd')}</th>
                <th style={{ textAlign: 'right' }}>{t('management.onTime.daysLate')}</th>
                <th>{t('management.onTime.bucket')}</th>
              </tr>
            </thead>
            <tbody>
              {report.shipments.map((s) => {
                const b = BUCKET_LABEL[s.bucket] ?? BUCKET_LABEL[99];
                return (
                  <tr key={s.shipmentId}>
                    <td>{s.shipmentNumber}</td>
                    <td>{formatDate(s.shipmentDate)}</td>
                    <td>{s.customerName ?? '—'}</td>
                    <td>{s.plannedEndDate ? formatDate(s.plannedEndDate) : '—'}</td>
                    <td style={{ textAlign: 'right' }}>{s.daysLate ?? '—'}</td>
                    <td>
                      <span style={{ background: b.bg, color: b.color, padding: '2px 6px', borderRadius: 3, fontSize: 12 }}>
                        {t(`management.onTime.buckets.${b.key}`)}
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
};

export default OnTimeDelivery;
