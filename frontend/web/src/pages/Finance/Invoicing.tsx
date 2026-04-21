import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { financeApi, masterDataApi, productionApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P12.2 — invoicing MVP.
 *
 * Split view: list of invoices (filterable by status / partner / period) +
 * right-side detail with line items. Actions:
 *   • Generate from PO: picks a completed production order, resolves rate
 *     via active contract + rate-card entry, creates Draft invoice.
 *   • Issue: assigns sequential INV-{year}-{seq} number, flips to Issued.
 *   • Mark paid / Cancel.
 */

type InvoiceLine = {
  id: string;
  lineNumber: number;
  description: string;
  itemId: string | null;
  itemCode: string | null;
  relatedProductionOrderId: string | null;
  relatedProductionOrderNumber: string | null;
  relatedShipmentId: string | null;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
};

type Invoice = {
  id: string;
  number: string;
  partnerId: string;
  partnerName: string;
  contractId: string | null;
  contractNumber: string | null;
  issueDate: string;
  dueDate: string;
  currency: string;
  subTotal: number;
  totalAmount: number;
  status: number;
  issuedAt: string | null;
  paidAt: string | null;
  notes: string | null;
  lines: InvoiceLine[];
};

type Partner = { id: string; code: string; name: string };
type ProductionOrder = {
  id: string;
  orderNumber: string;
  itemId: string;
  customerPartnerId?: string | null;
  producedQuantity: number;
  status: number;
};

const STATUSES = [
  { value: 1, key: 'draft', bg: '#e0e0e0', color: '#616161' },
  { value: 2, key: 'issued', bg: '#bbdefb', color: '#1565c0' },
  { value: 3, key: 'paid', bg: '#c8e6c9', color: '#2e7d32' },
  { value: 4, key: 'cancelled', bg: '#ffcdd2', color: '#c62828' },
];

const Invoicing: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Invoice[]>([]);
  const [detail, setDetail] = useState<Invoice | null>(null);
  const [partners, setPartners] = useState<Partner[]>([]);
  const [orders, setOrders] = useState<ProductionOrder[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState<number | ''>('');
  const [partnerFilter, setPartnerFilter] = useState('');
  const [search, setSearch] = useState('');

  // Generate-from-PO draft
  const [genOpen, setGenOpen] = useState(false);
  const [genPo, setGenPo] = useState('');
  const [genOverride, setGenOverride] = useState('');
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [invs, ps] = await Promise.all([
        financeApi.getInvoices({
          status: statusFilter === '' ? undefined : Number(statusFilter),
          partnerId: partnerFilter || undefined,
        }),
        masterDataApi.getPartners(),
      ]);
      const env = invs.data as { data?: Invoice[] };
      setRows(env?.data ?? (invs.data as Invoice[]) ?? []);
      setPartners((ps.data as Partner[]) ?? []);
    } catch (err) {
      setError(translateError(err));
    } finally {
      setLoading(false);
    }
  }, [statusFilter, partnerFilter]);

  useEffect(() => { load(); }, [load]);

  const loadDetail = useCallback(async (id: string) => {
    try {
      const resp = await financeApi.getInvoice(id);
      const env = resp.data as { data?: Invoice };
      setDetail(env?.data ?? (resp.data as Invoice));
    } catch (err) {
      setError(translateError(err));
    }
  }, []);

  const loadPOs = useCallback(async () => {
    try {
      const resp = await productionApi.getOrders();
      const list = (resp.data as any[]) ?? [];
      // Only completed POs with a customer + produced qty.
      setOrders(list
        .map((po) => ({
          id: po.id,
          orderNumber: po.orderNumber,
          itemId: po.itemId,
          customerPartnerId: po.customerPartnerId,
          producedQuantity: po.producedQuantity ?? 0,
          status: po.status,
        }))
        .filter((po) => po.customerPartnerId && po.producedQuantity > 0));
    } catch (err) {
      setError(translateError(err));
    }
  }, []);

  const generateFromPo = async () => {
    if (!genPo) return;
    setSaving(true);
    try {
      await financeApi.generateFromPo({
        productionOrderId: genPo,
        contractId: null,
        overrideUnitPrice: genOverride ? Number(genOverride) : null,
        issueDate: null,
      });
      setGenOpen(false);
      setGenPo(''); setGenOverride('');
      await load();
    } catch (err) {
      setError(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  const issue = async () => {
    if (!detail) return;
    setSaving(true);
    try {
      await financeApi.issue(detail.id);
      await load();
      await loadDetail(detail.id);
    } catch (err) {
      setError(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  const markPaid = async () => {
    if (!detail) return;
    setSaving(true);
    try {
      await financeApi.markPaid(detail.id);
      await load();
      await loadDetail(detail.id);
    } catch (err) {
      setError(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  const cancel = async () => {
    if (!detail) return;
    const reason = window.prompt(t('finance.invoicing.cancelReason') ?? '');
    if (reason === null) return;
    setSaving(true);
    try {
      await financeApi.cancel(detail.id, reason);
      await load();
      await loadDetail(detail.id);
    } catch (err) {
      setError(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  const removeLine = async (lineId: string) => {
    if (!detail) return;
    try {
      await financeApi.removeLine(detail.id, lineId);
      await loadDetail(detail.id);
    } catch (err) {
      setError(translateError(err));
    }
  };

  const filteredRows = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter(
      (r) =>
        r.number.toLowerCase().includes(q) ||
        (r.partnerName ?? '').toLowerCase().includes(q) ||
        (r.contractNumber ?? '').toLowerCase().includes(q)
    );
  }, [rows, search]);

  const summary = useMemo(() => {
    const outstanding = filteredRows
      .filter((r) => r.status === 2)
      .reduce((acc, r) => acc + r.totalAmount, 0);
    const paid = filteredRows
      .filter((r) => r.status === 3)
      .reduce((acc, r) => acc + r.totalAmount, 0);
    const drafts = filteredRows.filter((r) => r.status === 1).length;
    return { outstanding, paid, drafts };
  }, [filteredRows]);

  const exportCsv = () => {
    exportToCsv(
      filteredRows,
      [
        { key: 'number', label: t('finance.invoicing.number') },
        { key: 'partnerName', label: t('finance.invoicing.partner') },
        {
          key: 'status',
          label: t('finance.invoicing.status'),
          get: (r) => STATUSES.find((s) => s.value === r.status)?.key ?? String(r.status),
        },
        { key: 'issueDate', label: t('finance.invoicing.issued'), type: 'date' },
        { key: 'dueDate', label: t('finance.invoicing.due'), type: 'date' },
        { key: 'totalAmount', label: t('finance.invoicing.total'), type: 'number', decimals: 2 },
        { key: 'currency', label: t('finance.invoicing.currency') ?? 'Currency' },
      ],
      'invoices',
    );
  };

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('finance.invoicing.title')}</h1>
      <p style={{ color: '#666' }}>{t('finance.invoicing.subtitle')}</p>

      {error && (
        <div style={{ padding: 10, background: '#ffebee', color: '#c62828', marginBottom: 12, borderRadius: 4 }}>
          {error}
          <button onClick={() => setError(null)} style={{ marginLeft: 8 }}>×</button>
        </div>
      )}

      <div style={{ display: 'flex', gap: 16, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, flexWrap: 'wrap' }}>
        <div><small>{t('finance.invoicing.outstanding')}</small><div style={{ fontWeight: 600, color: '#1565c0' }}>{formatQuantity(summary.outstanding, 2)}</div></div>
        <div><small>{t('finance.invoicing.paidTotal')}</small><div style={{ fontWeight: 600, color: '#2e7d32' }}>{formatQuantity(summary.paid, 2)}</div></div>
        <div><small>{t('finance.invoicing.drafts')}</small><div style={{ fontWeight: 600 }}>{summary.drafts}</div></div>
      </div>

      <div style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'center', flexWrap: 'wrap' }}>
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('finance.invoicing.searchPlaceholder') as string}
          style={{ padding: 6, minWidth: 220 }}
        />
        <label>
          {t('finance.invoicing.status')}:{' '}
          <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value === '' ? '' : Number(e.target.value))}>
            <option value="">{t('common.all')}</option>
            {STATUSES.map((s) => (
              <option key={s.value} value={s.value}>{t(`finance.invoicing.statuses.${s.key}`)}</option>
            ))}
          </select>
        </label>
        <label>
          {t('finance.invoicing.partner')}:{' '}
          <select value={partnerFilter} onChange={(e) => setPartnerFilter(e.target.value)}>
            <option value="">{t('common.all')}</option>
            {partners.map((p) => (
              <option key={p.id} value={p.id}>{p.code} — {p.name}</option>
            ))}
          </select>
        </label>
        <button onClick={exportCsv}>{t('common.exportCsv')}</button>
        <button onClick={async () => { await loadPOs(); setGenOpen(!genOpen); }}>
          {genOpen ? t('common.cancel') : t('finance.invoicing.generateFromPo')}
        </button>
      </div>

      {genOpen && (
        <div style={{ padding: 12, background: '#f0f7ff', borderRadius: 4, marginBottom: 12 }}>
          <h3 style={{ marginTop: 0 }}>{t('finance.invoicing.generateFromPo')}</h3>
          <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr', gap: 8, alignItems: 'end' }}>
            <label>{t('finance.invoicing.productionOrder')}
              <select value={genPo} onChange={(e) => setGenPo(e.target.value)}>
                <option value="">— {t('common.select')} —</option>
                {orders.map((po) => (
                  <option key={po.id} value={po.id}>
                    {po.orderNumber} — {po.producedQuantity}
                  </option>
                ))}
              </select>
            </label>
            <label>{t('finance.invoicing.overrideUnitPrice')}
              <input type="number" step="0.0001" value={genOverride} onChange={(e) => setGenOverride(e.target.value)} placeholder={t('finance.invoicing.useContractRate') ?? ''} />
            </label>
            <button onClick={generateFromPo} disabled={saving || !genPo}>
              {saving ? t('common.saving') : t('common.generate')}
            </button>
          </div>
          <div style={{ fontSize: 12, color: '#666', marginTop: 6 }}>
            {t('finance.invoicing.generateHint')}
          </div>
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: '2fr 3fr', gap: 16 }}>
        <div>
          <h3>{t('finance.invoicing.listTitle')} ({filteredRows.length})</h3>
          {loading ? <div>{t('common.loading')}</div> : (
            <table style={{ width: '100%', fontSize: 13 }}>
              <thead>
                <tr>
                  <th>{t('finance.invoicing.number')}</th>
                  <th>{t('finance.invoicing.partner')}</th>
                  <th>{t('finance.invoicing.status')}</th>
                  <th style={{ textAlign: 'right' }}>{t('finance.invoicing.total')}</th>
                  <th>{t('finance.invoicing.due')}</th>
                </tr>
              </thead>
              <tbody>
                {filteredRows.map((r) => {
                  const s = STATUSES.find((x) => x.value === r.status);
                  return (
                    <tr key={r.id}
                        onClick={() => loadDetail(r.id)}
                        style={{ background: detail?.id === r.id ? '#e3f2fd' : undefined, cursor: 'pointer' }}>
                      <td>{r.number}</td>
                      <td>{r.partnerName}</td>
                      <td>
                        <span style={{ background: s?.bg, color: s?.color, padding: '2px 6px', borderRadius: 3, fontSize: 12 }}>
                          {t(`finance.invoicing.statuses.${s?.key ?? 'unknown'}`)}
                        </span>
                      </td>
                      <td style={{ textAlign: 'right' }}>{formatQuantity(r.totalAmount, 2)} {r.currency}</td>
                      <td style={{ fontSize: 12, color: '#666' }}>{formatDate(r.dueDate)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>

        <div>
          {!detail ? (
            <div style={{ color: '#999', padding: 24, textAlign: 'center' }}>
              {t('finance.invoicing.selectInvoice')}
            </div>
          ) : (
            <>
              <div style={{ padding: 12, background: '#f9f9f9', borderRadius: 4, marginBottom: 12 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <h3 style={{ margin: 0 }}>{detail.number}</h3>
                  <div>
                    {detail.status === 1 && (
                      <button onClick={issue} disabled={saving} style={{ background: '#1565c0', color: '#fff' }}>
                        {t('finance.invoicing.issue')}
                      </button>
                    )}
                    {detail.status === 2 && (
                      <>
                        <button onClick={markPaid} disabled={saving} style={{ background: '#2e7d32', color: '#fff', marginRight: 4 }}>
                          {t('finance.invoicing.markPaid')}
                        </button>
                        <button onClick={cancel} disabled={saving} style={{ color: '#c62828' }}>
                          {t('finance.invoicing.cancel')}
                        </button>
                      </>
                    )}
                  </div>
                </div>
                <div style={{ fontSize: 13, color: '#555', marginTop: 4 }}>
                  {detail.partnerName}
                  {detail.contractNumber && <> · {t('finance.invoicing.contract')} {detail.contractNumber}</>}<br/>
                  {t('finance.invoicing.issued')}: {formatDate(detail.issueDate)} · {t('finance.invoicing.due')}: {formatDate(detail.dueDate)}<br/>
                  <strong>{t('finance.invoicing.total')}: {formatQuantity(detail.totalAmount, 2)} {detail.currency}</strong>
                </div>
                {detail.notes && <div style={{ fontSize: 12, color: '#666', marginTop: 6, whiteSpace: 'pre-wrap' }}>{detail.notes}</div>}
              </div>

              <h4>{t('finance.invoicing.lines')} ({detail.lines.length})</h4>
              <table style={{ width: '100%', fontSize: 13 }}>
                <thead>
                  <tr>
                    <th>#</th>
                    <th>{t('finance.invoicing.description')}</th>
                    <th>{t('finance.invoicing.reference')}</th>
                    <th style={{ textAlign: 'right' }}>{t('finance.invoicing.qty')}</th>
                    <th style={{ textAlign: 'right' }}>{t('finance.invoicing.unitPrice')}</th>
                    <th style={{ textAlign: 'right' }}>{t('finance.invoicing.total')}</th>
                    {detail.status === 1 && <th></th>}
                  </tr>
                </thead>
                <tbody>
                  {detail.lines.map((l) => (
                    <tr key={l.id}>
                      <td>{l.lineNumber}</td>
                      <td>{l.description}</td>
                      <td style={{ fontSize: 12, color: '#666' }}>
                        {l.relatedProductionOrderNumber ?? l.itemCode ?? '—'}
                      </td>
                      <td style={{ textAlign: 'right' }}>{formatQuantity(l.quantity, 2)}</td>
                      <td style={{ textAlign: 'right' }}>{l.unitPrice.toFixed(4)}</td>
                      <td style={{ textAlign: 'right' }}>{formatQuantity(l.lineTotal, 2)}</td>
                      {detail.status === 1 && (
                        <td style={{ textAlign: 'right' }}>
                          <button onClick={() => removeLine(l.id)} style={{ color: '#c62828' }}>×</button>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr>
                    <td colSpan={5} style={{ textAlign: 'right', fontWeight: 600 }}>{t('finance.invoicing.subtotal')}:</td>
                    <td style={{ textAlign: 'right', fontWeight: 600 }}>{formatQuantity(detail.subTotal, 2)} {detail.currency}</td>
                    {detail.status === 1 && <td />}
                  </tr>
                </tfoot>
              </table>
            </>
          )}
        </div>
      </div>
    </div>
  );
};

export default Invoicing;
