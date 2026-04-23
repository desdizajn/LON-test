import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { api } from '../../services/api';
import { translateError } from '../../utils/translateError';

/**
 * Admin-only tenant (Uvoznik) manager. Lists every tenant on the platform,
 * lets admin toggle the per-tenant policy flags (InflateImportForWaste,
 * AllowFefoAutoPick), edit basic metadata, and create new tenants.
 *
 * Backend: GET/POST/PUT/DELETE /api/Tenants (Administrator role required).
 */

type Tenant = {
  id: string;
  code: string;
  name: string;
  legacyUvoznik?: string | null;
  taxNumber?: string | null;
  country?: string | null;
  contactName?: string | null;
  email?: string | null;
  phone?: string | null;
  customsAuthorizationNumber?: string | null;
  defaultLanguage?: string;
  isActive: boolean;
  inflateImportForWaste?: boolean;
  allowFefoAutoPick?: boolean;
  createdAt: string;
};

const empty: Partial<Tenant> = {
  code: '',
  name: '',
  defaultLanguage: 'mk',
  isActive: true,
  inflateImportForWaste: false,
  allowFefoAutoPick: true,
};

const TenantList: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Tenant[]>([]);
  const [loading, setLoading] = useState(false);
  const [editing, setEditing] = useState<Tenant | null>(null);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<Partial<Tenant>>(empty);
  const [saving, setSaving] = useState(false);

  const load = async () => {
    setLoading(true);
    try {
      const r = await api.get('/Tenants');
      setRows((r.data as Tenant[]) ?? []);
    } catch (err) {
      toast.error(translateError(err));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const startCreate = () => {
    setForm(empty);
    setEditing(null);
    setCreating(true);
  };

  const startEdit = (row: Tenant) => {
    setForm({ ...row });
    setEditing(row);
    setCreating(false);
  };

  const closeModal = () => {
    setEditing(null);
    setCreating(false);
    setForm(empty);
  };

  const submit = async () => {
    setSaving(true);
    try {
      if (creating) {
        await api.post('/Tenants', form);
        toast.success(t('tenants.createSuccess', 'Тенантот е креиран.'));
      } else if (editing) {
        await api.put(`/Tenants/${editing.id}`, form);
        toast.success(t('tenants.updateSuccess', 'Тенантот е изменет.'));
      }
      closeModal();
      load();
    } catch (err) {
      toast.error(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  const toggleFefo = async (row: Tenant) => {
    try {
      await api.put(`/Tenants/${row.id}/settings/fefo`, {
        allowFefoAutoPick: !row.allowFefoAutoPick,
      });
      toast.success(t('tenants.fefoUpdated', 'FEFO policy обновена.'));
      load();
    } catch (err) {
      toast.error(translateError(err));
    }
  };

  return (
    <div style={{ padding: 20 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 20 }}>
        <div>
          <h1 style={{ margin: 0 }}>{t('tenants.title', 'Тенанти (Uvoznici)')}</h1>
          <div style={{ color: '#666', fontSize: 13, marginTop: 5 }}>
            {t(
              'tenants.subtitle',
              'Admin-only регистар на сите tenants. Се корегираат per-tenant полици (inflate-for-waste, FEFO auto-pick) + мета-податоци (ЕМБС, царинска авторизација, јазик).'
            )}
          </div>
        </div>
        <button onClick={startCreate} className="btn btn-primary">
          {t('tenants.new', 'Нов тенант')}
        </button>
      </div>

      {loading && <div className="loading">{t('common.loading')}</div>}
      {!loading && rows.length === 0 && (
        <div style={{ padding: 40, textAlign: 'center', color: '#888' }}>
          {t('tenants.empty', 'Нема тенанти.')}
        </div>
      )}
      {!loading && rows.length > 0 && (
        <table className="data-table">
          <thead>
            <tr>
              <th>{t('tenants.col.code', 'Код')}</th>
              <th>{t('tenants.col.name', 'Име')}</th>
              <th>{t('tenants.col.legacy', 'Legacy Uvoznik')}</th>
              <th>{t('tenants.col.tax', 'ЕДБ')}</th>
              <th>{t('tenants.col.country', 'Земја')}</th>
              <th>{t('tenants.col.lang', 'Јазик')}</th>
              <th>{t('tenants.col.inflate', 'Inflate')}</th>
              <th>{t('tenants.col.fefo', 'FEFO')}</th>
              <th>{t('tenants.col.status', 'Активен')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.id}>
                <td><strong>{r.code}</strong></td>
                <td>{r.name}</td>
                <td>{r.legacyUvoznik || <span style={{ color: '#aaa' }}>—</span>}</td>
                <td>{r.taxNumber || <span style={{ color: '#aaa' }}>—</span>}</td>
                <td>{r.country || <span style={{ color: '#aaa' }}>—</span>}</td>
                <td>{r.defaultLanguage || 'mk'}</td>
                <td>{r.inflateImportForWaste ? '✓' : '—'}</td>
                <td>
                  <button className="btn btn-sm btn-outline" onClick={() => toggleFefo(r)}>
                    {r.allowFefoAutoPick ? '✓' : '—'}
                  </button>
                </td>
                <td>
                  <span
                    style={{
                      padding: '3px 8px',
                      borderRadius: 10,
                      fontSize: 12,
                      background: r.isActive ? '#d4edda' : '#f8d7da',
                      color: r.isActive ? '#155724' : '#721c24',
                    }}
                  >
                    {r.isActive ? t('common.active', 'активен') : t('common.inactive', 'неактивен')}
                  </span>
                </td>
                <td>
                  <button className="btn btn-sm btn-primary" onClick={() => startEdit(r)}>
                    {t('common.edit', 'Измени')}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {(creating || editing) && (
        <div
          onClick={() => !saving && closeModal()}
          style={{
            position: 'fixed',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            background: 'rgba(0,0,0,0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
          }}
        >
          <div
            onClick={(e) => e.stopPropagation()}
            style={{
              background: 'white',
              padding: 25,
              borderRadius: 8,
              minWidth: 520,
              maxWidth: 640,
              maxHeight: '80vh',
              overflowY: 'auto',
            }}
          >
            <h3 style={{ marginTop: 0 }}>
              {creating ? t('tenants.newTitle', 'Нов тенант') : `${t('common.edit', 'Измени')}: ${editing?.code}`}
            </h3>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
              <label>
                {t('tenants.col.code', 'Код')} *
                <input
                  value={form.code || ''}
                  onChange={(e) => setForm({ ...form, code: e.target.value })}
                  disabled={!creating}
                  style={{ width: '100%', padding: 6 }}
                />
              </label>
              <label>
                {t('tenants.col.name', 'Име')} *
                <input
                  value={form.name || ''}
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                  style={{ width: '100%', padding: 6 }}
                />
              </label>
              <label>
                {t('tenants.col.legacy', 'Legacy Uvoznik')}
                <input
                  value={form.legacyUvoznik || ''}
                  onChange={(e) => setForm({ ...form, legacyUvoznik: e.target.value })}
                  style={{ width: '100%', padding: 6 }}
                />
              </label>
              <label>
                {t('tenants.col.tax', 'ЕДБ')}
                <input
                  value={form.taxNumber || ''}
                  onChange={(e) => setForm({ ...form, taxNumber: e.target.value })}
                  style={{ width: '100%', padding: 6 }}
                />
              </label>
              <label>
                {t('tenants.col.country', 'Земја (ISO-2)')}
                <input
                  value={form.country || ''}
                  onChange={(e) => setForm({ ...form, country: e.target.value })}
                  maxLength={2}
                  style={{ width: '100%', padding: 6 }}
                />
              </label>
              <label>
                {t('tenants.col.lang', 'Default јазик')}
                <select
                  value={form.defaultLanguage || 'mk'}
                  onChange={(e) => setForm({ ...form, defaultLanguage: e.target.value })}
                  style={{ width: '100%', padding: 6 }}
                >
                  <option value="mk">mk — Македонски</option>
                  <option value="sr">sr — Српски</option>
                  <option value="sq">sq — Албански</option>
                  <option value="en">en — English</option>
                </select>
              </label>
              <label style={{ gridColumn: '1 / span 2' }}>
                {t('tenants.col.customsAuth', 'Царинска авторизација #')}
                <input
                  value={form.customsAuthorizationNumber || ''}
                  onChange={(e) => setForm({ ...form, customsAuthorizationNumber: e.target.value })}
                  style={{ width: '100%', padding: 6 }}
                />
              </label>
              <label style={{ gridColumn: '1 / span 2' }}>
                <input
                  type="checkbox"
                  checked={!!form.inflateImportForWaste}
                  onChange={(e) => setForm({ ...form, inflateImportForWaste: e.target.checked })}
                />{' '}
                {t('tenants.inflateLabel', 'Inflate import-for-waste (TEKSPORT legacy)')}
              </label>
              <label style={{ gridColumn: '1 / span 2' }}>
                <input
                  type="checkbox"
                  checked={!!form.allowFefoAutoPick}
                  onChange={(e) => setForm({ ...form, allowFefoAutoPick: e.target.checked })}
                />{' '}
                {t('tenants.fefoLabel', 'Allow FEFO auto-pick on material issue')}
              </label>
              <label style={{ gridColumn: '1 / span 2' }}>
                <input
                  type="checkbox"
                  checked={form.isActive !== false}
                  onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                />{' '}
                {t('common.active', 'Активен')}
              </label>
            </div>
            <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end', marginTop: 15 }}>
              <button className="btn btn-outline" onClick={closeModal} disabled={saving}>
                {t('common.cancel', 'Откажи')}
              </button>
              <button className="btn btn-primary" onClick={submit} disabled={saving || !form.code || !form.name}>
                {saving ? t('common.saving', 'Се зачувува...') : t('common.save', 'Зачувај')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default TenantList;
