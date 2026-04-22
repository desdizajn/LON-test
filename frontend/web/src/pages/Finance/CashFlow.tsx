import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { financeApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P12.8 — Cash flow forecast.
 *
 * Buckets outstanding Issued invoices by dueDate: overdue, 0-7d, 8-30d,
 * 31-60d, 61+d. Gives operators a rolling view of expected cash with a
 * tactical "call these customers" list for overdue buckets.
 */

type Invoice = {
  id: string;
  number: string;
  partnerId: string;
  partnerName: string;
  status: number; // 1 Draft, 2 Issued, 3 Paid, 4 Cancelled
  totalAmount: number;
  currency: string;
  issueDate: string;
  dueDate: string;
};

type Bucket = {
  key: 'overdue' | 'due0_7' | 'due8_30' | 'due31_60' | 'due61plus';
  label: string;
  count: number;
  amount: number;
  color: string;
};

const CashFlow: React.FC = () => {
  const { t } = useTranslation();
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [activeBucket, setActiveBucket] = useState<Bucket['key'] | 'all'>('all');

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const resp = await financeApi.getInvoices({ status: 2 }); // Issued
        const env = resp.data as { data?: Invoice[] };
        if (!cancelled) setInvoices(env?.data ?? (resp.data as Invoice[]) ?? []);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const bucketKey = (days: number): Bucket['key'] => {
    if (days < 0) return 'overdue';
    if (days <= 7) return 'due0_7';
    if (days <= 30) return 'due8_30';
    if (days <= 60) return 'due31_60';
    return 'due61plus';
  };

  const enriched = useMemo(() => {
    const now = Date.now();
    return invoices.map((inv) => {
      const due = inv.dueDate ? new Date(inv.dueDate).getTime() : now;
      const days = Math.round((due - now) / 86_400_000);
      return { ...inv, daysToDue: days, bucket: bucketKey(days) };
    });
  }, [invoices]);

  const buckets = useMemo<Bucket[]>(() => {
    const tpl: Bucket[] = [
      { key: 'overdue', label: t('cashFlow.buckets.overdue'), count: 0, amount: 0, color: '#c62828' },
      { key: 'due0_7', label: t('cashFlow.buckets.due0_7'), count: 0, amount: 0, color: '#ef6c00' },
      { key: 'due8_30', label: t('cashFlow.buckets.due8_30'), count: 0, amount: 0, color: '#f9a825' },
      { key: 'due31_60', label: t('cashFlow.buckets.due31_60'), count: 0, amount: 0, color: '#1976d2' },
      { key: 'due61plus', label: t('cashFlow.buckets.due61plus'), count: 0, amount: 0, color: '#2e7d32' },
    ];
    enriched.forEach((inv) => {
      const b = tpl.find((x) => x.key === inv.bucket);
      if (b) { b.count++; b.amount += inv.totalAmount; }
    });
    return tpl;
  }, [enriched, t]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return enriched
      .filter((inv) => activeBucket === 'all' || inv.bucket === activeBucket)
      .filter((inv) => !q || `${inv.number} ${inv.partnerName}`.toLowerCase().includes(q))
      .sort((a, b) => a.daysToDue - b.daysToDue);
  }, [enriched, activeBucket, search]);

  const totalExpected = buckets.reduce((s, b) => s + b.amount, 0);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('cashFlow.title')}</h1>
      <p style={{ color: '#666' }}>{t('cashFlow.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: 10, marginBottom: 12 }}>
        <button onClick={() => setActiveBucket('all')} style={{ padding: 10, background: activeBucket === 'all' ? '#e7f2fe' : '#f5f5f5', border: '1px solid #ddd', borderRadius: 6 }}>
          <div style={{ fontSize: 12, color: '#666' }}>{t('cashFlow.totalExpected')}</div>
          <div style={{ fontWeight: 700, fontSize: 18 }}>{formatQuantity(totalExpected, 2)}</div>
          <div style={{ fontSize: 11, color: '#888' }}>{enriched.length} {t('cashFlow.invoices')}</div>
        </button>
        {buckets.map((b) => (
          <button key={b.key} onClick={() => setActiveBucket(b.key)} style={{ padding: 10, background: activeBucket === b.key ? '#e7f2fe' : '#f5f5f5', border: '1px solid #ddd', borderRadius: 6, textAlign: 'left' }}>
            <div style={{ fontSize: 12, color: b.color, fontWeight: 600 }}>{b.label}</div>
            <div style={{ fontWeight: 700, fontSize: 16 }}>{formatQuantity(b.amount, 2)}</div>
            <div style={{ fontSize: 11, color: '#888' }}>{b.count} {t('cashFlow.invoices')}</div>
          </button>
        ))}
      </div>

      <div style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'center' }}>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('cashFlow.searchPlaceholder') as string} style={{ padding: 6, minWidth: 260 }} />
        <span style={{ color: '#888', marginLeft: 'auto' }}>{t('cashFlow.rowCount', { count: filtered.length })}</span>
        <button
          onClick={() => exportToCsv(filtered, [
            { key: 'number', label: t('cashFlow.number') as string },
            { key: 'partnerName', label: t('cashFlow.partner') as string },
            { key: 'dueDate', label: t('cashFlow.dueDate') as string, type: 'date' },
            { key: 'daysToDue', label: t('cashFlow.daysToDue') as string, type: 'number', decimals: 0 },
            { key: 'totalAmount', label: t('cashFlow.amount') as string, type: 'number' },
            { key: 'currency', label: t('cashFlow.currency') as string },
          ], 'cash-flow-forecast')}
          disabled={filtered.length === 0}
          style={{ padding: '6px 12px' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('cashFlow.number')}</th>
              <th>{t('cashFlow.partner')}</th>
              <th>{t('cashFlow.dueDate')}</th>
              <th>{t('cashFlow.daysToDue')}</th>
              <th>{t('cashFlow.amount')}</th>
              <th>{t('cashFlow.bucket')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filtered.length === 0 && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>}
            {!loading && filtered.map((inv) => {
              const b = buckets.find((x) => x.key === inv.bucket)!;
              return (
                <tr key={inv.id}>
                  <td><strong>{inv.number}</strong></td>
                  <td>{inv.partnerName}</td>
                  <td>{inv.dueDate ? new Date(inv.dueDate).toLocaleDateString() : '-'}</td>
                  <td style={{ color: b.color, fontWeight: 600 }}>{inv.daysToDue}</td>
                  <td>{formatQuantity(inv.totalAmount, 2)} {inv.currency}</td>
                  <td><span style={{ padding: '2px 8px', borderRadius: 3, background: b.color, color: 'white', fontSize: 12 }}>{b.label}</span></td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default CashFlow;
