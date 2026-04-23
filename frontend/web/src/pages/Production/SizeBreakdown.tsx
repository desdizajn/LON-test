import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { api } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';

/**
 * P15.16.1 — NormativiVelicini per-size BOM editor.
 *
 * Legacy ELON had this as a subform on frmNormativi. We surface it as a
 * standalone page at /production/size-breakdown: operator picks a
 * production order + material, then edits the size rows inline. On save,
 * backend enforces Σ size qty == PO.OrderQuantity and recomputes parent
 * RequiredQuantity as Σ(qty × normativ).
 *
 * When HasSizeBreakdown=false, the page shows the current flat normativ
 * and an "Enable sizes" button that seeds a single row at full PO qty.
 */

type POMaterialSummary = {
  materialId: string;
  productionOrderId: string;
  orderNumber: string;
  itemCode: string;
  itemName: string;
  requiredQuantity: number;
  issuedQuantity: number;
  hasSizeBreakdown: boolean;
  poQuantity: number;
  uoMCode: string;
};

type SizeRow = {
  id?: string;
  sizeOrdinal: number;
  sizeLabel: string;
  quantity: number;
  normativPerUnit: number;
  totalMaterialQuantity?: number;
};

type MaterialDetail = {
  id: string;
  productionOrderId: string;
  itemId: string;
  hasSizeBreakdown: boolean;
  requiredQuantity: number;
  issuedQuantity: number;
  poQuantity: number;
  sizes: SizeRow[];
};

