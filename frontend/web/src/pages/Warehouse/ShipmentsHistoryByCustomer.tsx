import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { wmsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P7.8 — shipment history aggregated by customer × month.
 *
 * Plain aggregation over the existing /WMS/shipments endpoint. Period
 * defaults to last 12 months. Matrix: rows = customer, columns = months,
 * cells = shipment count + total qty.
 */

type Shipment = {
  id: string;
  shipmentNumber: string;
  shipmentDate: string;
  customerId?: string | null;
  customerName?: string | null;
  status: number;
  lines?: Array<{ quantity: number }>;
};

function monthKey(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
}

const ShipmentsHistoryByCustomer: React.FC = () => {
  const { t, i18n } = useTranslation();
  const [rows, setRows] = useState<Shipment[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [monthsBack, setMonthsBack] = useState<number>(12);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const resp = await wmsApi.getShipments(1, 2000);
        if (!cancelled) setRows((resp.data as Shipment[]) ?? []);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const { columns, matrix, totals } = useMemo(() => {
    const cols: string[] = [];
    const now = new Date();
    for (let i = monthsBack - 1; i >= 0; i--) {
      const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
      cols.push(monthKey(d));
    }

    const by = new Map<string, { customer: string; cells: Record<string, { count: number; qty: number }> }>();
    rows.forEach((s) => {
      const mk = monthKey(new Date(s.shipmentDate));
      if (!cols.includes(mk)) return;
      const key = s.customerId ?? '__none__';
      const customer = s.customerName ?? (t('shipmentsHistoryByCustomer.unknownCustomer') as string);
      const entry = by.get(key) ?? { customer, cells: {} };
      const cell = entry.cells[mk] ?? { count: 0, qty: 0 };
      cell.count += 1;
      cell.qty += s.lines?.reduce((a, l) => a + l.quantity, 0) ?? 0;
      entry.cells[mk] = cell;
      by.set(key, entry);
    });

    const matrixList = Array.from(by.values()).sort((a, b) => a.customer.localeCompare(b.customer, i18n.language));
    const totalsByCol: Record<string, { count: number; qty: number }> = {};
    cols.forEach((c) => (totalsByCol[c] = { count: 0, qty: 0 }));
    matrixList.forEach((r) => {
      cols.forEach((c) => {
        const v = r.cells[c];
        if (v) {
          totalsByCol[c].count += v.count;
          totalsByCol[c].qty += v.qty;
        }
      });
    });

    return { columns: cols, matrix: matrixList, totals: totalsByCol };
  }, [rows, monthsBack, i18n.language, t]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('shipmentsHistoryByCustomer.title')}</h1>
      <p style={{ color: '#666' }}>{t('shipmentsHistoryByCustomer.subtitle')}</p>

      <div style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'center' }}>
        <label>
          {t('shipmentsHistoryByCustomer.monthsBack')}:
          <select value={monthsBack} onChange={(e) => setMonthsBack(Number(e.target.value))} style={{ marginLeft: 6, padding: 4 }}>
            <option value={3}>3</option>
            <option value={6}>6</option>
            <option value={12}>12</option>
            <option value={24}>24</option>
          </select>
        </label>
        <span style={{ color: '#888', marginLeft: 'auto' }}>
          {t('shipmentsHistoryByCustomer.customerCount', { count: matrix.length })}
        </span>
        <button
          onClick={() =>
            exportToCsv(
              matrix.map((r) => {
                const row: Record<string, unknown> = { customer: r.customer };
                columns.forEach((c) => {
                  const v = r.cells[c];
                  row[`${c}_count`] = v?.count ?? 0;
                  row[`${c}_qty`] = v?.qty ?? 0;
                });
                return row;
              }),
              [
                { key: 'customer', label: t('shipmentsHistoryByCustomer.customer') as string },
                ...columns.flatMap((c) => [
                  { key: `${c}_count`, label: `${c} (#)`, type: 'number' as const, decimals: 0 },
                  { key: `${c}_qty`, label: `${c} (qty)`, type: 'number' as const },
                ]),
              ],
              'history-by-customer'
            )
          }
          disabled={matrix.length === 0}
          style={{ padding: '6px 12px' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div className="table-container">
        <table style={{ fontSize: 12 }}>
          <thead>
            <tr>
              <th>{t('shipmentsHistoryByCustomer.customer')}</th>
              {columns.map((c) => <th key={c} style={{ whiteSpace: 'nowrap' }}>{c}</th>)}
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={columns.length + 1} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && matrix.length === 0 && (
              <tr><td colSpan={columns.length + 1} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>
            )}
            {!loading && matrix.map((r, i) => (
              <tr key={i}>
                <td><strong>{r.customer}</strong></td>
                {columns.map((c) => {
                  const v = r.cells[c];
                  return (
                    <td key={c} style={{ textAlign: 'right' }}>
                      {v ? (
                        <>
                          <div>{v.count}×</div>
                          <div style={{ fontSize: 11, color: '#666' }}>{formatQuantity(v.qty, 0)}</div>
                        </>
                      ) : '-'}
                    </td>
                  );
                })}
              </tr>
            ))}
            {!loading && matrix.length > 0 && (
              <tr style={{ background: '#f4f4f4', fontWeight: 'bold' }}>
                <td>{t('shipmentsHistoryByCustomer.totals')}</td>
                {columns.map((c) => {
                  const v = totals[c];
                  return (
                    <td key={c} style={{ textAlign: 'right' }}>
                      <div>{v.count}×</div>
                      <div style={{ fontSize: 11, color: '#666' }}>{formatQuantity(v.qty, 0)}</div>
                    </td>
                  );
                })}
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default ShipmentsHistoryByCustomer;
