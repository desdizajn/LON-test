import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { masterDataApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate } from '../../utils/format';
import { exportToCsv } from '../../utils/export';
import {
  EmployeeCertificationDto,
  useCertificationsQuery,
  useCreateCertification,
  useDeleteCertification,
} from '../../hooks/queries/useTrainings';

/**
 * P16.C2 — Training / certification records backed by the
 * `EmployeeCertification` entity. Replaces the localStorage-only
 * persistence the page used to depend on.
 */

type Employee = { id: string; employeeNumber: string; firstName: string; lastName: string; department?: string };

const SKILL_AREAS = ['Sewing', 'Cutting', 'Quality Control', 'Machine Operation', 'Safety', 'Customs', 'IT', 'Management', 'Other'];

interface DraftState {
  employeeId: string;
  certificationName: string;
  skillArea: string;
  issuingAuthority: string;
  issuedDate: string;
  expiryDate: string;
  certificateNumber: string;
}

const today = () => new Date().toISOString().slice(0, 10);

const TrainingPage: React.FC = () => {
  const { t } = useTranslation();
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [expiringOnly, setExpiringOnly] = useState(false);

  const { data: rows = [], isLoading } = useCertificationsQuery();
  const createMut = useCreateCertification();
  const deleteMut = useDeleteCertification();

  const [draft, setDraft] = useState<DraftState>({
    employeeId: '',
    certificationName: '',
    skillArea: 'Sewing',
    issuingAuthority: '',
    issuedDate: today(),
    expiryDate: '',
    certificateNumber: '',
  });

  useEffect(() => {
    (async () => {
      try {
        const resp = await masterDataApi.getEmployees();
        setEmployees((resp.data as Employee[]) ?? []);
      } catch (err) {
        setError(translateError(err));
      }
    })();
  }, []);

  async function add() {
    if (!draft.employeeId || !draft.certificationName.trim()) {
      toast.error(t('training.invalid') as string);
      return;
    }
    try {
      await createMut.mutateAsync({
        employeeId: draft.employeeId,
        certificationName: draft.certificationName.trim(),
        skillArea: draft.skillArea || null,
        issuingAuthority: draft.issuingAuthority || null,
        issuedDate: draft.issuedDate,
        expiryDate: draft.expiryDate || null,
        certificateNumber: draft.certificateNumber || null,
      });
      setDraft({ ...draft, certificationName: '', issuingAuthority: '', certificateNumber: '' });
      toast.success(t('training.saved') as string);
    } catch (err: any) {
      toast.error(err.message || 'Failed');
    }
  }

  async function remove(id: string) {
    if (!window.confirm(t('training.confirmDelete') as string)) return;
    try {
      await deleteMut.mutateAsync(id);
    } catch (err: any) {
      toast.error(err.message || 'Failed');
    }
  }

  const enriched = useMemo(() => {
    const now = Date.now();
    return rows.map((r) => {
      const exp = r.expiryDate ? new Date(r.expiryDate).getTime() : 0;
      const daysLeft = exp ? Math.round((exp - now) / 86_400_000) : null;
      return { ...r, daysLeft };
    });
  }, [rows]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return enriched
      .filter((r) => {
        if (expiringOnly && (r.daysLeft === null || r.daysLeft > 60)) return false;
        if (q) {
          const hay = `${r.employeeName ?? ''} ${r.certificationName} ${r.skillArea ?? ''} ${r.issuingAuthority ?? ''} ${r.certificateNumber ?? ''}`.toLowerCase();
          if (!hay.includes(q)) return false;
        }
        return true;
      })
      .sort((a, b) => {
        if (a.daysLeft === null) return 1;
        if (b.daysLeft === null) return -1;
        return a.daysLeft - b.daysLeft;
      });
  }, [enriched, expiringOnly, search]);

  const expiringCount = enriched.filter((r) => r.daysLeft !== null && r.daysLeft <= 60 && r.daysLeft >= 0).length;
  const expiredCount = enriched.filter((r) => r.daysLeft !== null && r.daysLeft < 0).length;

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('training.title')}</h1>
      <p style={{ color: '#666' }}>{t('training.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <fieldset style={{ border: '1px solid #ddd', borderRadius: 4, padding: 12, marginBottom: 12 }}>
        <legend>{t('training.newLegend')}</legend>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: 8, alignItems: 'end' }}>
          <label>{t('training.employee')}
            <select value={draft.employeeId} onChange={(e) => setDraft({ ...draft, employeeId: e.target.value })} style={{ padding: 6, width: '100%' }}>
              <option value="">—</option>
              {employees.map((e) => <option key={e.id} value={e.id}>{e.employeeNumber} · {e.firstName} {e.lastName}</option>)}
            </select>
          </label>
          <label>{t('training.topic')}
            <input type="text" value={draft.certificationName} onChange={(e) => setDraft({ ...draft, certificationName: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('training.skillArea')}
            <select value={draft.skillArea} onChange={(e) => setDraft({ ...draft, skillArea: e.target.value })} style={{ padding: 6, width: '100%' }}>
              {SKILL_AREAS.map((s) => <option key={s} value={s}>{s}</option>)}
            </select>
          </label>
          <label>{t('training.provider')}
            <input type="text" value={draft.issuingAuthority} onChange={(e) => setDraft({ ...draft, issuingAuthority: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('training.completionDate')}
            <input type="date" value={draft.issuedDate} onChange={(e) => setDraft({ ...draft, issuedDate: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('training.expiryDate')}
            <input type="date" value={draft.expiryDate} onChange={(e) => setDraft({ ...draft, expiryDate: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('training.certificate')}
            <input type="text" value={draft.certificateNumber} onChange={(e) => setDraft({ ...draft, certificateNumber: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <button onClick={add} disabled={createMut.isPending} style={{ padding: '8px 12px', background: 'var(--taris-blue-500, #1e88e5)', color: 'white', border: 'none', borderRadius: 4 }}>
            {createMut.isPending ? t('common.saving') : t('training.add')}
          </button>
        </div>
      </fieldset>

      <div style={{ display: 'flex', gap: 12, marginBottom: 10, alignItems: 'center', flexWrap: 'wrap' }}>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('training.searchPlaceholder') as string} style={{ padding: 6, minWidth: 200 }} />
        <label><input type="checkbox" checked={expiringOnly} onChange={(e) => setExpiringOnly(e.target.checked)} /> {t('training.expiringOnly')}</label>
        <span style={{ color: '#ef6c00' }}>{t('training.expiring', { count: expiringCount })}</span>
        <span style={{ color: '#c62828' }}>{t('training.expired', { count: expiredCount })}</span>
        <span style={{ color: '#888', marginLeft: 'auto' }}>
          {isLoading ? t('common.loading') : t('training.rowCount', { count: filtered.length })}
        </span>
        <button onClick={() => exportToCsv(filtered, [
          { key: 'employeeName', label: t('training.employee') as string, get: (r: EmployeeCertificationDto) => r.employeeName ?? '' },
          { key: 'certificationName', label: t('training.topic') as string },
          { key: 'skillArea', label: t('training.skillArea') as string },
          { key: 'issuingAuthority', label: t('training.provider') as string },
          { key: 'issuedDate', label: t('training.completionDate') as string, type: 'date' },
          { key: 'expiryDate', label: t('training.expiryDate') as string, type: 'date' },
          { key: 'certificateNumber', label: t('training.certificate') as string },
        ], 'training')}
          disabled={filtered.length === 0}
          style={{ padding: '6px 12px' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('training.employee')}</th>
              <th>{t('training.topic')}</th>
              <th>{t('training.skillArea')}</th>
              <th>{t('training.completionDate')}</th>
              <th>{t('training.expiryDate')}</th>
              <th>{t('training.daysLeft')}</th>
              <th>{t('training.certificate')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{isLoading ? t('common.loading') : t('training.empty')}</td></tr>}
            {filtered.map((r) => (
              <tr key={r.id}>
                <td><strong>{r.employeeName ?? r.employeeNumber ?? '-'}</strong></td>
                <td>{r.certificationName}</td>
                <td>{r.skillArea ?? '-'}</td>
                <td>{formatDate(r.issuedDate)}</td>
                <td>{r.expiryDate ? formatDate(r.expiryDate) : '-'}</td>
                <td style={{ color: r.daysLeft === null ? '#888' : r.daysLeft < 0 ? '#c62828' : r.daysLeft <= 60 ? '#ef6c00' : '#2e7d32', fontWeight: 600 }}>
                  {r.daysLeft === null ? '-' : r.daysLeft < 0 ? t('training.expiredBadge') : r.daysLeft}
                </td>
                <td>{r.certificateNumber || '-'}</td>
                <td><button onClick={() => remove(r.id)} disabled={deleteMut.isPending} style={{ padding: '4px 10px', fontSize: 12, color: '#c62828' }}>×</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default TrainingPage;
