import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi } from '../../services/api';

interface Props {
  onClose?: () => void;
}

/**
 * P4.2 — PEE060 generator panel. Lets the user pick a LON authorization + date
 * range, then downloads the XML produced by /api/customs/pee/060. Mounted as a
 * modal from the Customs page "Reports" menu.
 */
const Pee060Panel: React.FC<Props> = ({ onClose }) => {
  const { t } = useTranslation();
  const today = new Date().toISOString().slice(0, 10);
  const firstOfMonth = today.slice(0, 7) + '-01';

  const [auths, setAuths] = useState<Array<{ id: string; authorizationNumber: string }>>([]);
  const [authId, setAuthId] = useState('');
  const [from, setFrom] = useState(firstOfMonth);
  const [to, setTo] = useState(today);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    customsApi
      .getLONAuthorizations(true)
      .then((r) => {
        const list = (r.data || []).map((a: any) => ({ id: a.id, authorizationNumber: a.authorizationNumber }));
        setAuths(list);
        if (list.length && !authId) setAuthId(list[0].id);
      })
      .catch((e) => setErr(e?.message || 'auths load failed'));
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const handleGenerate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!authId) return;
    if (from > to) {
      setErr(t('pee.invalidDateRange'));
      return;
    }
    setBusy(true);
    setErr(null);
    try {
      const res = await customsApi.generatePee060(authId, from, to);
      const blob = new Blob([res.data], { type: 'application/xml' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      const selectedAuth = auths.find((a) => a.id === authId);
      const safe = selectedAuth?.authorizationNumber.replace(/[^\w.-]/g, '_') || 'auth';
      link.download = `PEE060_${safe}_${from}_${to}.xml`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || e?.message || 'generate failed');
    } finally {
      setBusy(false);
    }
  };

  const body = (
    <form
      onSubmit={handleGenerate}
      style={{
        background: '#fff',
        borderRadius: 8,
        padding: 24,
        minWidth: 440,
        maxWidth: 560,
        boxShadow: '0 10px 30px rgba(0,0,0,0.3)',
      }}
      onClick={(e) => e.stopPropagation()}
    >
      <h3 style={{ marginTop: 0 }}>{t('pee.pee060')}</h3>
      <div style={{ fontSize: 13, color: '#666', marginBottom: 15 }}>{t('pee.pee060Description')}</div>

      <div style={{ marginBottom: 15 }}>
        <label style={labelStyle}>{t('pee.authorization')} *</label>
        <select required value={authId} onChange={(e) => setAuthId(e.target.value)} style={inputStyle}>
          <option value="">{t('pee.selectAuthorization')}</option>
          {auths.map((a) => (
            <option key={a.id} value={a.id}>
              {a.authorizationNumber}
            </option>
          ))}
        </select>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 15, marginBottom: 20 }}>
        <div>
          <label style={labelStyle}>{t('pee.fromDate')} *</label>
          <input type="date" required value={from} onChange={(e) => setFrom(e.target.value)} style={inputStyle} />
        </div>
        <div>
          <label style={labelStyle}>{t('pee.toDate')} *</label>
          <input type="date" required value={to} onChange={(e) => setTo(e.target.value)} style={inputStyle} />
        </div>
      </div>

      {err && <div style={{ color: '#b00020', marginBottom: 15, fontSize: 13 }}>{err}</div>}

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
        {onClose && (
          <button type="button" onClick={onClose} className="secondary-button" disabled={busy}>
            {t('common.close')}
          </button>
        )}
        <button type="submit" className="primary-button" disabled={busy || !authId}>
          {busy ? t('pee.generating') : t('pee.generate')}
        </button>
      </div>
    </form>
  );

  if (!onClose) return body;

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
      {body}
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

export default Pee060Panel;
