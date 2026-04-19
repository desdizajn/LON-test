import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { wmsApi } from '../../services/api';

/**
 * P5.2.2 — one-click move of every balance for a batch into a target stage
 * (Production / Shipping / Quarantine / …). Triggered from the Inventory
 * page via a row action; prefilled with the row's batch and warehouse.
 */
interface Props {
  defaultBatchNumber: string;
  defaultWarehouseId?: string | null;
  onSuccess: (summary: { balancesMoved: number; totalQty: number }) => void;
  onCancel: () => void;
}

// LocationType enum values (matches Domain/Enums.cs)
const STAGES: Array<{ value: number; labelKey: string }> = [
  { value: 1, labelKey: 'locationType.Receiving' },
  { value: 2, labelKey: 'locationType.Storage' },
  { value: 3, labelKey: 'locationType.Picking' },
  { value: 4, labelKey: 'locationType.Production' },
  { value: 5, labelKey: 'locationType.Shipping' },
  { value: 6, labelKey: 'locationType.Quarantine' },
  { value: 7, labelKey: 'locationType.Blocked' },
];

const MoveBatchModal: React.FC<Props> = ({ defaultBatchNumber, defaultWarehouseId, onSuccess, onCancel }) => {
  const { t } = useTranslation();
  const [batchNumber, setBatchNumber] = useState(defaultBatchNumber);
  const [targetStage, setTargetStage] = useState<number>(4 /* Production */);
  const [reason, setReason] = useState('');
  const [loading, setLoading] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const submit = async () => {
    setErr(null);
    setLoading(true);
    try {
      const res = await (wmsApi as any).moveBatch({
        batchNumber,
        targetStage,
        warehouseId: defaultWarehouseId || null,
        reason: reason || null,
      });
      const data = res.data?.data;
      onSuccess({
        balancesMoved: data?.balancesMoved ?? 0,
        totalQty: data?.totalQuantityMoved ?? 0,
      });
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || e?.message || 'Move failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        background: 'rgba(0,0,0,0.4)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 1000,
      }}
    >
      <div
        style={{
          background: '#fff',
          padding: 24,
          borderRadius: 8,
          width: 420,
          boxShadow: '0 4px 16px rgba(0,0,0,0.2)',
        }}
      >
        <h3 style={{ marginTop: 0 }}>{t('moveBatch.title')}</h3>
        <p style={{ color: '#666', marginTop: 0, fontSize: 13 }}>{t('moveBatch.hint')}</p>

        {err && (
          <div style={{ background: '#fde', color: '#b00020', padding: 8, borderRadius: 4, marginBottom: 10 }}>
            {err}
          </div>
        )}

        <div style={{ marginBottom: 10 }}>
          <label>
            {t('moveBatch.batchNumber')}:
            <input
              type="text"
              value={batchNumber}
              onChange={(e) => setBatchNumber(e.target.value)}
              style={{ width: '100%', padding: 6 }}
            />
          </label>
        </div>

        <div style={{ marginBottom: 10 }}>
          <label>
            {t('moveBatch.targetStage')}:
            <select
              value={targetStage}
              onChange={(e) => setTargetStage(Number(e.target.value))}
              style={{ width: '100%', padding: 6 }}
            >
              {STAGES.map((s) => (
                <option key={s.value} value={s.value}>
                  {t(s.labelKey, s.labelKey)}
                </option>
              ))}
            </select>
          </label>
        </div>

        <div style={{ marginBottom: 14 }}>
          <label>
            {t('moveBatch.reason')} ({t('common.optional', 'optional')}):
            <input
              type="text"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              style={{ width: '100%', padding: 6 }}
            />
          </label>
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
          <button onClick={onCancel} disabled={loading}>
            {t('common.cancel')}
          </button>
          <button
            onClick={submit}
            disabled={loading || !batchNumber.trim()}
            style={{ background: '#2b6cb0', color: '#fff', padding: '6px 14px' }}
          >
            {loading ? t('common.saving') : t('moveBatch.submit')}
          </button>
        </div>
      </div>
    </div>
  );
};

export default MoveBatchModal;
