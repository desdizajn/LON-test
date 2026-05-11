import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { formatDate } from '../../utils/format';
import { exportToCsv } from '../../utils/export';
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
 * P16.C1 — Open risks register backed by the RiskRegisterItem entity.
 * Replaces the localStorage-only persistence the page used to depend on.
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

const CATEGORIES = ['Supplier', 'Quality', 'Machine', 'Legal', 'Financial', 'HR', 'Customs', 'Other'];

interface DraftState {
  title: string;
  category: string;
  severity: RiskSeverity;
  status: RiskStatus;
  owner: string;
  mitigation: string;
  reviewDate: string;
}

const today = () => new Date().toISOString().slice(0, 10);

const OpenRisks: React.FC = () => {
  const { t } = useTranslation();
  const { data: rows = [], isLoading } = useRisksQuery(1); // Kind=Risk
  const createMut = useCreateRisk();
  const updateMut = useUpdateRisk();
  const deleteMut = useDeleteRisk();

  const [search, setSearch] = useState('');
  const [severityFilter, setSeverityFilter] = useState<RiskSeverity | 'All'>('All');
  const [statusFilter, setStatusFilter] = useState<RiskStatus | 'All'>(1);

  const [draft, setDraft] = useState<DraftState>({
    title: '',
    category: 'Other',
    severity: 2,
    status: 1,
    owner: '',
    mitigation: '',
    reviewDate: today(),
  });

  async function add() {
    if (!draft.title.trim()) {
      toast.error(t('risks.invalid') as string);
      return;
    }
    try {
      await createMut.mutateAsync({
        kind: 1,
        title: draft.title.trim(),
        category: draft.category,
        severity: draft.severity,
        status: draft.status,
        owner: draft.owner || null,
        mitigation: draft.mitigation || null,
        reviewDate: draft.reviewDate || null,
      });
      setDraft({ ...draft, title: '', mitigation: '' });
      toast.success(t('risks.saved') as string);
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

  async function remove(id: string) {
    if (!window.confirm(t('risks.confirmDelete') as string)) return;
    try {
      await deleteMut.mutateAsync(id);
    } catch (err: any) {
      toast.error(err.message || 'Failed');
    }
  }

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows
      .filter((r) => {
        if (severityFilter !== 'All' && r.severity !== severityFilter) return false;
        if (statusFilter !== 'All' && r.status !== statusFilter) return false;
        if (q && !`${r.title} ${r.owner ?? ''} ${r.category ?? ''} ${r.mitigation ?? ''}`.toLowerCase().includes(q)) return false;
        return true;
      })
      .sort((a, b) => b.severity - a.severity);
  }, [rows, severityFilter, statusFilter, search]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('risks.title')}</h1>
      <p style={{ color: '#666' }}>{t('risks.subtitle')}</p>

      <fieldset style={{ border: '1px solid #ddd', borderRadius: 4, padding: 12, marginBottom: 12 }}>
        <legend>{t('risks.newLegend')}</legend>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: 8, alignItems: 'end' }}>
          <label>{t('risks.titleLabel')}
            <input type="text" value={draft.title} onChange={(e) => setDraft({ ...draft, title: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('risks.category')}
            <select value={draft.category} onChange={(e) => setDraft({ ...draft, category: e.target.value })} style={{ padding: 6, width: '100%' }}>
              {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
          </label>
          <label>{t('risks.severity')}
            <select value={draft.severity} onChange={(e) => setDraft({ ...draft, severity: Number(e.target.value) as RiskSeverity })} style={{ padding: 6, width: '100%' }}>
              {(Object.keys(SEVERITY_LABEL) as unknown as RiskSeverity[]).map((s) => (
                <option key={s} value={s}>{t(`risks.severities.${SEVERITY_LABEL[s]}`)}</option>
              ))}
            </select>
          </label>
          <label>{t('risks.owner')}
            <input type="text" value={draft.owner} onChange={(e) => setDraft({ ...draft, owner: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('risks.reviewDate')}
            <input type="date" value={draft.reviewDate} onChange={(e) => setDraft({ ...draft, reviewDate: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label style={{ gridColumn: '1 / -1' }}>{t('risks.mitigation')}
            <textarea rows={2} value={draft.mitigation} onChange={(e) => setDraft({ ...draft, mitigation: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <button onClick={add} disabled={createMut.isPending} style={{ padding: '8px 12px', background: 'var(--taris-blue-500, #1e88e5)', color: 'white', border: 'none', borderRadius: 4 }}>
            {createMut.isPending ? t('common.saving') : t('risks.add')}
          </button>
        </div>
      </fieldset>

      <div style={{ display: 'flex', gap: 12, marginBottom: 10, alignItems: 'center', flexWrap: 'wrap' }}>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('risks.searchPlaceholder') as string} style={{ padding: 6, minWidth: 200 }} />
        <select value={severityFilter} onChange={(e) => setSeverityFilter(e.target.value === 'All' ? 'All' : Number(e.target.value) as RiskSeverity)} style={{ padding: 6 }}>
          <option value="All">{t('risks.allSeverities')}</option>
          {(Object.keys(SEVERITY_LABEL) as unknown as RiskSeverity[]).map((s) => (
            <option key={s} value={s}>{t(`risks.severities.${SEVERITY_LABEL[s]}`)}</option>
          ))}
        </select>
        <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value === 'All' ? 'All' : Number(e.target.value) as RiskStatus)} style={{ padding: 6 }}>
          <option value="All">{t('risks.allStatuses')}</option>
          {(Object.keys(STATUS_LABEL) as unknown as RiskStatus[]).map((s) => (
            <option key={s} value={s}>{t(`risks.statuses.${STATUS_LABEL[s]}`, { defaultValue: STATUS_LABEL[s] })}</option>
          ))}
        </select>
        <span style={{ color: '#888', marginLeft: 'auto' }}>
          {isLoading ? t('common.loading') : t('risks.rowCount', { count: filtered.length })}
        </span>
        <button onClick={() => exportToCsv(filtered, [
          { key: 'title', label: t('risks.titleLabel') as string },
          { key: 'category', label: t('risks.category') as string },
          { key: 'severity', label: t('risks.severity') as string, get: (r: RiskRegisterItemDto) => SEVERITY_LABEL[r.severity] },
          { key: 'status', label: t('risks.statusCol') as string, get: (r: RiskRegisterItemDto) => STATUS_LABEL[r.status] },
          { key: 'owner', label: t('risks.owner') as string },
          { key: 'reviewDate', label: t('risks.reviewDate') as string, type: 'date' },
          { key: 'mitigation', label: t('risks.mitigation') as string },
        ], 'risks')} disabled={filtered.length === 0} style={{ padding: '6px 12px' }}>
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('risks.severity')}</th>
              <th>{t('risks.titleLabel')}</th>
              <th>{t('risks.category')}</th>
              <th>{t('risks.owner')}</th>
              <th>{t('risks.reviewDate')}</th>
              <th>{t('risks.mitigation')}</th>
              <th>{t('risks.statusCol')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{isLoading ? t('common.loading') : t('risks.empty')}</td></tr>}
            {filtered.map((r) => (
              <tr key={r.id}>
                <td><span style={{ padding: '2px 8px', borderRadius: 3, background: SEVERITY_COLOR[r.severity], color: 'white', fontSize: 12, fontWeight: 600 }}>{t(`risks.severities.${SEVERITY_LABEL[r.severity]}`)}</span></td>
                <td><strong>{r.title}</strong></td>
                <td>{r.category ?? '-'}</td>
                <td>{r.owner || '-'}</td>
                <td>{r.reviewDate ? formatDate(r.reviewDate) : '-'}</td>
                <td style={{ fontSize: 13 }}>{r.mitigation || '-'}</td>
                <td>
                  <select value={r.status} onChange={(e) => setStatusFor(r, Number(e.target.value) as RiskStatus)} style={{ padding: 4 }} disabled={updateMut.isPending}>
                    {(Object.keys(STATUS_LABEL) as unknown as RiskStatus[]).map((s) => (
                      <option key={s} value={s}>{t(`risks.statuses.${STATUS_LABEL[s]}`, { defaultValue: STATUS_LABEL[s] })}</option>
                    ))}
                  </select>
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

export default OpenRisks;
