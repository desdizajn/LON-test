import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { masterDataApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';
import {
  SUPPLIER_INVOICE_STATUS_LABEL,
  SupplierInvoiceDto,
  SupplierInvoiceProjectedStatus,
  useCreateSupplierInvoice,
  useDeleteSupplierInvoice,
  useSupplierInvoicesQuery,
  useUpdateSupplierInvoice,
} from '../../hooks/queries/useSupplierInvoices';

/**
 * P16.C3.c — Supplier invoices register backed by the SupplierInvoice entity.
 * The `Overdue` status is **derived** by the backend (Status=Open + DueDate < today).
 */

type Partner = { id: string; code: string; name: string; type: number };

interface DraftState {
  number: string;
  supplierId: string;
  invoiceDate: string;
  dueDate: string;
  amount: string;
  currency: string;
  notes: string;
}

const today = () => new Date().toISOString().slice(0, 10);

const SupplierInvoices: React.FC = () => {
  const { t } = useTranslation();
  const [partners, setPartners] = useState<Partner[]>([]);
  const [refError, setRefError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<SupplierInvoiceProjectedStatus | 'All'>('All');

  const { data: rows = [], isLoading } = useSupplierInvoicesQuery(
    statusFilter === 'All' ? undefined : statusFilter
  );
  const createMut = useCreateSupplierInvoice();
  const updateMut = useUpdateSupplierInvoice();
  const deleteMut = useDeleteSupplierInvoice();

  const [draft, setDraft] = useState<DraftState>({
    number: '',
    supplierId: '',
    invoiceDate: today(),
    dueDate: today(),
    amount: '',
    currency: 'EUR',
    notes: '',
  });

  useEffect(() => {
    (async () => {
      try {
        const resp = await masterDataApi.getPartners();
        const all = (resp.data as Partner[]) ?? [];
        // type=Supplier — legacy numeric value left as in PartnersList page.
        setPartners(all.filter((p) => p.type === 1));
      } catch (err) {
        setRefError(translateError(err));
      }
    })();
  }, []);

  async function add() {
    if (!draft.number.trim() || !draft.supplierId || !draft.amount) {
      toast.error(t('supplierInvoices.invalid', { defaultValue: 'Number, supplier and amount are required' }) as string);
      return;
    }
    try {
      await createMut.mutateAsync({
        number: draft.number.trim(),
        supplierPartnerId: draft.supplierId,
        invoiceDate: draft.invoiceDate,
        dueDate: draft.dueDate,
        amount: Number(draft.amount),
        currency: draft.currency.toUpperCase(),
        notes: draft.notes || null,
      });
      setDraft({ ...draft, number: '', amount: '', notes: '' });
      toast.success(t('supplierInvoices.saved', { defaultValue: 'Saved' }) as string);
    } catch (err: any) {
      toast.error(err.message || 'Failed');
    }
  }

  async function markPaid(inv: SupplierInvoiceDto) {
    try {
      await updateMut.mutateAsync({
        id: inv.id,
        number: inv.number,
        supplierPartnerId: inv.supplierPartnerId,
        invoiceDate: inv.invoiceDate,
        dueDate: inv.dueDate,
        amount: inv.amount,
        currency: inv.currency,
        status: 2,
        paidDate: today(),
        notes: inv.notes,
      });
    } catch (err: any) {
      toast.error(err.message || 'Failed');
    }
  }

  async function markCancelled(inv: SupplierInvoiceDto) {
    try {
      await updateMut.mutateAsync({
        id: inv.id,
        number: inv.number,
        supplierPartnerId: inv.supplierPartnerId,
        invoiceDate: inv.invoiceDate,
        dueDate: inv.dueDate,
        amount: inv.amount,
        currency: inv.currency,
        status: 3,
        notes: inv.notes,
      });
    } catch (err: any) {
      toast.error(err.message || 'Failed');
    }
  }

  async function remove(id: string) {
    if (!window.confirm(t('supplierInvoices.confirmDelete', { defaultValue: 'Delete this invoice?' }) as string)) return;
    try {
      await deleteMut.mutateAsync(id);
    } catch (err: any) {
      toast.error(err.message || 'Failed');
    }
  }

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter((r) =>
      `${r.number} ${r.supplierCode ?? ''} ${r.supplierName ?? ''} ${r.notes ?? ''}`.toLowerCase().includes(q)
    );
  }, [rows, search]);

  const totals = useMemo(() => filtered.reduce(
    (acc, r) => {
      if (r.status === 2) acc.paid += r.amount;
      else if (r.status === 4) acc.overdue += r.amount;
      else if (r.status === 1) acc.open += r.amount;
      return acc;
    },
    { open: 0, paid: 0, overdue: 0 }
  ), [filtered]);

  function statusBadge(s: SupplierInvoiceProjectedStatus) {
    const colors: Record<SupplierInvoiceProjectedStatus, string> = {
      1: '#1976d2', 2: '#2e7d32', 3: '#616161', 4: '#c62828',
    };
    return (
      <span style={{ padding: '2px 8px', borderRadius: 3, background: colors[s], color: 'white', fontSize: 12, fontWeight: 600 }}>
        {SUPPLIER_INVOICE_STATUS_LABEL[s]}
      </span>
    );
  }

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('supplierInvoices.title')}</h1>
      <p style={{ color: '#666' }}>{t('supplierInvoices.subtitle')}</p>

      {refError && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{refError}</div>}

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
          <label>{t('supplierInvoices.invoiceDate', { defaultValue: 'Invoice date' })}
            <input type="date" value={draft.invoiceDate} onChange={(e) => setDraft({ ...draft, invoiceDate: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('supplierInvoices.dueDate')}
            <input type="date" value={draft.dueDate} onChange={(e) => setDraft({ ...draft, dueDate: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('supplierInvoices.amount')}
            <input type="number" step="0.01" min={0} value={draft.amount} onChange={(e) => setDraft({ ...draft, amount: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('supplierInvoices.currency')}
            <input type="text" maxLength={3} value={draft.currency} onChange={(e) => setDraft({ ...draft, currency: e.target.value.toUpperCase() })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label style={{ gridColumn: '1 / -1' }}>{t('supplierInvoices.notes')}
            <input type="text" value={draft.notes} onChange={(e) => setDraft({ ...draft, notes: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <button onClick={add} disabled={createMut.isPending} style={{ padding: '8px 12px', background: 'var(--taris-blue-500, #1e88e5)', color: 'white', border: 'none', borderRadius: 4 }}>
            {createMut.isPending ? t('common.saving') : t('supplierInvoices.add')}
          </button>
        </div>
      </fieldset>

      <div style={{ display: 'flex', gap: 12, marginBottom: 10, alignItems: 'center', flexWrap: 'wrap' }}>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('supplierInvoices.searchPlaceholder') as string} style={{ padding: 6, minWidth: 200 }} />
        <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value === 'All' ? 'All' : Number(e.target.value) as SupplierInvoiceProjectedStatus)} style={{ padding: 6 }}>
          <option value="All">{t('supplierInvoices.allStatuses', { defaultValue: 'All statuses' })}</option>
          {([1, 2, 3, 4] as SupplierInvoiceProjectedStatus[]).map((s) => (
            <option key={s} value={s}>{SUPPLIER_INVOICE_STATUS_LABEL[s]}</option>
          ))}
        </select>
        <span style={{ color: '#1976d2' }}>{t('supplierInvoices.openTotal', { defaultValue: 'Open' })}: <strong>{formatQuantity(totals.open, 2)}</strong></span>
        <span style={{ color: '#c62828' }}>{t('supplierInvoices.overdueTotal', { defaultValue: 'Overdue' })}: <strong>{formatQuantity(totals.overdue, 2)}</strong></span>
        <span style={{ color: '#2e7d32' }}>{t('supplierInvoices.paidTotal', { defaultValue: 'Paid' })}: <strong>{formatQuantity(totals.paid, 2)}</strong></span>
        <span style={{ color: '#888', marginLeft: 'auto' }}>
          {isLoading ? t('common.loading') : t('supplierInvoices.rowCount', { count: filtered.length })}
        </span>
        <button onClick={() => exportToCsv(filtered, [
          { key: 'number', label: t('supplierInvoices.number') as string },
          { key: 'supplierName', label: t('supplierInvoices.supplier') as string },
          { key: 'invoiceDate', label: 'Invoice date', type: 'date' },
          { key: 'dueDate', label: t('supplierInvoices.dueDate') as string, type: 'date' },
          { key: 'amount', label: t('supplierInvoices.amount') as string, type: 'number' },
          { key: 'currency', label: t('supplierInvoices.currency') as string },
          { key: 'status', label: 'Status', get: (r: SupplierInvoiceDto) => SUPPLIER_INVOICE_STATUS_LABEL[r.status] },
          { key: 'paidDate', label: 'Paid date', type: 'date' },
          { key: 'notes', label: t('supplierInvoices.notes') as string },
        ], 'supplier-invoices')}
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
              <th>{t('supplierInvoices.number')}</th>
              <th>{t('supplierInvoices.supplier')}</th>
              <th>{t('supplierInvoices.invoiceDate', { defaultValue: 'Invoice date' })}</th>
              <th>{t('supplierInvoices.dueDate')}</th>
              <th>{t('supplierInvoices.amount')}</th>
              <th>Status</th>
              <th>Paid</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!isLoading && filtered.length === 0 && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('supplierInvoices.empty', { defaultValue: 'No invoices' })}</td></tr>}
            {!isLoading && filtered.map((r) => (
              <tr key={r.id}>
                <td><code>{r.number}</code></td>
                <td>{r.supplierName ?? r.supplierCode ?? '-'}</td>
                <td>{r.invoiceDate?.slice(0, 10)}</td>
                <td>{r.dueDate?.slice(0, 10)}</td>
                <td><strong>{formatQuantity(r.amount, 2)}</strong> {r.currency}</td>
                <td>{statusBadge(r.status)}</td>
                <td>{r.paidDate ? r.paidDate.slice(0, 10) : '-'}</td>
                <td style={{ display: 'flex', gap: 4 }}>
                  {(r.status === 1 || r.status === 4) && (
                    <button onClick={() => markPaid(r)} disabled={updateMut.isPending} style={{ padding: '4px 8px', fontSize: 12, background: '#2e7d32', color: 'white', border: 'none', borderRadius: 3 }}>
                      {t('supplierInvoices.markPaid', { defaultValue: 'Mark paid' })}
                    </button>
                  )}
                  {(r.status === 1 || r.status === 4) && (
                    <button onClick={() => markCancelled(r)} disabled={updateMut.isPending} style={{ padding: '4px 8px', fontSize: 12 }}>
                      {t('supplierInvoices.cancel', { defaultValue: 'Cancel' })}
                    </button>
                  )}
                  <button onClick={() => remove(r.id)} disabled={deleteMut.isPending} style={{ padding: '4px 8px', fontSize: 12, color: '#c62828' }}>×</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default SupplierInvoices;
