import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { formatDate } from '../../utils/format';
import {
  RiskRegisterItemDto,
  RiskSeverity,
  RiskStatus,
  useCreateRisk,
  useDeleteRisk,
  useRisksQuery,
  useUpdateRisk,
} from '../../hooks/queries/useRisks';

/**
 * P16.C1 — Escalations register backed by the RiskRegisterItem entity
 * (Kind=Escalation). Replaces the localStorage-only persistence.
 *
 * `party` (customer / supplier / internal) is stored in the shared
 * `category` column on RiskRegisterItem. `description` is stored in
 * `mitigation`. `resolution` maps to `resolution`. Keeping schema
 * unified — single table, dual surface.
 */

const SEVERITY_LABEL: Record<RiskSeverity, string> = {
  1: 'Low',
  2: 'Medium',
  3: 'High',
  4: 'Critical',
};

const STATUS_LABEL: Record<RiskStatus, string> = {
  1: 'Open',
  2: 'InReview',
  3: 'Mitigating',
  4: 'Resolved',
  5: 'Deferred',
  6: 'Closed',
};

const SEVERITY_COLOR: Record<RiskSeverity, string> = {
  1: '#2e7d32',
  2: '#f9a825',
  3: '#ef6c00',
  4: '#c62828',
};

const STATUS_COLOR: Record<RiskStatus, string> = {
  1: '#ef6c00',
  2: '#1976d2',
  3: '#7b1fa2',
  4: '#2e7d32',
  5: '#616161',
  6: '#424242',
};

// Escalation page surfaces a subset of statuses; the rest hide via dropdown
// but unmapped statuses still render correctly if the row arrives with one.
const ESCALATION_STATUSES: RiskStatus[] = [1, 2, 4, 5];

interface DraftState {
  title: string;
  party: string;
  severity: RiskSeverity;
  status: RiskStatus;
  owner: string;
  dueDate: string;
  description: string;
}

const today = () => new Date().toISOString().slice(0, 10);

