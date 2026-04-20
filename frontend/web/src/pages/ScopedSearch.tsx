import React, { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { customsApi, wmsApi, productionApi } from '../services/api';
import { translateError } from '../utils/translateError';
import { formatDate, formatQuantity } from '../utils/format';

/**
 * P7.9 — scoped search.
 *
 * One component, three routes (customs / warehouse / production). Hits only
 * the endpoints relevant to the scope and collates hits into a unified
 * result list with deep-link to the target detail page.
 *
 * Intentionally client-side fan-out over existing list endpoints — a true
 * backend search is a Phase 8+ deliverable.
 */

type Scope = 'customs' | 'warehouse' | 'production';

type Hit = {
  kind: string;
  title: string;
  subtitle?: string;
  detail?: string;
  href: string;
};

function norm(s: string | null | undefined): string {
  return (s ?? '').toString().toLowerCase();
}

function match(q: string, ...fields: Array<string | null | undefined>): boolean {
  if (!q) return false;
  const lq = q.toLowerCase();
  return fields.some((f) => norm(f).includes(lq));
}

interface Props {
  scope: Scope;
}

const ScopedSearch: React.FC<Props> = ({ scope }) => {
  const { t } = useTranslation();
  const [query, setQuery] = useState('');
  const [hits, setHits] = useState<Hit[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const titleKey = `scopedSearch.${scope}.title`;
  const subtitleKey = `scopedSearch.${scope}.subtitle`;

  useEffect(() => {
    if (query.trim().length < 2) {
      setHits([]);
      return;
    }
    let cancelled = false;
    const timer = setTimeout(async () => {
      setLoading(true);
      setError(null);
      try {
        const collected: Hit[] = [];

        if (scope === 'customs') {
          const [decls, mrns, auths] = await Promise.all([
            customsApi.getDeclarations(),
            customsApi.getMRNRegistry(),
            customsApi.getLONAuthorizations(true),
          ]);
          ((decls.data as any[]) ?? []).forEach((d) => {
            if (match(query, d.declarationNumber, d.mrn, d.partnerName, d.procedureCode)) {
              collected.push({
                kind: t('scopedSearch.kind.declaration') as string,
                title: d.declarationNumber,
                subtitle: d.mrn,
                detail: `${d.partnerName ?? ''} · ${formatDate(d.declarationDate)} · ${formatQuantity(d.totalCustomsValue ?? 0)} ${d.currency ?? ''}`,
                href: `/customs`,
              });
            }
          });
          ((mrns.data as any[]) ?? []).forEach((m) => {
            if (match(query, m.mrn)) {
              collected.push({
                kind: 'MRN',
                title: m.mrn,
                subtitle: m.isActive ? t('scopedSearch.mrnActive') as string : t('scopedSearch.mrnClosed') as string,
                detail: `Used ${formatQuantity(m.usedQuantity)} / ${formatQuantity(m.totalQuantity)}`,
                href: `/customs/deadlines`,
              });
            }
          });
          ((auths.data as any[]) ?? []).forEach((a) => {
            if (match(query, a.authorizationNumber, a.partnerName)) {
              collected.push({
                kind: t('scopedSearch.kind.authorization') as string,
                title: a.authorizationNumber,
                subtitle: a.partnerName,
                detail: `${a.status ?? ''} · ${formatDate(a.expiryDate)}`,
                href: `/customs/authorizations`,
              });
            }
          });
        }

        if (scope === 'warehouse') {
          const [receipts, shipments, inventory] = await Promise.all([
            wmsApi.getReceipts(1, 500),
            wmsApi.getShipments(1, 500),
            wmsApi.getInventory(),
          ]);
          ((receipts.data as any[]) ?? []).forEach((r) => {
            if (match(query, r.receiptNumber, r.purchaseOrderNumber, r.referenceNumber)) {
              collected.push({
                kind: t('scopedSearch.kind.receipt') as string,
                title: r.receiptNumber,
                detail: `${formatDate(r.receiptDate)} · ${r.lines?.length ?? 0} ${t('scopedSearch.lines')}`,
                href: `/inventory`,
              });
            }
          });
          ((shipments.data as any[]) ?? []).forEach((s) => {
            if (match(query, s.shipmentNumber, s.trackingNumber, s.salesOrderNumber)) {
              collected.push({
                kind: t('scopedSearch.kind.shipment') as string,
                title: s.shipmentNumber,
                subtitle: s.customerName,
                detail: formatDate(s.shipmentDate),
                href: `/warehouse/ready-to-ship`,
              });
            }
          });
          ((inventory.data as any[]) ?? []).forEach((i) => {
            if (match(query, i.batchNumber, i.mrn, i.item?.code, i.item?.name)) {
              collected.push({
                kind: t('scopedSearch.kind.inventory') as string,
                title: `${i.item?.code ?? ''} @ ${i.location?.code ?? ''}`,
                subtitle: `Batch ${i.batchNumber ?? '-'} / MRN ${i.mrn ?? '-'}`,
                detail: `${formatQuantity(i.quantity)} ${i.uoM?.code ?? ''}`,
                href: `/inventory`,
              });
            }
          });
        }

        if (scope === 'production') {
          const orders = await productionApi.getOrders();
          ((orders.data as any[]) ?? []).forEach((o) => {
            if (match(query, o.orderNumber, o.customerOrderNumber, o.item?.code, o.item?.name)) {
              collected.push({
                kind: t('scopedSearch.kind.productionOrder') as string,
                title: o.orderNumber,
                subtitle: o.item?.name ?? o.item?.code ?? '',
                detail: `${o.status ?? ''} · ${formatQuantity(o.orderQuantity ?? 0)}`,
                href: `/production`,
              });
            }
          });
        }

        if (!cancelled) setHits(collected.slice(0, 200));
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    }, 300);
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [query, scope, t]);

  const grouped = useMemo(() => {
    const map = new Map<string, Hit[]>();
    hits.forEach((h) => {
      const list = map.get(h.kind) ?? [];
      list.push(h);
      map.set(h.kind, list);
    });
    return Array.from(map.entries());
  }, [hits]);

  return (
    <div style={{ padding: 16, maxWidth: 900 }}>
      <h1>{t(titleKey)}</h1>
      <p style={{ color: '#666' }}>{t(subtitleKey)}</p>

      <input
        type="text"
        autoFocus
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        placeholder={t('scopedSearch.placeholder') as string}
        style={{ width: '100%', padding: 10, fontSize: 16, border: '1px solid #ccc', borderRadius: 4, marginBottom: 16 }}
      />

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4 }}>{error}</div>}
      {loading && <div style={{ color: '#888' }}>{t('common.loading')}</div>}
      {!loading && query.trim().length < 2 && (
        <div style={{ color: '#888' }}>{t('scopedSearch.typePrompt')}</div>
      )}
      {!loading && query.trim().length >= 2 && hits.length === 0 && (
        <div style={{ color: '#888' }}>{t('scopedSearch.noResults')}</div>
      )}

      {grouped.map(([kind, items]) => (
        <div key={kind} style={{ marginBottom: 20 }}>
          <h3 style={{ textTransform: 'uppercase', color: '#666', fontSize: 12, marginBottom: 6 }}>
            {kind} ({items.length})
          </h3>
          {items.map((h, i) => (
            <Link
              key={i}
              to={h.href}
              style={{
                display: 'block',
                padding: 10,
                border: '1px solid #eee',
                borderRadius: 4,
                marginBottom: 4,
                textDecoration: 'none',
                color: 'inherit',
              }}
            >
              <strong>{h.title}</strong>
              {h.subtitle && <span style={{ marginLeft: 8, color: '#666' }}>{h.subtitle}</span>}
              {h.detail && <div style={{ fontSize: 12, color: '#888' }}>{h.detail}</div>}
            </Link>
          ))}
        </div>
      ))}
    </div>
  );
};

export default ScopedSearch;
