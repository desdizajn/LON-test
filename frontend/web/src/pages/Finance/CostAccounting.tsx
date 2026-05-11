import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { masterDataApi, api } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';
import {
  COST_RATE_SCOPE_LABEL,
  CostRateDto,
  CostRateScope,
  useCostRatesQuery,
  useCreateCostRate,
  useDeleteCostRate,
} from '../../hooks/queries/useCostRates';

/**
 * P16.C3.a — Cost accounting backed by the CostRate entity.
 * Scope picker (Machine / Operator / Shift / Operation / WorkCenter)
 * drives a scope-specific dropdown for ScopeId. Replaces the
 * localStorage-only persistence.
 */

type Reference = { id: string; code?: string | null; name?: string | null };

interface DraftState {
  scope: CostRateScope;
  scopeId: string;
  costPerHour: string;
  costPerUnit: string;
  currency: string;
  validFrom: string;
  notes: string;
}

const today = () => new Date().toISOString().slice(0, 10);

const CostAccounting: React.FC = () => {
  const { t } = useTranslation();
  const [workCenters, setWorkCenters] = useState<Reference[]>([]);
  const [machines, setMachines] = useState<Reference[]>([]);
  const [shifts, setShifts] = useState<Reference[]>([]);
  const [employees, setEmployees] = useState<Reference[]>([]);
  const [refError, setRefError] = useState<string | null>(null);

  const { data: rows = [], isLoading } = useCostRatesQuery();
  const createMut = useCreateCostRate();
  const deleteMut = useDeleteCostRate();

  const [search, setSearch] = useState('');
  const [scopeFilter, setScopeFilter] = useState<CostRateScope | 'All'>('All');

  const [draft, setDraft] = useState<DraftState>({
    scope: 5, // WorkCenter — closest to legacy default
    scopeId: '',
    costPerHour: '',
    costPerUnit: '',
    currency: 'EUR',
    validFrom: today(),
    notes: '',
  });

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [wcResp, shResp, mResp, empResp] = await Promise.all([
          masterDataApi.getWorkCenters(),
          api.get('/shifts'),
          masterDataApi.getMachines(),
          masterDataApi.getEmployees(),
        ]);
        if (cancelled) return;
        setWorkCenters((wcResp.data as Reference[]) ?? []);
        setShifts((shResp.data as Reference[]) ?? []);
        setMachines((mResp.data as Reference[]) ?? []);
        setEmployees((empResp.data as Reference[]) ?? []);
      } catch (err) {
        if (!cancelled) setRefError(translateError(err));
      }
    })();
    return () => { cancelled = true; };
  }, []);

  function scopeOptions(scope: CostRateScope): Reference[] {
    switch (scope) {
      case 1: return machines;
      case 2: return employees;
      case 3: return shifts;
      case 4: return []; // operations — free-form / future picker
      case 5: return workCenters;
    }
  }

  function resolveScopeName(scope: CostRateScope, scopeId?: string | null): string {
    if (!scopeId) return t('costAccounting.tenantWide', { defaultValue: '(tenant default)' }) as string;
    const opts = scopeOptions(scope);
    const hit = opts.find((o) => o.id === scopeId);
    if (!hit) return scopeId;
    return `${hit.code ?? ''} ${hit.name ?? ''}`.trim() || scopeId;
  }

  async function add() {
    const cph = draft.costPerHour ? Number(draft.costPerHour) : null;
    const cpu = draft.costPerUnit ? Number(draft.costPerUnit) : null;
    if ((!cph || cph <= 0) && (!cpu || cpu <= 0)) {
      toast.error(t('costAccounting.invalid') as string);
      return;
    }
    if (!draft.currency || draft.currency.length !== 3) {
      toast.error(t('costAccounting.currencyInvalid', { defaultValue: 'Currency must be 3 letters' }) as string);
      return;
    }
    try {
      await createMut.mutateAsync({
        scope: draft.scope,
        scopeId: draft.scopeId || null,
        costPerHour: cph,
        costPerUnit: cpu,
        currency: draft.currency.toUpperCase(),
        validFrom: draft.validFrom,
        notes: draft.notes || null,
      });
      setDraft({ ...draft, scopeId: '', costPerHour: '', costPerUnit: '', notes: '' });
      toast.success(t('costAccounting.saved') as string);
    } catch (err: any) {
      toast.error(err.message || 'Failed');
    }
  }

  async function remove(id: string) {
    if (!window.confirm(t('costAccounting.confirmDelete', { defaultValue: 'Delete this rate?' }) as string)) return;
    try {
      await deleteMut.mutateAsync(id);
    } catch (err: any) {
      toast.error(err.message || 'Failed');
    }
  }

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows.filter((r) => {
      if (scopeFilter !== 'All' && r.scope !== scopeFilter) return false;
      if (q) {
        const scopeName = resolveScopeName(r.scope, r.scopeId);
        const hay = `${COST_RATE_SCOPE_LABEL[r.scope]} ${scopeName} ${r.currency} ${r.notes ?? ''}`.toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    });
  }, [rows, scopeFilter, search, workCenters, shifts, machines, employees]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('costAccounting.title')}</h1>
      <p style={{ color: '#666' }}>{t('costAccounting.subtitle')}</p>

      {refError && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{refError}</div>}

      <fieldset style={{ border: '1px solid #ddd', borderRadius: 4, padding: 12, marginBottom: 12 }}>
        <legend>{t('costAccounting.upsertLegend')}</legend>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: 8, alignItems: 'end' }}>
          <label>{t('costAccounting.scope', { defaultValue: 'Scope' })}
            <select value={draft.scope} onChange={(e) => setDraft({ ...draft, scope: Number(e.target.value) as CostRateScope, scopeId: '' })} style={{ padding: 6, width: '100%' }}>
              {(Object.keys(COST_RATE_SCOPE_LABEL) as unknown as CostRateScope[]).map((s) => (
                <option key={s} value={s}>{COST_RATE_SCOPE_LABEL[s]}</option>
              ))}
            </select>
          </label>
          <label>{t('costAccounting.scopeId', { defaultValue: 'Scope target' })}
            <select value={draft.scopeId} onChange={(e) => setDraft({ ...draft, scopeId: e.target.value })} style={{ padding: 6, width: '100%' }}>
              <option value="">{t('costAccounting.tenantWide', { defaultValue: '(tenant default)' })}</option>
              {scopeOptions(draft.scope).map((o) => (
                <option key={o.id} value={o.id}>{(o.code ? `${o.code} · ` : '') + (o.name ?? o.id)}</option>
              ))}
            </select>
          </label>
          <label>{t('costAccounting.costPerHour', { defaultValue: 'Cost / hour' })}
            <input type="number" step="0.0001" min={0} value={draft.costPerHour} onChange={(e) => setDraft({ ...draft, costPerHour: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('costAccounting.costPerUnit', { defaultValue: 'Cost / unit' })}
            <input type="number" step="0.0001" min={0} value={draft.costPerUnit} onChange={(e) => setDraft({ ...draft, costPerUnit: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('costAccounting.currency')}
            <input type="text" maxLength={3} value={draft.currency} onChange={(e) => setDraft({ ...draft, currency: e.target.value.toUpperCase() })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('costAccounting.validFrom', { defaultValue: 'Valid from' })}
            <input type="date" value={draft.validFrom} onChange={(e) => setDraft({ ...draft, validFrom: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label style={{ gridColumn: '1 / -1' }}>{t('costAccounting.notes')}
            <input type="text" value={draft.notes} onChange={(e) => setDraft({ ...draft, notes: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <button onClick={add} disabled={createMut.isPending} style={{ padding: '8px 12px', background: 'var(--taris-blue-500, #1e88e5)', color: 'white', border: 'none', borderRadius: 4 }}>
            {createMut.isPending ? t('common.saving') : t('costAccounting.upsert')}
          </button>
        </div>
      </fieldset>

      <div style={{ display: 'flex', gap: 12, marginBottom: 10, alignItems: 'center' }}>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('costAccounting.searchPlaceholder') as string} style={{ padding: 6, minWidth: 240 }} />
        <select value={scopeFilter} onChange={(e) => setScopeFilter(e.target.value === 'All' ? 'All' : Number(e.target.value) as CostRateScope)} style={{ padding: 6 }}>
          <option value="All">{t('common.all', { defaultValue: 'All' })}</option>
          {(Object.keys(COST_RATE_SCOPE_LABEL) as unknown as CostRateScope[]).map((s) => (
            <option key={s} value={s}>{COST_RATE_SCOPE_LABEL[s]}</option>
          ))}
        </select>
        <span style={{ color: '#888' }}>
          {isLoading ? t('common.loading') : t('costAccounting.rowCount', { count: filtered.length })}
        </span>
        <button onClick={() => exportToCsv(filtered, [
          { key: 'scope', label: 'Scope', get: (r: CostRateDto) => COST_RATE_SCOPE_LABEL[r.scope] },
          { key: 'scopeName', label: 'Scope target', get: (r: CostRateDto) => resolveScopeName(r.scope, r.scopeId) },
          { key: 'costPerHour', label: 'Cost / hour', type: 'number', decimals: 4 },
          { key: 'costPerUnit', label: 'Cost / unit', type: 'number', decimals: 4 },
          { key: 'currency', label: t('costAccounting.currency') as string },
          { key: 'validFrom', label: 'Valid from', type: 'date' },
          { key: 'notes', label: t('costAccounting.notes') as string },
        ], 'cost-rates')}
          disabled={filtered.length === 0}
          style={{ padding: '6px 12px', marginLeft: 'auto' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('costAccounting.scope', { defaultValue: 'Scope' })}</th>
              <th>{t('costAccounting.scopeId', { defaultValue: 'Target' })}</th>
              <th>{t('costAccounting.costPerHour', { defaultValue: 'Cost / hr' })}</th>
              <th>{t('costAccounting.costPerUnit', { defaultValue: 'Cost / unit' })}</th>
              <th>{t('costAccounting.currency')}</th>
              <th>{t('costAccounting.validFrom', { defaultValue: 'Valid from' })}</th>
              <th>{t('costAccounting.notes')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!isLoading && filtered.length === 0 && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('costAccounting.empty')}</td></tr>}
            {!isLoading && filtered.map((r) => (
              <tr key={r.id}>
                <td>{COST_RATE_SCOPE_LABEL[r.scope]}</td>
                <td>{resolveScopeName(r.scope, r.scopeId)}</td>
                <td><strong>{r.costPerHour != null ? formatQuantity(r.costPerHour, 4) : '-'}</strong></td>
                <td>{r.costPerUnit != null ? formatQuantity(r.costPerUnit, 4) : '-'}</td>
                <td>{r.currency}</td>
                <td>{r.validFrom?.slice(0, 10) ?? '-'}</td>
                <td style={{ fontSize: 13 }}>{r.notes || '-'}</td>
                <td><button onClick={() => remove(r.id)} disabled={deleteMut.isPending} style={{ padding: '4px 10px', fontSize: 12, color: '#c62828' }}>×</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default CostAccounting;