const Escalations: React.FC = () => {
  const { t } = useTranslation();
  const { data: rows = [], isLoading } = useRisksQuery(2); // Kind=Escalation
  const createMut = useCreateRisk();
  const updateMut = useUpdateRisk();
  const deleteMut = useDeleteRisk();

  const [search, setSearch] = useState('');
  const [severityFilter, setSeverityFilter] = useState<RiskSeverity | 'All'>('All');
  const [statusFilter, setStatusFilter] = useState<RiskStatus | 'All'>(1);

  const [draft, setDraft] = useState<DraftState>({
    title: '',
    party: '',
    severity: 2,
    status: 1,
    owner: '',
    dueDate: today(),
    description: '',
  });

  async function add() {
    if (!draft.title.trim()) {
      toast.error(t('escalations.invalid') as string);
      return;
    }
    try {
      await createMut.mutateAsync({
        kind: 2,
        title: draft.title.trim(),
        category: draft.party || null,
        severity: draft.severity,
        status: draft.status,
        owner: draft.owner || null,
        mitigation: draft.description || null,
        dueDate: draft.dueDate || null,
      });
      setDraft({ ...draft, title: '', description: '' });
      toast.success(t('escalations.saved') as string);
    } catch (err: any) {
      toast.error(err.message || 'Failed');
    }
  }

  async function setStatusFor(r: RiskRegisterItemDto, status: RiskStatus) {
    try {
      await updateMut.mutateAsync({
        id: r.id,
        title: r.title,
        category: r.category,
        severity: r.severity,
        status,
        owner: r.owner,
        mitigation: r.mitigation,
        resolution: r.resolution,
        dueDate: r.dueDate,
        reviewDate: r.reviewDate,
      });
    } catch (err: any) {
      toast.error(err.message || 'Failed');
    }
  }

  async function updateResolution(r: RiskRegisterItemDto, resolution: string) {
    try {
      await updateMut.mutateAsync({
        id: r.id,
        title: r.title,
        category: r.category,
        severity: r.severity,
        status: r.status,
        owner: r.owner,
        mitigation: r.mitigation,
        resolution,
        dueDate: r.dueDate,
        reviewDate: r.reviewDate,
      });
    } catch (err: any) {
      toast.error(err.message || 'Failed');
    }
  }

  async function remove(id: string) {
    if (!window.confirm(t('escalations.confirmDelete') as string)) return;
    try {
      await deleteMut.mutateAsync(id);
    } catch (err: any) {
      toast.error(err.message || 'Failed');
    }
  }

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows.filter((r) => {
      if (severityFilter !== 'All' && r.severity !== severityFilter) return false;
      if (statusFilter !== 'All' && r.status !== statusFilter) return false;
      if (q && !`${r.title} ${r.category ?? ''} ${r.owner ?? ''} ${r.mitigation ?? ''}`.toLowerCase().includes(q)) return false;
      return true;
    });
  }, [rows, severityFilter, statusFilter, search]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('escalations.title')}</h1>
      <p style={{ color: '#666' }}>{t('escalations.subtitle')}</p>

      <fieldset style={{ border: '1px solid #ddd', borderRadius: 4, padding: 12, marginBottom: 12 }}>
        <legend>{t('escalations.newLegend')}</legend>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: 8, alignItems: 'end' }}>
          <label>{t('escalations.titleLabel')}
            <input type="text" value={draft.title} onChange={(e) => setDraft({ ...draft, title: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('escalations.party')}
            <input type="text" value={draft.party} onChange={(e) => setDraft({ ...draft, party: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('escalations.severity')}
            <select value={draft.severity} onChange={(e) => setDraft({ ...draft, severity: Number(e.target.value) as RiskSeverity })} style={{ padding: 6, width: '100%' }}>
              {(Object.keys(SEVERITY_LABEL) as unknown as RiskSeverity[]).map((s) => (
                <option key={s} value={s}>{t(`risks.severities.${SEVERITY_LABEL[s]}`)}</option>
              ))}
            </select>
          </label>
          <label>{t('escalations.owner')}
            <input type="text" value={draft.owner} onChange={(e) => setDraft({ ...draft, owner: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('escalations.dueDate')}
            <input type="date" value={draft.dueDate} onChange={(e) => setDraft({ ...draft, dueDate: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label style={{ gridColumn: '1 / -1' }}>{t('escalations.description')}
            <textarea rows={2} value={draft.description} onChange={(e) => setDraft({ ...draft, description: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <button onClick={add} disabled={createMut.isPending} style={{ padding: '8px 12px', background: 'var(--taris-blue-500, #1e88e5)', color: 'white', border: 'none', borderRadius: 4 }}>
            {createMut.isPending ? t('common.saving') : t('escalations.add')}
          </button>
        </div>
      </fieldset>

      <div style={{ display: 'flex', gap: 12, marginBottom: 10, alignItems: 'center', flexWrap: 'wrap' }}>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('escalations.searchPlaceholder') as string} style={{ padding: 6, minWidth: 200 }} />
        <select value={severityFilter} onChange={(e) => setSeverityFilter(e.target.value === 'All' ? 'All' : Number(e.target.value) as RiskSeverity)} style={{ padding: 6 }}>
          <option value="All">{t('risks.allSeverities')}</option>
          {(Object.keys(SEVERITY_LABEL) as unknown as RiskSeverity[]).map((s) => (
            <option key={s} value={s}>{t(`risks.severities.${SEVERITY_LABEL[s]}`)}</option>
          ))}
        </select>
        <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value === 'All' ? 'All' : Number(e.target.value) as RiskStatus)} style={{ padding: 6 }}>
          <option value="All">{t('escalations.allStatuses')}</option>
          {ESCALATION_STATUSES.map((s) => (
            <option key={s} value={s}>{t(`escalations.statuses.${STATUS_LABEL[s]}`, { defaultValue: STATUS_LABEL[s] })}</option>
          ))}
        </select>
        <span style={{ color: '#888', marginLeft: 'auto' }}>
          {isLoading ? t('common.loading') : t('escalations.rowCount', { count: filtered.length })}
        </span>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('escalations.severity')}</th>
              <th>{t('escalations.titleLabel')}</th>
              <th>{t('escalations.party')}</th>
              <th>{t('escalations.owner')}</th>
              <th>{t('escalations.dueDate')}</th>
              <th>{t('escalations.statusCol')}</th>
              <th>{t('escalations.resolution')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{isLoading ? t('common.loading') : t('escalations.empty')}</td></tr>}
            {filtered.map((r) => (
              <tr key={r.id}>
                <td><span style={{ padding: '2px 8px', borderRadius: 3, background: SEVERITY_COLOR[r.severity], color: 'white', fontSize: 12, fontWeight: 600 }}>{t(`risks.severities.${SEVERITY_LABEL[r.severity]}`)}</span></td>
                <td><strong>{r.title}</strong>{r.mitigation && <div style={{ fontSize: 12, color: '#666', marginTop: 4 }}>{r.mitigation}</div>}</td>
                <td>{r.category || '-'}</td>
                <td>{r.owner || '-'}</td>
                <td>{r.dueDate ? formatDate(r.dueDate) : '-'}</td>
                <td>
                  <select value={r.status} onChange={(e) => setStatusFor(r, Number(e.target.value) as RiskStatus)} style={{ padding: 4, borderColor: STATUS_COLOR[r.status] }} disabled={updateMut.isPending}>
                    {ESCALATION_STATUSES.map((s) => (
                      <option key={s} value={s}>{t(`escalations.statuses.${STATUS_LABEL[s]}`, { defaultValue: STATUS_LABEL[s] })}</option>
                    ))}
                  </select>
                </td>
                <td>
                  <input
                    type="text"
                    defaultValue={r.resolution ?? ''}
                    onBlur={(e) => {
                      if ((r.resolution ?? '') !== e.target.value) updateResolution(r, e.target.value);
                    }}
                    style={{ padding: 4, width: 180 }}
                  />
                </td>
                <td><button onClick={() => remove(r.id)} disabled={deleteMut.isPending} style={{ padding: '4px 10px', fontSize: 12, color: '#c62828' }}>×</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default Escalations;
