import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P6.37 — real page for "Царински предмети" (LON authorizations).
 * Consumes the existing GET /api/Customs/lon-authorizations endpoint.
 * Filter: active-only toggle + text search (authorization number / partner).
 */

type Authorization = {
  id: string;
  authorizationNumber: string;
  authorizationType?: string | null;
  systemType?: string | null;
  operationType?: string | null;
  issueDate?: string | null;
  expiryDate?: string | null;
  guaranteeAmount?: number | null;
  guaranteeReference?: string | null;
  competentCustomsOffice?: string | null;
  status?: string | null;
  partnerName?: string | null;
};

const LONAuthorizationsList: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Authorization[]>([]);
  const [activeOnly, setActiveOnly] = useState(true);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const resp = await customsApi.getLONAuthorizations(activeOnly);
        if (!cancelled) setRows((resp.data as Authorization[]) ?? []);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [activeOnly]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter(
      (r) =>
        r.authorizationNumber.toLowerCase().includes(q) ||
        (r.partnerName ?? '').toLowerCase().includes(q)
    );
  }, [rows, search]);

  function daysLeft(expiry?: string | null): number | null {
    if (!expiry) return null;
    const d = new Date(expiry);
    return Math.round((d.getTime() - Date.now()) / 86_400_000);
  }

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('lonAuthorizations.title')}</h1>
      <p style={{ color: '#666' }}>{t('lonAuthorizations.subtitle')}</p>

      <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginBottom: 12 }}>
        <label style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <input type="checkbox" checked={activeOnly} onChange={(e) => setActiveOnly(e.target.checked)} />
          {t('lonAuthorizations.activeOnly')}
        </label>
        <input
          type="text"
          placeholder={t('lonAuthorizations.searchPlaceholder') as string}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          style={{ padding: 6, minWidth: 260 }}
        />
        <span style={{ color: '#888', marginLeft: 'auto' }}>
          {t('lonAuthorizations.rowCount', { count: filtered.length })}
        </span>
        <button
          onClick={() =>
            exportToCsv(
              filtered,
              [
                { key: 'authorizationNumber', label: t('lonAuthorizations.authNumber') as string },
                { key: 'partnerName', label: t('lonAuthorizations.partner') as string },
                { key: 'issueDate', label: t('lonAuthorizations.issueDate') as string, type: 'date' },
                { key: 'expiryDate', label: t('lonAuthorizations.expiryDate') as string, type: 'date' },
                { key: 'guaranteeAmount', label: t('lonAuthorizations.guaranteeAmount') as string, type: 'number' },
                { key: 'status', label: t('lonAuthorizations.status') as string },
                { key: 'competentCustomsOffice', label: t('lonAuthorizations.customsOffice') as string },
              ],
              'lon-authorizations'
            )
          }
          disabled={filtered.length === 0}
          style={{ padding: '6px 12px' }}
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
              <th>{t('lonAuthorizations.authNumber')}</th>
              <th>{t('lonAuthorizations.partner')}</th>
              <th>{t('lonAuthorizations.issueDate')}</th>
              <th>{t('lonAuthorizations.expiryDate')}</th>
              <th>{t('lonAuthorizations.daysLeft')}</th>
              <th>{t('lonAuthorizations.guaranteeAmount')}</th>
              <th>{t('lonAuthorizations.status')}</th>
              <th>{t('lonAuthorizations.customsOffice')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && (
              <tr>
                <td colSpan={8} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td>
              </tr>
            )}
            {!loading && filtered.length === 0 && (
              <tr>
                <td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>
                  {t('common.noData')}
                </td>
              </tr>
            )}
            {!loading &&
              filtered.map((r) => {
                const left = daysLeft(r.expiryDate);
                const urgency =
                  left === null ? '#888' : left < 0 ? '#c00' : left < 14 ? '#e67e22' : left < 30 ? '#f1c40f' : '#27ae60';
                return (
                  <tr key={r.id}>
                    <td><strong>{r.authorizationNumber}</strong></td>
                    <td>{r.partnerName ?? '-'}</td>
                    <td>{formatDate(r.issueDate)}</td>
                    <td>{formatDate(r.expiryDate)}</td>
                    <td style={{ color: urgency, fontWeight: 'bold' }}>
                      {left === null ? '-' : left < 0 ? t('lonAuthorizations.expired') : left}
                    </td>
                    <td>{r.guaranteeAmount !== null && r.guaranteeAmount !== undefined ? formatQuantity(r.guaranteeAmount) : '-'}</td>
                    <td>{r.status ?? '-'}</td>
                    <td>{r.competentCustomsOffice ?? '-'}</td>
                  </tr>
                );
              })}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default LONAuthorizationsList;
