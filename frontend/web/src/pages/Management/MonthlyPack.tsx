import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { financeApi, productionApi, managementApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity, formatPercent } from '../../utils/format';

/**
 * P13.10 — Monthly review pack.
 *
 * Executive single-page snapshot for the selected month: revenue, orders,
 * on-time %, outstanding invoices, alerts. Designed to be copied into a
 * slide deck or printed as PDF (use browser print).
 */

type Invoice = { issueDate: string; status: number; totalAmount: number };
type Order = { plannedEndDate: string; producedQuantity: number; status: number };
type Alert = { severity: 'Critical' | 'Warning' | 'Info'; category: string; title: string; description: string };

const MonthlyPack: React.FC = () => {
  const { t } = useTranslation();
  const today = new Date();
  const [month, setMonth] = useState<string>(`${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}`);
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [onTimePct, setOnTimePct] = useState<number>(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const [yy, mm] = month.split('-').map(Number);
        const from = new Date(yy, mm - 1, 1).toISOString();
        const to = new Date(yy, mm, 0, 23, 59, 59).toISOString();
        const [iResp, oResp, otResp, aResp] = await Promise.all([
          financeApi.getInvoices({}),
          productionApi.getOrders(),
          managementApi.getOnTime({ from, to }),
          managementApi.getAlerts(),
        ]);
        if (cancelled) return;
        const iEnv = iResp.data as { data?: Invoice[] };
        setInvoices(iEnv?.data ?? (iResp.data as Invoice[]) ?? []);
        setOrders((oResp.data as Order[]) ?? []);
        const ot = (otResp.data as any)?.data ?? otResp.data;
        setOnTimePct(ot?.overall?.onTimePercentage ?? 0);
        const alertsEnv = (aResp.data as any)?.data ?? aResp.data;
        setAlerts(alertsEnv ?? []);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [month]);

  const stats = useMemo(() => {
    const [yy, mm] = month.split('-').map(Number);
    const monthInvoices = invoices.filter((inv) => {
      if (inv.status === 4) return false;
      const d = new Date(inv.issueDate);
      return d.getFullYear() === yy && d.getMonth() === mm - 1;
    });
    const monthOrders = orders.filter((o) => {
      if (!o.plannedEndDate) return false;
      const d = new Date(o.plannedEndDate);
      return d.getFullYear() === yy && d.getMonth() === mm - 1;
    });
    const revenue = monthInvoices.reduce((s, inv) => s + inv.totalAmount, 0);
    const paid = monthInvoices.filter((inv) => inv.status === 3).reduce((s, inv) => s + inv.totalAmount, 0);
    const outstanding = monthInvoices.filter((inv) => inv.status === 2).reduce((s, inv) => s + inv.totalAmount, 0);
    const completedOrders = monthOrders.filter((o) => o.status === 4).length;
    const producedQty = monthOrders.reduce((s, o) => s + o.producedQuantity, 0);
    return { revenue, paid, outstanding, invoiceCount: monthInvoices.length, orderCount: monthOrders.length, completedOrders, producedQty };
  }, [invoices, orders, month]);

  const alertsBySeverity = useMemo(() => ({
    Critical: alerts.filter((a) => a.severity === 'Critical'),
    Warning: alerts.filter((a) => a.severity === 'Warning'),
    Info: alerts.filter((a) => a.severity === 'Info'),
  }), [alerts]);

  return (
    <div style={{ padding: 16 }}>
      <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginBottom: 12 }}>
        <h1 style={{ margin: 0 }}>{t('monthlyPack.title')}</h1>
        <input type="month" value={month} onChange={(e) => setMonth(e.target.value)} style={{ padding: 6 }} />
        <button onClick={() => window.print()} style={{ marginLeft: 'auto', padding: '6px 12px' }}>🖨 {t('monthlyPack.print')}</button>
      </div>
      <p style={{ color: '#666' }}>{t('monthlyPack.subtitle', { month })}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}
      {loading && <div>{t('common.loading')}</div>}

      {!loading && (
        <>
          <section style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 12, marginBottom: 20 }}>
            <Card title={t('monthlyPack.revenue') as string} value={formatQuantity(stats.revenue, 2)} color="#1976d2" />
            <Card title={t('monthlyPack.paid') as string} value={formatQuantity(stats.paid, 2)} color="#2e7d32" />
            <Card title={t('monthlyPack.outstanding') as string} value={formatQuantity(stats.outstanding, 2)} color="#e67e22" />
            <Card title={t('monthlyPack.invoices') as string} value={String(stats.invoiceCount)} color="#7b1fa2" />
            <Card title={t('monthlyPack.orders') as string} value={String(stats.orderCount)} color="#546e7a" />
            <Card title={t('monthlyPack.completedOrders') as string} value={String(stats.completedOrders)} color="#2e7d32" />
            <Card title={t('monthlyPack.producedQty') as string} value={formatQuantity(stats.producedQty, 0)} color="#0d47a1" />
            <Card title={t('monthlyPack.onTimePct') as string} value={formatPercent(onTimePct / 100, 1)} color={onTimePct > 90 ? '#2e7d32' : onTimePct > 75 ? '#f9a825' : '#c62828'} />
          </section>

          <section style={{ marginBottom: 20 }}>
            <h2 style={{ fontSize: 16, margin: '0 0 8px' }}>{t('monthlyPack.alertsTitle')}</h2>
            {alerts.length === 0 && <p style={{ color: '#888' }}>{t('monthlyPack.noAlerts')}</p>}
            {alerts.length > 0 && (
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12 }}>
                {(['Critical', 'Warning', 'Info'] as const).map((sev) => (
                  <div key={sev} style={{ border: '1px solid #ddd', borderRadius: 6, padding: 10 }}>
                    <h3 style={{ margin: 0, fontSize: 13, color: sev === 'Critical' ? '#c62828' : sev === 'Warning' ? '#e67e22' : '#1976d2' }}>
                      {t(`monthlyPack.severities.${sev}`)} ({alertsBySeverity[sev].length})
                    </h3>
                    <ul style={{ paddingLeft: 16, margin: '8px 0 0', fontSize: 13 }}>
                      {alertsBySeverity[sev].slice(0, 10).map((a, i) => (
                        <li key={i}>{a.title}</li>
                      ))}
                    </ul>
                  </div>
                ))}
              </div>
            )}
          </section>
        </>
      )}
    </div>
  );
};

const Card: React.FC<{ title: string; value: string; color: string }> = ({ title, value, color }) => (
  <div style={{ background: 'white', border: '1px solid #e5e7eb', borderRadius: 8, padding: 14 }}>
    <div style={{ fontSize: 11, color: '#666', textTransform: 'uppercase', letterSpacing: 0.3 }}>{title}</div>
    <div style={{ fontSize: 22, fontWeight: 700, color, marginTop: 4 }}>{value}</div>
  </div>
);

export default MonthlyPack;
