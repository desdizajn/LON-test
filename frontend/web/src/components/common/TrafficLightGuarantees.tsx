import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { guaranteeApi } from '../../services/api';

interface TrafficLightRow {
  id: string;
  accountNumber: string;
  accountName: string;
  currency: string;
  totalLimit: number;
  used: number;
  available: number;
  utilisationPercent: number;
  indicator: 'green' | 'yellow' | 'red' | 'critical';
}

/**
 * P4.4 — Traffic-light indicator widget.
 * Calls GET /api/guarantee/accounts/traffic-light and renders one row per account
 * with a colored utilisation bar (green < 60 < yellow < 80 < red < 95 < critical).
 * Drops in above the existing account-card grid on the Guarantees page.
 */
const TrafficLightGuarantees: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<TrafficLightRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    guaranteeApi
      .getTrafficLights()
      .then((r) => {
        if (!cancelled) setRows(r.data || []);
      })
      .catch((e) => {
        if (!cancelled) setErr(e?.message || 'load failed');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const colorFor = (i: TrafficLightRow['indicator']) =>
    i === 'critical' ? '#8b0000' : i === 'red' ? '#d9534f' : i === 'yellow' ? '#f0ad4e' : '#5cb85c';

  if (loading) return <div className="loading">{t('common.loading')}</div>;
  if (err) return <div style={{ color: '#b00020' }}>{err}</div>;
  if (rows.length === 0) return null;

  return (
    <div style={{ marginBottom: 30 }}>
      <h3 style={{ marginBottom: 5 }}>{t('trafficLight.title')}</h3>
      <div style={{ fontSize: 13, color: '#666', marginBottom: 15 }}>{t('trafficLight.subtitle')}</div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(360px, 1fr))', gap: 15 }}>
        {rows.map((r) => {
          const pct = Math.min(100, Math.max(0, r.utilisationPercent || 0));
          const color = colorFor(r.indicator);
          return (
            <div
              key={r.id}
              style={{
                border: '1px solid #e0e0e0',
                borderLeft: `5px solid ${color}`,
                borderRadius: 6,
                padding: 15,
                background: '#fff',
              }}
            >
              <div style={{ fontWeight: 600 }}>{r.accountName}</div>
              <div style={{ fontSize: 12, color: '#888', marginBottom: 10 }}>
                {r.accountNumber} · {r.currency}
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, marginBottom: 6 }}>
                <span>{t('trafficLight.used')}</span>
                <span style={{ fontFamily: 'monospace' }}>
                  {r.used.toLocaleString()} / {r.totalLimit.toLocaleString()}
                </span>
              </div>
              <div style={{ background: '#f0f0f0', height: 10, borderRadius: 5, overflow: 'hidden' }}>
                <div
                  style={{
                    width: `${pct}%`,
                    height: '100%',
                    background: color,
                    transition: 'width 300ms ease',
                  }}
                />
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 8, fontSize: 13 }}>
                <span style={{ color, fontWeight: 600 }}>
                  {pct.toFixed(2)}% · {t(`trafficLight.indicator${r.indicator.charAt(0).toUpperCase()}${r.indicator.slice(1)}`)}
                </span>
                <span>
                  {t('trafficLight.available')}: <b>{r.available.toLocaleString()}</b>
                </span>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};

export default TrafficLightGuarantees;
