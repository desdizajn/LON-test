import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { wmsApi } from '../services/api';

interface MinusiRow {
  itemId: string;
  itemCode: string;
  itemName: string;
  batchNumber: string | null;
  mrn: string | null;
  receiptsQty: number;
  issuesQty: number;
  netQty: number;
  currentBalance: number | null;
}

interface MinusiReport {
  negativeMovements: MinusiRow[];
  negativeBalances: MinusiRow[];
  totalChecked: number;
}

/**
 * P4.3 — MozniMinusi report page. Surfaces (Item, Batch, MRN) groups whose net
 * movement quantity is < 0, plus any InventoryBalance with Quantity < 0.
 * Legacy ELON shipped this so the expert could spot suspect stock entries
 * from legacy migration or issue / receipt mismatches.
 */
const MozniMinusi: React.FC = () => {
  const { t } = useTranslation();
  const [report, setReport] = useState<MinusiReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState<string | null>(null);

  const load = () => {
    setLoading(true);
    setErr(null);
    wmsApi
      .getMozniMinusi()
      .then((r) => setReport(r.data?.data ?? r.data))
      .catch((e) => setErr(e?.message || 'load failed'))
      .finally(() => setLoading(false));
  };

  useEffect(load, []);

  if (loading) return <div className="loading">{t('common.loading')}</div>;
  if (err) return <div style={{ color: '#b00020', padding: 20 }}>{err}</div>;
  if (!report) return null;

  const sections: Array<{ title: string; rows: MinusiRow[] }> = [
    { title: t('mozniMinusi.negativeMovements'), rows: report.negativeMovements },
    { title: t('mozniMinusi.negativeBalances'), rows: report.negativeBalances },
  ];

  return (
    <div style={{ padding: 20 }}>
      <div className="header" style={{ marginBottom: 10 }}>
        <h2>{t('mozniMinusi.title')}</h2>
        <button className="secondary-button" onClick={load}>
          {t('common.refresh')}
        </button>
      </div>
      <p style={{ color: '#666', marginBottom: 20 }}>{t('mozniMinusi.subtitle')}</p>
      <div style={{ fontSize: 13, color: '#888', marginBottom: 20 }}>
        {t('mozniMinusi.totalChecked')}: <b>{report.totalChecked.toLocaleString()}</b>
      </div>

      {sections.every((s) => s.rows.length === 0) ? (
        <div style={{ padding: 40, textAlign: 'center', color: '#5cb85c', fontSize: 16 }}>
          ✓ {t('mozniMinusi.noIssues')}
        </div>
      ) : (
        sections.map((s) =>
          s.rows.length === 0 ? null : (
            <div key={s.title} style={{ marginBottom: 30 }}>
              <h3 style={{ marginBottom: 10 }}>
                {s.title} <span style={{ color: '#b00020' }}>({s.rows.length})</span>
              </h3>
              <div style={{ overflowX: 'auto' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                  <thead>
                    <tr style={{ background: '#f4f6f8', textAlign: 'left' }}>
                      <th style={th}>{t('mozniMinusi.itemCode')}</th>
                      <th style={th}>{t('mozniMinusi.itemName')}</th>
                      <th style={th}>{t('mozniMinusi.batch')}</th>
                      <th style={th}>{t('mozniMinusi.mrn')}</th>
                      <th style={{ ...th, textAlign: 'right' }}>{t('mozniMinusi.receiptsQty')}</th>
                      <th style={{ ...th, textAlign: 'right' }}>{t('mozniMinusi.issuesQty')}</th>
                      <th style={{ ...th, textAlign: 'right' }}>{t('mozniMinusi.netQty')}</th>
                      <th style={{ ...th, textAlign: 'right' }}>{t('mozniMinusi.currentBalance')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {s.rows.map((r, i) => (
                      <tr key={`${r.itemId}-${r.batchNumber}-${r.mrn}-${i}`} style={{ borderBottom: '1px solid #eee' }}>
                        <td style={td}>{r.itemCode}</td>
                        <td style={td}>{r.itemName}</td>
                        <td style={td}>{r.batchNumber || '—'}</td>
                        <td style={td}>{r.mrn || '—'}</td>
                        <td style={{ ...td, textAlign: 'right' }}>{r.receiptsQty.toLocaleString()}</td>
                        <td style={{ ...td, textAlign: 'right' }}>{r.issuesQty.toLocaleString()}</td>
                        <td style={{ ...td, textAlign: 'right', color: '#b00020', fontWeight: 600 }}>
                          {r.netQty.toLocaleString()}
                        </td>
                        <td style={{ ...td, textAlign: 'right' }}>
                          {r.currentBalance === null ? '—' : r.currentBalance.toLocaleString()}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )
        )
      )}
    </div>
  );
};

const th: React.CSSProperties = { padding: '10px 12px', fontSize: 13, fontWeight: 600 };
const td: React.CSSProperties = { padding: '10px 12px', fontSize: 13 };

export default MozniMinusi;
