import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi, masterDataApi, wmsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P7.5 — stock grouped by customer (Partner).
 *
 * Join path: InventoryBalance.MRN → MRNRegistry.CustomsDeclarationId
 * → CustomsDeclaration.PartnerId → Partner.
 * Non-MRN inventory rolls up under "— Non-LON / domestic —".
 */

type Balance = {
  id: string;
  itemId: string;
  mrn?: string | null;
  quantity: number;
  item?: { code: string; name: string } | null;
  uoM?: { code: string; name: string } | null;
};

type Registry = {
  mrn: string;
  customsDeclaration?: { partnerId?: string | null } | null;
};

type Partner = { id: string; code: string; name: string };

type Group = {
  partnerId: string;
  partnerName: string;
  totalQty: number;
  lines: Array<{ itemCode: string; itemName: string; mrn?: string | null; quantity: number; uom?: string }>;
};

const StockByCustomer: React.FC = () => {
  const { t } = useTranslation();
  const [groups, setGroups] = useState<Group[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const [invResp, regResp, prtResp] = await Promise.all([
          wmsApi.getInventory(),
          customsApi.getMRNRegistry(undefined, undefined),
          masterDataApi.getPartners(),
        ]);
        if (cancelled) return;
        const inv = (invResp.data as Balance[]) ?? [];
        const reg = (regResp.data as Registry[]) ?? [];
        const prt = (prtResp.data as Partner[]) ?? [];

        const regByMrn = new Map<string, string | null>();
        reg.forEach((r) => regByMrn.set(r.mrn, r.customsDeclaration?.partnerId ?? null));
        const prtById = new Map<string, Partner>();
        prt.forEach((p) => prtById.set(p.id, p));

        const bucket = new Map<string, Group>();
        inv.forEach((b) => {
          if (b.quantity <= 0) return;
          const partnerId = b.mrn ? regByMrn.get(b.mrn) ?? null : null;
          const key = partnerId ?? '__unassigned__';
          const partnerName = partnerId ? (prtById.get(partnerId)?.name ?? partnerId) : t('stockByCustomer.unassigned') as string;
          const entry = bucket.get(key) ?? { partnerId: key, partnerName, totalQty: 0, lines: [] };
          entry.totalQty += b.quantity;
          entry.lines.push({
            itemCode: b.item?.code ?? '',
            itemName: b.item?.name ?? '',
            mrn: b.mrn,
            quantity: b.quantity,
            uom: b.uoM?.code,
          });
          bucket.set(key, entry);
        });
        const list = Array.from(bucket.values()).sort((a, b) => b.totalQty - a.totalQty);
        setGroups(list);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [t]);

  const flatForExport = useMemo(
    () =>
      groups.flatMap((g) =>
        g.lines.map((l) => ({
          partner: g.partnerName,
          itemCode: l.itemCode,
          itemName: l.itemName,
          mrn: l.mrn ?? '',
          quantity: l.quantity,
          uom: l.uom ?? '',
        }))
      ),
    [groups]
  );

  function toggle(id: string) {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('stockByCustomer.title')}</h1>
      <p style={{ color: '#666' }}>{t('stockByCustomer.subtitle')}</p>

      <div style={{ display: 'flex', gap: 12, marginBottom: 12 }}>
        <span style={{ color: '#888', marginLeft: 'auto' }}>
          {t('stockByCustomer.groupCount', { count: groups.length })}
        </span>
        <button
          onClick={() =>
            exportToCsv(
              flatForExport,
              [
                { key: 'partner', label: t('stockByCustomer.customer') as string },
                { key: 'itemCode', label: t('common.code') as string },
                { key: 'itemName', label: t('common.name') as string },
                { key: 'mrn', label: 'MRN' },
                { key: 'quantity', label: t('common.quantity') as string, type: 'number' },
                { key: 'uom', label: 'UoM' },
              ],
              'stock-by-customer'
            )
          }
          disabled={flatForExport.length === 0}
          style={{ padding: '6px 12px' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      {loading && <div>{t('common.loading')}</div>}
      {!loading && groups.length === 0 && <div style={{ color: '#888' }}>{t('common.noData')}</div>}

      {!loading && groups.map((g) => (
        <div key={g.partnerId} style={{ border: '1px solid #eee', borderRadius: 4, marginBottom: 8 }}>
          <button
            onClick={() => toggle(g.partnerId)}
            style={{ width: '100%', padding: 10, background: '#f8f8f8', border: 'none', textAlign: 'left', display: 'flex', justifyContent: 'space-between', cursor: 'pointer' }}
          >
            <div>
              <span style={{ marginRight: 8 }}>{expanded.has(g.partnerId) ? '▼' : '▶'}</span>
              <strong>{g.partnerName}</strong>
              <span style={{ color: '#888', marginLeft: 8 }}>({g.lines.length} {t('stockByCustomer.itemsLabel')})</span>
            </div>
            <strong>{formatQuantity(g.totalQty)}</strong>
          </button>
          {expanded.has(g.partnerId) && (
            <table style={{ width: '100%' }}>
              <thead>
                <tr>
                  <th>{t('common.code')}</th>
                  <th>{t('common.name')}</th>
                  <th>MRN</th>
                  <th>{t('common.quantity')}</th>
                </tr>
              </thead>
              <tbody>
                {g.lines.map((l, i) => (
                  <tr key={i}>
                    <td><code>{l.itemCode}</code></td>
                    <td>{l.itemName}</td>
                    <td>{l.mrn ?? '-'}</td>
                    <td>{formatQuantity(l.quantity)} {l.uom}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      ))}
    </div>
  );
};

export default StockByCustomer;
