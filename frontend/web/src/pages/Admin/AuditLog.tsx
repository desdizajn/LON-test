import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../services/api';

/**
 * Admin-only audit log viewer over GET /api/audit. Lets admin filter by
 * entity type / id / action / date window, pretty-prints the stored
 * ChangesJson diffs so field-level changes are legible.
 */

type AuditRow = {
  id: string;
  entityType: string;
  entityId: string;
  action: string;
  changesJson: string;
  userId: string | null;
  userName: string | null;
  occurredAt: string;
};

const AuditLog: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<AuditRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const [entityType, setEntityType] = useState('');
  const [entityId, setEntityId] = useState('');
  const [action, setAction] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [take, setTake] = useState(200);

  const load = async () => {
    setLoading(true);
    setErr(null);
    try {
      const r = await api.get('/Audit', {
        params: {
          entityType: entityType || undefined,
          entityId: entityId || undefined,
          action: action || undefined,
          from: from || undefined,
          to: to || undefined,
          take,
        },
      });
      const list: AuditRow[] = r.data?.data ?? r.data ?? [];
      setRows(Array.isArray(list) ? list : []);
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || e?.message || 'Load failed');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const prettyJson = (raw: string) => {
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  };

  const actionColor = (a: string) => {
    if (a === 'Created') return '#2e7d32';
    if (a === 'Deleted') return '#b00020';
    if (a === 'Modified' || a === 'Updated') return '#b45309';
    return '#555';
  };

  return (
    <div style={{ padding: 20, maxWidth: 1200, margin: '0 auto' }}>
      <h2>📜 {t('audit.title', 'Audit log')}</h2>
      <p style={{ color: '#666', marginTop: -6 }}>
        {t('audit.hint', 'Admin-only. Сите промени на tenant-scoped ентитети со претходна/нова вредност.')}
      </p>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(6, 1fr)', gap: 10, background: '#f7f7f7', padding: 10, borderRadius: 6, marginBottom: 10 }}>
        <label>
          Entity type
          <input value={entityType} onChange={(e) => setEntityType(e.target.value)} placeholder="CustomsDeclaration" style={{ width: '100%' }} />
        </label>
        <label>
          Entity id
          <input value={entityId} onChange={(e) => setEntityId(e.target.value)} placeholder="GUID" style={{ width: '100%' }} />
        </label>
        <label>
          Action
          <select value={action} onChange={(e) => setAction(e.target.value)} style={{ width: '100%' }}>
            <option value="">—</option>
            <option value="Created">Created</option>
            <option value="Modified">Modified</option>
            <option value="Updated">Updated</option>
            <option value="Deleted">Deleted</option>
          </select>
        </label>
        <label>
          From
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} style={{ width: '100%' }} />
        </label>
        <label>
          To
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)} style={{ width: '100%' }} />
        </label>
        <label>
          Take
          <input type="number" value={take} onChange={(e) => setTake(Math.max(1, Number(e.target.value)))} style={{ width: '100%' }} />
        </label>
      </div>

      <div style={{ display: 'flex', gap: 8, marginBottom: 14 }}>
        <button onClick={load} disabled={loading} style={{ padding: '6px 14px' }}>
          {loading ? '…' : t('common.refresh', 'Освежи')}
        </button>
        <span style={{ color: '#666', alignSelf: 'center' }}>
          {rows.length} {t('audit.rows', 'редови')}
        </span>
      </div>

      {err && <div style={{ background: '#fde', color: '#b00020', padding: 8, borderRadius: 4 }}>{err}</div>}

      <table style={{ width: '100%', fontSize: 13, borderCollapse: 'collapse' }}>
        <thead>
          <tr style={{ background: '#eee' }}>
            <th style={th}>When</th>
            <th style={th}>User</th>
            <th style={th}>Entity</th>
            <th style={th}>Action</th>
            <th style={th}>Changes</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.id}>
              <td style={td}>{new Date(r.occurredAt).toLocaleString()}</td>
              <td style={td}>{r.userName ?? '—'}</td>
              <td style={td}>
                <div>{r.entityType}</div>
                <div style={{ fontSize: 10, color: '#999', fontFamily: 'monospace' }}>{r.entityId}</div>
              </td>
              <td style={td}>
                <span style={{ color: actionColor(r.action), fontWeight: 600 }}>{r.action}</span>
              </td>
              <td style={td}>
                <details>
                  <summary style={{ cursor: 'pointer', color: '#2b6cb0' }}>JSON</summary>
                  <pre style={{ background: '#fafafa', padding: 6, fontSize: 11, maxHeight: 260, overflow: 'auto' }}>
                    {prettyJson(r.changesJson)}
                  </pre>
                </details>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};

const th: React.CSSProperties = { padding: '8px 10px', textAlign: 'left', borderBottom: '1px solid #ccc' };
const td: React.CSSProperties = { padding: '8px 10px', borderBottom: '1px solid #eee', verticalAlign: 'top' };

export default AuditLog;
