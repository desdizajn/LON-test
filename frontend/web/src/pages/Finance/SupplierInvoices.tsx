import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { masterDataApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import LocalStorageWarningBanner from '../../components/common/LocalStorageWarningBanner';
import { exportToCsv } from '../../utils/export';

/**
 * P12.6 — Supplier invoices (Добавувач invoices / AP).
 *
 * Lightweight AP register — persists to browser localStorage per tenant
 * until the backend SupplierInvoice entity lands. Operators can:
 *   • log a new supplier invoice (number, supplier, amount, dates)
 *   • mark as paid / unpaid / cancelled
 *   • see aging buckets + export CSV
 */

type Partner = { id: string; code: string; name: string; partnerType: number };

type SupplierInvoice = {
  id: string;
  number: string;
  supplierId: string;
  supplierName: string;
  issueDate: string;
  dueDate: string;
  amount: number;
  currency: string;
  status: 'Pending' | 'Paid' | 'Cancelled';
  reference: string;
  notes: string;
};

const storageKey = (tenantId: string) => `lon.supplierInvoices.${tenantId || 'default'}`;

function currentTenantId(): string {
  try {
    const raw = localStorage.getItem('token') || '';
    const part = raw.split('.')[1];
    if (!part) return 'default';
    const payload = JSON.parse(atob(part.replace(/-/g, '+').replace(/_/g, '/')));
    return payload['tenant_id'] || 'default';
  } catch { return 'default'; }
}

const STATUSES: SupplierInvoice['status'][] = ['Pending', 'Paid', 'Cancelled'];

const SupplierInvoices: React.FC = () => {
  const { t } = useTranslation();
  const [partners, setPartners] = useState<Partner[]>([]);
  const [rows, setRows] = useState<SupplierInvoice[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<SupplierInvoice['status'] | 'All'>('All');

  const today = new Date().toISOString().slice(0, 10);
  const [draft, setDraft] = useState<SupplierInvoice>({
    id: '', number: '', supplierId: '', supplierName: '', issueDate: today, dueDate: today,
    amount: 0, currency: 'EUR', status: 'Pending', reference: '', notes: '',
  });

  const tenantId = currentTenantId();

  useEffect(() => {
    (async () => {
      try {
        const resp = await masterDataApi.getPartners();
        const all = (resp.data as Partner[]) ?? [];
        setPartners(all.filter((p) => p.partnerType === 1)); // Suppliers
      } catch (err) {
        setError(translateError(err));
      }
    })();
  }, []);

  useEffect(() => {
    const raw = localStorage.getItem(storageKey(tenantId));
    if (raw) {
      try { setRows(JSON.parse(raw)); } catch { /* ignore */ }
    }
  }, [tenantId]);

  function persist(next: SupplierInvoice[]) {
    setRows(next);
    localStorage.setItem(storageKey(tenantId), JSON.stringify(next));
  }

  function add() {
    if (!draft.number.trim() || !draft.supplierId || draft.amount <= 0) {
      toast.error(t('supplierInvoices.invalid') as string);
      return;
    }
    const supplier = partners.find((p) => p.id === draft.supplierId);
    const entry: SupplierInvoice = {
      ...draft,
      id: crypto.randomUUID(),
      supplierName: supplier?.name ?? draft.supplierId,
    };
    persist([entry, ...rows]);
    toast.success(t('supplierInvoices.saved') as string);
    setDraft({ ...draft, id: '', number: '', amount: 0, reference: '', notes: '' });
  }

  function setStatus(id: string, status: SupplierInvoice['status']) {
    persist(rows.map((r) => (r.id === id ? { ...r, status } : r)));
  }
  function remove(id: string) {
    if (!window.confirm(t('supplierInvoices.confirmDelete') as string)) return;
    persist(rows.filter((r) => r.id !== id));
  }

  const enriched = useMemo(() => {
    const now = Date.now();
    return rows.map((r) => {
      const due = r.dueDate ? new Date(r.dueDate).getTime() : now;
      const daysToDue = Math.round((due - now) / 86_400_000);
      return { ...r, daysToDue };
    });
  }, [rows]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return enriched.filter((r) => {
      if (statusFilter !== 'All' && r.status !== statusFilter) return false;
      if (q && !`${r.number} ${r.supplierName} ${r.reference}`.toLowerCase().includes(q)) return false;
      return true;
    });
  }, [enriched, statusFilter, search]);

  const totals = useMemo(() => filtered.reduce((acc, r) => {
    if (r.status === 'Pending') acc.pending += r.amount;
    if (r.status === 'Paid') acc.paid += r.amount;
    acc.count++;
    return acc;
  }, { pending: 0, paid: 0, count: 0 }), [filtered]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('supplierInvoices.title')}</h1>
      <p style={{ color: '#666' }}>{t('supplierInvoices.subtitle')}</p>

      <LocalStorageWarningBanner />

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <fieldset style={{ border: '1px solid #ddd', borderRadius: 4, padding: 12, marginBottom: 12 }}>
        <legend>{t('supplierInvoices.newLegend')}</legend>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: 8, alignItems: 'end' }}>
          <label>{t('supplierInvoices.number')}
            <input type="text" value={draft.number} onChange={(e) => setDraft({ ...draft, number: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('supplierInvoices.supplier')}
            <select value={draft.supplierId} onChange={(e) => setDraft({ ...draft, supplierId: e.target.value })} style={{ padding: 6, width: '100%' }}>
              <option value="">—</option>
              {partners.map((p) => <option key={p.id} value={p.id}>{p.code} · {p.name}</option>)}
            </select>
          </label>
          <label>{t('supplierInvoices.issueDate')}
            <input type="date" value={draft.issueDate} onChange={(e) => setDraft({ ...draft, issueDate: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('supplierInvoices.dueDate')}
            <input type="date" value={draft.dueDate} onChange={(e) => setDraft({ ...draft, dueDate: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('supplierInvoices.amount')}
            <input type="number" min={0} step="0.01" value={draft.amount} onChange={(e) => setDraft({ ...draft, amount: Number(e.target.value) })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('supplierInvoices.currency')}
            <input type="text" maxLength={3} value={draft.currency} onChange={(e) => setDraft({ ...draft, currency: e.target.value.toUpperCase() })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('supplierInvoices.reference')}
            <input type="text" value={draft.reference} onChange={(e) => setDraft({ ...draft, reference: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <button onClick={add} style={{ padding: '8px 12px', background: 'var(--taris-blue-500, #1e88e5)', color: 'white', border: 'none', borderRadius: 4 }}>
            {t('supplierInvoices.add')}
          </button>
        </div>
        <div style={{ fontSize: 11, color: '#888', marginTop: 8 }}>{t('supplierInvoices.storageHint')}</div>
      </fieldset>

      <div style={{ display: 'flex', gap: 24, marginBottom: 10, padding: 10, background: '#f5f5f5', borderRadius: 4, alignItems: 'center', flexWrap: 'wrap' }}>
        <div><small>{t('supplierInvoices.pending')}</small><div style={{ fontWeight: 600, color: '#e67e22' }}>{formatQuantity(totals.pending, 2)}</div></div>
        <div><small>{t('supplierInvoices.paid')}</small><div style={{ fontWeight: 600, color: '#2e7d32' }}>{formatQuantity(totals.paid, 2)}</div></div>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('supplierInvoices.searchPlaceholder') as string} style={{ padding: 6, minWidth: 220 }} />
        <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)} style={{ padding: 6 }}>
          <option value="All">{t('common.all')}</option>
          {STATUSES.map((s) => <option key={s} value={s}>{t(`supplierInvoices.statuses.${s}`)}</option>)}
        </select>
        <button onClick={() => exportToCsv(filtered, [
          { key: 'number', label: t('supplierInvoices.number') as string },
          { key: 'supplierName', label: t('supplierInvoices.supplier') as string },
          { key: 'issueDate', label: t('supplierInvoices.issueDate') as string, type: 'date' },
          { key: 'dueDate', label: t('supplierInvoices.dueDate') as string, type: 'date' },
          { key: 'amount', label: t('supplierInvoices.amount') as string, type: 'number' },
          { key: 'currency', label: t('supplierInvoices.currency') as string },
          { key: 'status', label: t('supplierInvoices.status') as string },
          { key: 'reference', label: t('supplierInvoices.reference') as string },
        ], 'supplier-invoices')} disabled={filtered.length === 0} style={{ padding: '6px 12px', marginLeft: 'auto' }}>
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('supplierInvoices.number')}</th>
              <th>{t('supplierInvoices.supplier')}</th>
              <th>{t('supplierInvoices.issueDate')}</th>
              <th>{t('supplierInvoices.dueDate')}</th>
              <th>{t('supplierInvoices.daysToDue')}</th>
              <th>{t('supplierInvoices.amount')}</th>
              <th>{t('supplierInvoices.status')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('supplierInvoices.empty')}</td></tr>}
            {filtered.map((r) => {
              const isOverdue = r.status === 'Pending' && r.daysToDue < 0;
              return (
                <tr key={r.id} style={isOverdue ? { background: '#ffebee' } : undefined}>
                  <td><strong>{r.number}</strong></td>
                  <td>{r.supplierName}</td>
                  <td>{r.issueDate}</td>
                  <td>{r.dueDate}</td>
                  <td style={{ color: isOverdue ? '#c62828' : r.daysToDue <= 7 ? '#ef6c00' : '#2e7d32', fontWeight: 600 }}>{r.daysToDue}</td>
                  <td>{formatQuantity(r.amount, 2)} {r.currency}</td>
                  <td>
                    <select value={r.status} onChange={(e) => setStatus(r.id, e.target.value as SupplierInvoice['status'])} style={{ padding: 4 }}>
                      {STATUSES.map((s) => <option key={s} value={s}>{t(`supplierInvoices.statuses.${s}`)}</option>)}
                    </select>
                  </td>
                  <td><button onClick={() => remove(r.id)} style={{ padding: '4px 10px', fontSize: 12, color: '#c62828' }}>×</button></td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default SupplierInvoices;
