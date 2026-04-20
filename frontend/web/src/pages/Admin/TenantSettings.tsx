import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../services/api';

/**
 * Admin-only: toggles the policy flags on the current admin's tenant
 * (or any tenant listed). Wraps the existing PUT /api/tenants/{id} and
 * PUT /api/tenants/{id}/settings/fefo endpoints.
 */

type Tenant = {
  id: string;
  code: string;
  name: string;
  inflateImportForWaste: boolean;
  allowFefoAutoPick: boolean;
  defaultLanguage?: string;
  isActive: boolean;
};

const TenantSettings: React.FC = () => {
  const { t } = useTranslation();
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [loading, setLoading] = useState(true);
  const [savingId, setSavingId] = useState<string | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [msg, setMsg] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    setErr(null);
    try {
      const r = await api.get('/Tenants');
      const list: Tenant[] = r.data?.data ?? r.data ?? [];
      setTenants(Array.isArray(list) ? list : []);
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || e?.message || 'Load failed');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const toggleFefo = async (t: Tenant) => {
    setSavingId(t.id);
    setMsg(null);
    setErr(null);
    try {
      await api.put(`/Tenants/${t.id}/settings/fefo`, { allowFefoAutoPick: !t.allowFefoAutoPick });
      setTenants((prev) => prev.map((x) => (x.id === t.id ? { ...x, allowFefoAutoPick: !t.allowFefoAutoPick } : x)));
      setMsg(`FEFO ${!t.allowFefoAutoPick ? 'enabled' : 'disabled'} за ${t.code}`);
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || e?.message || 'Save failed');
    } finally {
      setSavingId(null);
    }
  };

  const toggleInflate = async (t: Tenant) => {
    setSavingId(t.id);
    setMsg(null);
    setErr(null);
    try {
      await api.put(`/Tenants/${t.id}`, { inflateImportForWaste: !t.inflateImportForWaste });
      setTenants((prev) => prev.map((x) => (x.id === t.id ? { ...x, inflateImportForWaste: !t.inflateImportForWaste } : x)));
      setMsg(`Inflate-for-waste ${!t.inflateImportForWaste ? 'enabled' : 'disabled'} за ${t.code}`);
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || e?.message || 'Save failed');
    } finally {
      setSavingId(null);
    }
  };

  return (
    <div style={{ padding: 20, maxWidth: 1100, margin: '0 auto' }}>
      <h2>⚙️ {t('tenantSettings.title', 'Политики по тенант')}</h2>
      <p style={{ color: '#666', marginTop: -6 }}>
        {t('tenantSettings.hint', 'Продукциско-битни flag-ови за секој тенант. Промените влегуваат во сила веднаш.')}
      </p>

      {err && <div style={{ background: '#fde', color: '#b00020', padding: 8, borderRadius: 4 }}>{err}</div>}
      {msg && <div style={{ background: '#e8f5e9', color: '#1b5e20', padding: 8, borderRadius: 4 }}>{msg}</div>}

      {loading ? (
        <div>Loading…</div>
      ) : (
        <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: 14 }}>
          <thead>
            <tr style={{ background: '#eee' }}>
              <th style={th}>Code</th>
              <th style={th}>Name</th>
              <th style={th}>Language</th>
              <th style={th}>Inflate-for-waste (I1)</th>
              <th style={th}>FEFO auto-pick (P5.2.5)</th>
              <th style={th}>Active</th>
            </tr>
          </thead>
          <tbody>
            {tenants.map((tn) => (
              <tr key={tn.id}>
                <td style={td}><strong>{tn.code}</strong></td>
                <td style={td}>{tn.name}</td>
                <td style={td}>{tn.defaultLanguage ?? 'mk'}</td>
                <td style={td}>
                  <label style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                    <input
                      type="checkbox"
                      checked={tn.inflateImportForWaste}
                      disabled={savingId === tn.id}
                      onChange={() => toggleInflate(tn)}
                    />
                    {tn.inflateImportForWaste ? t('common.on', 'вклучено') : t('common.off', 'исклучено')}
                  </label>
                </td>
                <td style={td}>
                  <label style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                    <input
                      type="checkbox"
                      checked={tn.allowFefoAutoPick}
                      disabled={savingId === tn.id}
                      onChange={() => toggleFefo(tn)}
                    />
                    {tn.allowFefoAutoPick ? t('common.on', 'вклучено') : t('common.off', 'исклучено')}
                  </label>
                </td>
                <td style={td}>{tn.isActive ? '✅' : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
};

const th: React.CSSProperties = { padding: '8px 10px', textAlign: 'left', borderBottom: '1px solid #ccc' };
const td: React.CSSProperties = { padding: '8px 10px', borderBottom: '1px solid #eee' };

export default TenantSettings;
