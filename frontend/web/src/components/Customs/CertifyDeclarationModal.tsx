import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi } from '../../services/api';

interface Props {
  declarationId: string;
  declarationNumber?: string;
  onClose: () => void;
  onSuccess: () => void;
}

/**
 * P4.1 — Zaverka certification modal. Posts to /api/customs/declarations/{id}/certify.
 * Opened from the declarations list row action "Certify" when Status != Cleared.
 */
const CertifyDeclarationModal: React.FC<Props> = ({ declarationId, declarationNumber, onClose, onSuccess }) => {
  const { t } = useTranslation();
  const today = new Date().toISOString().slice(0, 10);
  const [zaverkaNumber, setZaverkaNumber] = useState('');
  const [zaverkaDate, setZaverkaDate] = useState(today);
  const [saving, setSaving] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!zaverkaNumber.trim()) return;
    setSaving(true);
    setErr(null);
    try {
      await customsApi.certifyDeclaration(declarationId, zaverkaNumber.trim(), new Date(zaverkaDate).toISOString());
      onSuccess();
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || e?.message || t('zaverka.failed'));
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
          minWidth: 400,
          maxWidth: 520,
          boxShadow: '0 10px 30px rgba(0,0,0,0.3)',
        }}
      >
        <h3 style={{ marginTop: 0 }}>{t('zaverka.certifyDeclaration')}</h3>
        <div style={{ fontSize: 13, color: '#666', marginBottom: 15 }}>
          {t('zaverka.certifyPrompt')}
        </div>
        {declarationNumber && (
          <div style={{ fontSize: 13, color: '#333', marginBottom: 15 }}>
            <b>{t('common.code')}:</b> {declarationNumber}
          </div>
        )}

        <div style={{ marginBottom: 15 }}>
          <label style={labelStyle}>{t('zaverka.zaverkaNumber')} *</label>
          <input
            type="text"
            autoFocus
            required
            maxLength={50}
            value={zaverkaNumber}
            onChange={(e) => setZaverkaNumber(e.target.value)}
            style={inputStyle}
          />
        </div>
        <div style={{ marginBottom: 20 }}>
          <label style={labelStyle}>{t('zaverka.zaverkaDate')} *</label>
          <input
            type="date"
            required
            value={zaverkaDate}
            onChange={(e) => setZaverkaDate(e.target.value)}
            style={inputStyle}
          />
        </div>

        {err && (
          <div style={{ color: '#b00020', marginBottom: 15, fontSize: 13 }}>
            {err}
          </div>
        )}

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
          <button type="button" onClick={onClose} className="secondary-button" disabled={saving}>
            {t('common.cancel')}
          </button>
          <button type="submit" className="primary-button" disabled={saving || !zaverkaNumber.trim()}>
            {saving ? t('common.saving') : t('zaverka.certify')}
          </button>
        </div>
      </form>
    </div>
  );
};

const labelStyle: React.CSSProperties = { display: 'block', marginBottom: 5, fontWeight: 600, fontSize: 13 };
const inputStyle: React.CSSProperties = {
  width: '100%',
  padding: '8px 10px',
  border: '1px solid #ccc',
  borderRadius: 4,
  fontSize: 14,
  boxSizing: 'border-box',
};

export default CertifyDeclarationModal;
