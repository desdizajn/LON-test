import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi } from '../../services/api';
import { formatQuantity, formatDate } from '../../utils/format';

/**
 * P6.36 — inline MRN consumption meter.
 *
 * Given an MRN string, fetches the registry row once and renders a compact
 * progress strip showing Used/Total + Discharged/Used + expiry days. Intended
 * for mounting next to any MRN reference in the UI — Customs detail,
 * declaration list, inventory row, etc.
 */

type RegistryRow = {
  mrn: string;
  totalQuantity: number;
  usedQuantity: number;
  dischargedQuantity?: number | null;
  expiryDate?: string | null;
  isActive: boolean;
};

interface Props {
  mrn: string;
  compact?: boolean;
}

const MrnMeter: React.FC<Props> = ({ mrn, compact }) => {
  const { t } = useTranslation();
  const [row, setRow] = useState<RegistryRow | null>(null);
  const [error, setError] = useState<boolean>(false);

  useEffect(() => {
    let cancelled = false;
    if (!mrn) return;
    (async () => {
      try {
        const resp = await customsApi.getMRNByNumber(mrn);
        if (!cancelled) setRow(resp.data as RegistryRow);
      } catch {
        if (!cancelled) setError(true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [mrn]);

  if (error || !row) {
    return <span style={{ fontSize: 11, color: '#888' }}>{mrn}</span>;
  }

  const discharged = row.dischargedQuantity ?? 0;
  const outstanding = row.usedQuantity - discharged;
  const usedPct = row.totalQuantity > 0 ? (row.usedQuantity / row.totalQuantity) * 100 : 0;
  const dischargedPct = row.usedQuantity > 0 ? (discharged / row.usedQuantity) * 100 : 0;
  const days = row.expiryDate ? Math.round((new Date(row.expiryDate).getTime() - Date.now()) / 86_400_000) : null;
  const daysColor =
    days === null ? '#888' : days < 0 ? '#c00' : days < 14 ? '#e67e22' : days < 30 ? '#f1c40f' : '#27ae60';

  const strip = (
    <div style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 11 }}>
      <div title={`Used ${formatQuantity(row.usedQuantity)} / Total ${formatQuantity(row.totalQuantity)}`}>
        <div style={{ width: compact ? 80 : 120, height: 5, background: '#eee', borderRadius: 2, overflow: 'hidden' }}>
          <div style={{ width: `${Math.min(100, usedPct)}%`, height: '100%', background: usedPct > 95 ? '#e74c3c' : usedPct > 80 ? '#f1c40f' : '#27ae60' }} />
        </div>
        {!compact && <div style={{ color: '#555' }}>{t('mrnMeter.used')}: {formatQuantity(row.usedQuantity)} / {formatQuantity(row.totalQuantity)}</div>}
      </div>
      <div title={`Discharged ${formatQuantity(discharged)} / Used ${formatQuantity(row.usedQuantity)}`}>
        <div style={{ width: compact ? 80 : 120, height: 5, background: '#eee', borderRadius: 2, overflow: 'hidden' }}>
          <div style={{ width: `${Math.min(100, dischargedPct)}%`, height: '100%', background: dischargedPct > 95 ? '#27ae60' : dischargedPct > 50 ? '#f1c40f' : '#e74c3c' }} />
        </div>
        {!compact && <div style={{ color: '#555' }}>{t('mrnMeter.discharged')}: {formatQuantity(discharged)}</div>}
      </div>
      {outstanding > 0 && (
        <span style={{ color: '#e67e22', fontWeight: 'bold' }} title={t('mrnMeter.outstanding') as string}>
          ⚠ {formatQuantity(outstanding)}
        </span>
      )}
      {days !== null && (
        <span style={{ color: daysColor, fontWeight: 'bold' }} title={`${t('mrnMeter.expiry')}: ${formatDate(row.expiryDate)}`}>
          {days < 0 ? t('mrnMeter.expired') : `${days}d`}
        </span>
      )}
    </div>
  );

  return strip;
};

export default MrnMeter;
