import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi } from '../../services/api';

interface Slot {
  slotIndex: number; // 0 = Zaguba, 1..4 = normal
  quantity: number;
  category?: string;
}

interface Props {
  onClose: () => void;
  onSuccess: () => void;
}

/**
 * P2.6c — waste declaration with optional P4.6 slot breakdown.
 * Total quantity required. If slots are added, their sum must match total.
 * SlotIndex 0 is "Zaguba" (unrecoverable loss), 1..4 are normal buckets.
 */
const WasteDeclarationModal: React.FC<Props> = ({ onClose, onSuccess }) => {
  const { t } = useTranslation();
  const today = new Date().toISOString().slice(0, 10);
  const [mrn, setMrn] = useState('');
  const [quantity, setQuantity] = useState<number>(0);
  const [wasteDate, setWasteDate] = useState(today);
  const [reason, setReason] = useState('');
  const [slots, setSlots] = useState<Slot[]>([]);
  const [saving, setSaving] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const slotSum = slots.reduce((s, x) => s + (Number(x.quantity) || 0), 0);
  const sumMismatch = slots.length > 0 && Math.abs(slotSum - quantity) > 0.0001;

  const addSlot = () => {
    // Pick the smallest unused slot index in {1..4}, fall back to Zaguba (0)
    const usedNonZ = new Set(slots.filter((s) => s.slotIndex !== 0).map((s) => s.slotIndex));
    const hasZ = slots.some((s) => s.slotIndex === 0);
    const next = [1, 2, 3, 4].find((n) => !usedNonZ.has(n));
    if (next !== undefined) {
      setSlots([...slots, { slotIndex: next, quantity: 0, category: '' }]);
    } else if (!hasZ) {
      setSlots([...slots, { slotIndex: 0, quantity: 0, category: '' }]);
    }
  };

  const updateSlot = (i: number, patch: Partial<Slot>) => {
    setSlots(slots.map((s, idx) => (idx === i ? { ...s, ...patch } : s)));
  };

  const removeSlot = (i: number) => setSlots(slots.filter((_, idx) => idx !== i));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!mrn.trim() || quantity <= 0 || !reason.trim()) return;
    if (sumMismatch) {
      setErr(t('waste.slotsSumMismatch', { sum: slotSum, total: quantity }));
      return;
    }
    setSaving(true);
    setErr(null);
    try {
      const payload: any = {
        mrn: mrn.trim(),
        wasteDate: new Date(wasteDate).toISOString(),
        quantity,
        reason: reason.trim(),
      };
      if (slots.length > 0) payload.slots = slots;
      await customsApi.createWasteDeclaration(payload);
      onSuccess();
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || e?.message || 'failed');
    } finally {
      setSaving(false);
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
      onClick={onClose}
    >
      <form
        onSubmit={handleSubmit}
        onClick={(e) => e.stopPropagation()}
        style={{
          background: '#fff',
          borderRadius: 8,
          padding: 24,
          minWidth: 560,
          maxWidth: 720,
          maxHeight: '90vh',
          overflowY: 'auto',
          boxShadow: '0 10px 30px rgba(0,0,0,0.3)',
        }}
      >
        <h3 style={{ marginTop: 0 }}>{t('waste.title')}</h3>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 15, marginBottom: 15 }}>
          <div>
            <label style={lbl}>MRN *</label>
            <input type="text" required value={mrn} onChange={(e) => setMrn(e.target.value)} style={inp} />
          </div>
          <div>
            <label style={lbl}>{t('common.date')} *</label>
            <input type="date" required value={wasteDate} onChange={(e) => setWasteDate(e.target.value)} style={inp} />
          </div>
        </div>

        <div style={{ marginBottom: 15 }}>
          <label style={lbl}>{t('waste.totalQty')} *</label>
          <input
            type="number"
            step="0.0001"
            required
            min={0}
            value={quantity}
            onChange={(e) => setQuantity(Number(e.target.value))}
            style={inp}
          />
        </div>

        <div style={{ marginBottom: 15 }}>
          <label style={lbl}>{t('waste.reason')} *</label>
          <textarea
            required
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            style={{ ...inp, minHeight: 60 }}
          />
        </div>

        <div style={{ marginBottom: 10, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <b style={{ fontSize: 13 }}>{t('waste.slots')}</b>
          <button type="button" className="btn btn-sm" onClick={addSlot} disabled={slots.length >= 5}>
            + {t('waste.addSlot')}
          </button>
        </div>
        <div style={{ fontSize: 12, color: '#666', marginBottom: 10 }}>{t('waste.slotsHint')}</div>

        {slots.map((s, i) => (
          <div
            key={i}
            style={{
              display: 'grid',
              gridTemplateColumns: '140px 1fr 1fr auto',
              gap: 8,
              marginBottom: 8,
              alignItems: 'end',
            }}
          >
            <select value={s.slotIndex} onChange={(e) => updateSlot(i, { slotIndex: Number(e.target.value) })} style={inp}>
              <option value={1}>{t('waste.slotNormal', { n: 1 })}</option>
              <option value={2}>{t('waste.slotNormal', { n: 2 })}</option>
              <option value={3}>{t('waste.slotNormal', { n: 3 })}</option>
              <option value={4}>{t('waste.slotNormal', { n: 4 })}</option>
              <option value={0}>{t('waste.slotZaguba')}</option>
            </select>
            <input
              type="text"
              placeholder={t('waste.slotCategory')}
              value={s.category || ''}
              onChange={(e) => updateSlot(i, { category: e.target.value })}
              style={inp}
            />
            <input
              type="number"
              step="0.0001"
              min={0}
              placeholder={t('waste.slotQuantity')}
              value={s.quantity}
              onChange={(e) => updateSlot(i, { quantity: Number(e.target.value) })}
              style={inp}
            />
            <button type="button" className="btn btn-sm" onClick={() => removeSlot(i)}>
              {t('waste.removeSlot')}
            </button>
          </div>
        ))}

        {sumMismatch && (
          <div style={{ color: '#b00020', fontSize: 13, marginBottom: 10 }}>
            {t('waste.slotsSumMismatch', { sum: slotSum, total: quantity })}
          </div>
        )}

        {err && <div style={{ color: '#b00020', marginBottom: 15, fontSize: 13 }}>{err}</div>}

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 20 }}>
          <button type="button" className="secondary-button" onClick={onClose} disabled={saving}>
            {t('common.cancel')}
          </button>
          <button
            type="submit"
            className="primary-button"
            disabled={saving || !mrn.trim() || quantity <= 0 || !reason.trim() || sumMismatch}
          >
            {saving ? t('common.saving') : t('common.save')}
          </button>
        </div>
      </form>
    </div>
  );
};

const lbl: React.CSSProperties = { display: 'block', marginBottom: 5, fontWeight: 600, fontSize: 13 };
const inp: React.CSSProperties = {
  width: '100%',
  padding: '8px 10px',
  border: '1px solid #ccc',
  borderRadius: 4,
  fontSize: 14,
  boxSizing: 'border-box',
};

export default WasteDeclarationModal;