const SizeBreakdown: React.FC = () => {
  const { t } = useTranslation();
  const [orders, setOrders] = useState<any[]>([]);
  const [selectedOrderId, setSelectedOrderId] = useState<string>('');
  const [materials, setMaterials] = useState<POMaterialSummary[]>([]);
  const [selectedMaterialId, setSelectedMaterialId] = useState<string>('');
  const [detail, setDetail] = useState<MaterialDetail | null>(null);
  const [rows, setRows] = useState<SizeRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    api
      .get('/Production/orders')
      .then((r) => setOrders((r.data ?? []).filter((o: any) => o.status !== 6)))
      .catch((err) => toast.error(translateError(err)));
  }, []);

  useEffect(() => {
    if (!selectedOrderId) {
      setMaterials([]);
      return;
    }
    setLoading(true);
    api
      .get(`/Production/orders/${selectedOrderId}`)
      .then((r) => {
        const po = r.data;
        const mats: POMaterialSummary[] = (po.materials ?? []).map((m: any) => ({
          materialId: m.id,
          productionOrderId: po.id,
          orderNumber: po.orderNumber,
          itemCode: m.item?.code ?? '',
          itemName: m.item?.name ?? '',
          requiredQuantity: m.requiredQuantity ?? 0,
          issuedQuantity: m.issuedQuantity ?? 0,
          hasSizeBreakdown: !!m.hasSizeBreakdown,
          poQuantity: po.orderQuantity ?? 0,
          uoMCode: m.uoM?.code ?? '',
        }));
        setMaterials(mats);
      })
      .catch((err) => toast.error(translateError(err)))
      .finally(() => setLoading(false));
  }, [selectedOrderId]);

  useEffect(() => {
    if (!selectedMaterialId) {
      setDetail(null);
      setRows([]);
      return;
    }
    setLoading(true);
    api
      .get(`/Production/materials/${selectedMaterialId}/sizes`)
      .then((r) => {
        const d = r.data as MaterialDetail;
        setDetail(d);
        setRows(d.sizes && d.sizes.length > 0
          ? d.sizes
          : [{ sizeOrdinal: 1, sizeLabel: '', quantity: d.poQuantity, normativPerUnit: 0 }]
        );
      })
      .catch((err) => toast.error(translateError(err)))
      .finally(() => setLoading(false));
  }, [selectedMaterialId]);

  const updateRow = (i: number, patch: Partial<SizeRow>) =>
    setRows((r) => r.map((x, idx) => (idx === i ? { ...x, ...patch } : x)));

  const addRow = () =>
    setRows((r) => [
      ...r,
      {
        sizeOrdinal: (r[r.length - 1]?.sizeOrdinal ?? 0) + 1,
        sizeLabel: '',
        quantity: 0,
        normativPerUnit: r[0]?.normativPerUnit ?? 0,
      },
    ]);

  const removeRow = (i: number) =>
    setRows((r) => (r.length === 1 ? r : r.filter((_, idx) => idx !== i)));

  const distributeRemaining = () => {
    if (!detail || rows.length === 0) return;
    const sum = rows.reduce((s, r) => s + (r.quantity || 0), 0);
    const delta = detail.poQuantity - sum + rows[rows.length - 1].quantity;
    updateRow(rows.length - 1, { quantity: Math.max(0, delta) });
  };

  const save = async () => {
    if (!selectedMaterialId) return;
    setSaving(true);
    try {
      await api.post(`/Production/materials/${selectedMaterialId}/sizes`,
        rows.map((r) => ({
          sizeOrdinal: r.sizeOrdinal,
          sizeLabel: r.sizeLabel,
          quantity: r.quantity,
          normativPerUnit: r.normativPerUnit,
        }))
      );
      toast.success(t('sizeBreakdown.saveSuccess', 'Распределбата по големини е зачувана.'));
      // Re-load
      const r = await api.get(`/Production/materials/${selectedMaterialId}/sizes`);
      setDetail(r.data);
      setRows(r.data.sizes || []);
    } catch (err) {
      toast.error(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  const clear = async () => {
    if (!selectedMaterialId) return;
    if (!window.confirm(t('sizeBreakdown.confirmClear', 'Избриши ја распределбата по големини?') as string)) return;
    setSaving(true);
    try {
      await api.delete(`/Production/materials/${selectedMaterialId}/sizes`);
      toast.success(t('sizeBreakdown.cleared', 'Распределбата е избришана.'));
      const r = await api.get(`/Production/materials/${selectedMaterialId}/sizes`);
      setDetail(r.data);
      setRows([{ sizeOrdinal: 1, sizeLabel: '', quantity: r.data.poQuantity, normativPerUnit: 0 }]);
    } catch (err) {
      toast.error(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  const sumQty = rows.reduce((s, r) => s + (r.quantity || 0), 0);
  const sumMaterial = rows.reduce((s, r) => s + (r.quantity || 0) * (r.normativPerUnit || 0), 0);
  const weightedAvgNorm = sumQty > 0 ? sumMaterial / sumQty : 0;
  const poQty = detail?.poQuantity ?? 0;
  const qtyMismatch = Math.abs(sumQty - poQty) > 0.01;

  return (
    <div style={{ padding: 20 }}>
      <div style={{ marginBottom: 20 }}>
        <h1 style={{ margin: 0 }}>{t('sizeBreakdown.title', 'Распределба по големини (NormativiVelicini)')}</h1>
        <div style={{ color: '#666', fontSize: 13, marginTop: 5 }}>
          {t(
            'sizeBreakdown.subtitle',
            'Per-size нормативи: различни големини (S/M/L/XXL/40/42) може да консумираат различно количество материјал. Parent RequiredQuantity се пресметува како Σ(qty × normativ).'
          )}
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 15, marginBottom: 20 }}>
        <label>
          {t('sizeBreakdown.pickOrder', 'Избери производствен налог')}
          <select
            value={selectedOrderId}
            onChange={(e) => {
              setSelectedOrderId(e.target.value);
              setSelectedMaterialId('');
            }}
            style={{ width: '100%', padding: 8, marginTop: 4 }}
          >
            <option value="">-- {t('common.pick', 'избери')} --</option>
            {orders.map((o) => (
              <option key={o.id} value={o.id}>
                {o.orderNumber} · {o.item?.code} · {formatQuantity(o.orderQuantity)}
              </option>
            ))}
          </select>
        </label>
        <label>
          {t('sizeBreakdown.pickMaterial', 'Избери материјал')}
          <select
            value={selectedMaterialId}
            onChange={(e) => setSelectedMaterialId(e.target.value)}
            disabled={!selectedOrderId || materials.length === 0}
            style={{ width: '100%', padding: 8, marginTop: 4 }}
          >
            <option value="">-- {t('common.pick', 'избери')} --</option>
            {materials.map((m) => (
              <option key={m.materialId} value={m.materialId}>
                {m.itemCode} · req {formatQuantity(m.requiredQuantity)} {m.uoMCode}
                {m.hasSizeBreakdown ? ' · ✓ sizes' : ''}
              </option>
            ))}
          </select>
        </label>
      </div>

      {loading && <div className="loading">{t('common.loading')}</div>}

      {detail && !loading && (
        <section>
          <div style={{ background: '#f5f5f5', padding: 12, borderRadius: 6, marginBottom: 15, fontSize: 13 }}>
            <div><strong>{t('sizeBreakdown.poQuantity', 'PO количина')}:</strong> {formatQuantity(detail.poQuantity)}</div>
            <div><strong>{t('sizeBreakdown.requiredQty', 'Моментално RequiredQuantity')}:</strong> {formatQuantity(detail.requiredQuantity)}</div>
            <div><strong>{t('sizeBreakdown.issuedQty', 'Веќе издадено')}:</strong> {formatQuantity(detail.issuedQuantity)}</div>
            <div style={{ marginTop: 5 }}>
              <strong>{t('sizeBreakdown.hasSizes', 'Активна распределба')}:</strong>{' '}
              {detail.hasSizeBreakdown ? '✓' : '—'}
            </div>
          </div>

          <table className="data-table">
            <thead>
              <tr>
                <th>#</th>
                <th>{t('sizeBreakdown.col.label', 'Ознака (S / M / L / 40)')}</th>
                <th style={{ textAlign: 'right' }}>{t('sizeBreakdown.col.qty', 'Количина FG')}</th>
                <th style={{ textAlign: 'right' }}>{t('sizeBreakdown.col.norm', 'Нормативи / парче')}</th>
                <th style={{ textAlign: 'right' }}>{t('sizeBreakdown.col.material', 'Вкупен материјал')}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r, i) => (
                <tr key={i}>
                  <td style={{ textAlign: 'center' }}>
                    <input
                      type="number"
                      value={r.sizeOrdinal}
                      onChange={(e) => updateRow(i, { sizeOrdinal: parseInt(e.target.value) || 1 })}
                      style={{ width: 40, padding: 4 }}
                    />
                  </td>
                  <td>
                    <input
                      value={r.sizeLabel}
                      onChange={(e) => updateRow(i, { sizeLabel: e.target.value })}
                      placeholder="S / M / L / XXL / 40"
                      style={{ width: '100%', padding: 4 }}
                    />
                  </td>
                  <td style={{ textAlign: 'right' }}>
                    <input
                      type="number"
                      step="0.0001"
                      value={r.quantity}
                      onChange={(e) => updateRow(i, { quantity: parseFloat(e.target.value) || 0 })}
                      style={{ width: 120, padding: 4, textAlign: 'right', fontFamily: 'monospace' }}
                    />
                  </td>
                  <td style={{ textAlign: 'right' }}>
                    <input
                      type="number"
                      step="0.000001"
                      value={r.normativPerUnit}
                      onChange={(e) => updateRow(i, { normativPerUnit: parseFloat(e.target.value) || 0 })}
                      style={{ width: 120, padding: 4, textAlign: 'right', fontFamily: 'monospace' }}
                    />
                  </td>
                  <td style={{ textAlign: 'right', fontFamily: 'monospace' }}>
                    {formatQuantity((r.quantity || 0) * (r.normativPerUnit || 0), 4)}
                  </td>
                  <td>
                    <button
                      className="btn btn-sm btn-outline"
                      onClick={() => removeRow(i)}
                      disabled={rows.length === 1}
                    >
                      ×
                    </button>
                  </td>
                </tr>
              ))}
              <tr style={{ background: '#fafafa', fontWeight: 600 }}>
                <td colSpan={2} style={{ textAlign: 'right' }}>Σ</td>
                <td style={{ textAlign: 'right', fontFamily: 'monospace', color: qtyMismatch ? '#b00020' : '#155724' }}>
                  {formatQuantity(sumQty)}
                  {qtyMismatch && <span style={{ fontSize: 11, marginLeft: 6 }}>(≠ {formatQuantity(poQty)})</span>}
                </td>
                <td style={{ textAlign: 'right', fontFamily: 'monospace', fontSize: 12, color: '#666' }}>
                  avg {formatQuantity(weightedAvgNorm, 4)}
                </td>
                <td style={{ textAlign: 'right', fontFamily: 'monospace' }}>
                  {formatQuantity(sumMaterial, 4)}
                </td>
                <td></td>
              </tr>
            </tbody>
          </table>

          <div style={{ display: 'flex', gap: 10, marginTop: 15 }}>
            <button className="btn btn-sm btn-outline" onClick={addRow}>
              + {t('sizeBreakdown.addSize', 'Додади големина')}
            </button>
            <button className="btn btn-sm btn-outline" onClick={distributeRemaining}>
              {t('sizeBreakdown.fillRemaining', 'Стави остаток во последна')}
            </button>
            <div style={{ flex: 1 }}></div>
            {detail.hasSizeBreakdown && (
              <button className="btn btn-outline" onClick={clear} disabled={saving}>
                {t('sizeBreakdown.clearAll', 'Избриши распределба')}
              </button>
            )}
            <button
              className="btn btn-primary"
              onClick={save}
              disabled={saving || qtyMismatch || rows.some((r) => !r.sizeLabel)}
            >
              {saving ? t('common.saving', 'Се зачувува...') : t('common.save', 'Зачувај')}
            </button>
          </div>
          {qtyMismatch && (
            <div style={{ marginTop: 10, padding: 10, background: '#f8d7da', borderRadius: 4, color: '#721c24', fontSize: 13 }}>
              ⚠️ {t('sizeBreakdown.qtyMismatch',
                'Σ количина мора да е еднаква на PO.OrderQuantity ({{po}}). Сега имаш {{sum}}.',
                { po: formatQuantity(poQty), sum: formatQuantity(sumQty) })}
            </div>
          )}
        </section>
      )}
    </div>
  );
};

export default SizeBreakdown;
