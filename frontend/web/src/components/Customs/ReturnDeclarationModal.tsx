import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi, masterDataApi } from '../../services/api';

/**
 * P2.6b — Return declaration (procedure 6121). Reverses a prior EX: the
 * backend walks Exported → Imported/InProduction balances by FEFO of the
 * original discharge, decrements MRN.DischargedQuantity, re-debits the
 * guarantee ledger proportionally (mirror of P2.6a credit), and re-intakes
 * FG inventory. Shape is identical to Export; the caller-supplied
 * ReturnTarget on each line picks Imported or InProduction as the
 * destination bucket.
 */

type Item = { id: string; code: string; name: string };
type Uom = { id: string; code: string };
type Procedure = { id: string; code: string; name: string };
type Partner = { id: string; code: string; name: string };

type LineInput = {
  itemId: string;
  tariffCode: string;
  quantity: number;
  uoMId: string;
  customsValue: number;
  countryOfOrigin: string;
  netWeight: number;
  grossWeight: number;
  batchNumber: string;
  sourceMRN: string;
  returnQuantity: number;
  returnTarget: 'Imported' | 'InProduction';
};

interface Props {
  onClose: () => void;
  onSuccess: () => void;
}

const newLine = (): LineInput => ({
  itemId: '',
  tariffCode: '',
  quantity: 0,
  uoMId: '',
  customsValue: 0,
  countryOfOrigin: '',
  netWeight: 0,
  grossWeight: 0,
  batchNumber: '',
  sourceMRN: '',
  returnQuantity: 0,
  returnTarget: 'Imported',
});

const ReturnDeclarationModal: React.FC<Props> = ({ onClose, onSuccess }) => {
  const { t } = useTranslation();
  const today = new Date().toISOString().slice(0, 10);

  const [items, setItems] = useState<Item[]>([]);
  const [uoms, setUoms] = useState<Uom[]>([]);
  const [procedures, setProcedures] = useState<Procedure[]>([]);
  const [partners, setPartners] = useState<Partner[]>([]);

  const [declarationNumber, setDeclarationNumber] = useState(
    `RET-${new Date().toISOString().slice(0, 10).replace(/-/g, '')}-${Math.floor(Math.random() * 9999)
      .toString()
      .padStart(4, '0')}`,
  );
  const [mrn, setMrn] = useState('');
  const [declarationDate, setDeclarationDate] = useState(today);
  const [procedureId, setProcedureId] = useState('');
  const [partnerId, setPartnerId] = useState('');
  const [currency, setCurrency] = useState('EUR');
  const [totalCustomsValue, setTotalCustomsValue] = useState<number>(0);
  const [specialRemarks, setSpecialRemarks] = useState('');

  const [lines, setLines] = useState<LineInput[]>([newLine()]);

  const [saving, setSaving] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    masterDataApi.getItems().then((r) => {
      const list = r.data?.data ?? r.data ?? [];
      setItems(Array.isArray(list) ? list : []);
    });
    masterDataApi.getUoM().then((r: any) => {
      const list = r.data?.data ?? r.data ?? [];
      setUoms(Array.isArray(list) ? list : []);
    });
    customsApi.getProcedures().then((r) => {
      const list = r.data?.data ?? r.data ?? [];
      const all: Procedure[] = Array.isArray(list) ? list : [];
      setProcedures(all);
      const ret = all.find((p) => p.code === '6121');
      if (ret) setProcedureId(ret.id);
    });
    masterDataApi.getPartners().then((r) => {
      const list = r.data?.data ?? r.data ?? [];
      setPartners(Array.isArray(list) ? list : []);
    });
  }, []);

  const updateLine = (i: number, patch: Partial<LineInput>) => {
    setLines(lines.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));
  };

  const submit = async () => {
    setErr(null);
    setSaving(true);
    try {
      const payload = {
        declarationNumber,
        mrn: mrn || null,
        declarationDate,
        customsProcedureId: procedureId,
        partnerId: partnerId || null,
        currency,
        totalCustomsValue: Number(totalCustomsValue) || 0,
        specialRemarks: specialRemarks || null,
        lines: lines.map((l) => ({
          itemId: l.itemId,
          tariffCode: l.tariffCode || null,
          quantity: Number(l.quantity) || 0,
          uoMId: l.uoMId,
          customsValue: Number(l.customsValue) || 0,
          countryOfOrigin: l.countryOfOrigin || null,
          netWeight: Number(l.netWeight) || null,
          grossWeight: Number(l.grossWeight) || null,
          batchNumber: l.batchNumber,
          sourceMRN: l.sourceMRN,
          returnQuantity: Number(l.returnQuantity) || 0,
          returnTarget: l.returnTarget,
        })),
      };
      const r = await customsApi.createReturnDeclaration(payload);
      if (r.data?.isSuccess === false) {
        setErr(r.data?.errorMessage || 'Failed');
      } else {
        onSuccess();
      }
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || e?.message || 'Failed');
    } finally {
      setSaving(false);
    }
  };

  const hasLine =
    lines.length > 0 &&
    lines.every(
      (l) => l.itemId && l.uoMId && l.batchNumber && l.sourceMRN && l.quantity > 0 && l.returnQuantity > 0,
    );
  const canSubmit = !!declarationNumber && !!procedureId && hasLine;

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        background: 'rgba(0,0,0,0.4)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 1000,
      }}
    >
      <div style={{ background: '#fff', borderRadius: 8, padding: 22, width: 980, maxHeight: '92vh', overflow: 'auto' }}>
        <h3 style={{ marginTop: 0 }}>↩️ {t('returnDecl.title', 'Враќање (6121)')}</h3>
        <p style={{ color: '#666', marginTop: -6, fontSize: 13 }}>
          {t(
            'returnDecl.hint',
            'Враќа ре-извезена LON-роба. Го намалува DischargedQuantity на MRN-от, повторно задолжува гаранција и ги враќа Exported балансите во Imported или InProduction.',
          )}
        </p>

        {err && (
          <div style={{ background: '#fde', color: '#b00020', padding: 8, borderRadius: 4, marginBottom: 10 }}>{err}</div>
        )}

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10, marginBottom: 14 }}>
          <label>
            {t('returnDecl.number', 'Број')}*
            <input value={declarationNumber} onChange={(e) => setDeclarationNumber(e.target.value)} style={{ width: '100%' }} />
          </label>
          <label>
            {t('returnDecl.date', 'Датум')}
            <input type="date" value={declarationDate} onChange={(e) => setDeclarationDate(e.target.value)} style={{ width: '100%' }} />
          </label>
          <label>
            MRN
            <input value={mrn} onChange={(e) => setMrn(e.target.value)} placeholder="auto" style={{ width: '100%' }} />
          </label>
          <label>
            {t('returnDecl.procedure', 'Постапка')}*
            <select value={procedureId} onChange={(e) => setProcedureId(e.target.value)} style={{ width: '100%' }}>
              <option value="">—</option>
              {procedures.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.code} — {p.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            {t('returnDecl.partner', 'Партнер')}
            <select value={partnerId} onChange={(e) => setPartnerId(e.target.value)} style={{ width: '100%' }}>
              <option value="">—</option>
              {partners.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.code} — {p.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            {t('returnDecl.currency', 'Валута')}
            <input value={currency} onChange={(e) => setCurrency(e.target.value)} style={{ width: '100%' }} />
          </label>
          <label>
            {t('returnDecl.totalValue', 'Вкупна царинска вредност')}
            <input type="number" value={totalCustomsValue} onChange={(e) => setTotalCustomsValue(Number(e.target.value))} style={{ width: '100%' }} />
          </label>
          <label>
            {t('returnDecl.remarks', 'Забелешки')}
            <input value={specialRemarks} onChange={(e) => setSpecialRemarks(e.target.value)} style={{ width: '100%' }} />
          </label>
        </div>

        <h4 style={{ marginBottom: 6 }}>{t('returnDecl.lines', 'Ставки')}</h4>
        <table style={{ width: '100%', fontSize: 12, borderCollapse: 'collapse' }}>
          <thead>
            <tr style={{ background: '#eee' }}>
              <th style={th}>Item</th>
              <th style={th}>Batch</th>
              <th style={th}>Source MRN</th>
              <th style={th}>Qty</th>
              <th style={th}>UoM</th>
              <th style={th}>Return Qty</th>
              <th style={th}>Target</th>
              <th style={th}>Customs Value</th>
              <th style={th}>Tariff</th>
              <th style={th}></th>
            </tr>
          </thead>
          <tbody>
            {lines.map((l, i) => (
              <tr key={i}>
                <td style={td}>
                  <select value={l.itemId} onChange={(e) => updateLine(i, { itemId: e.target.value })} style={{ width: 160 }}>
                    <option value="">—</option>
                    {items.map((it) => (
                      <option key={it.id} value={it.id}>
                        {it.code}
                      </option>
                    ))}
                  </select>
                </td>
                <td style={td}>
                  <input value={l.batchNumber} onChange={(e) => updateLine(i, { batchNumber: e.target.value })} style={{ width: 110 }} />
                </td>
                <td style={td}>
                  <input value={l.sourceMRN} onChange={(e) => updateLine(i, { sourceMRN: e.target.value })} style={{ width: 160 }} />
                </td>
                <td style={td}>
                  <input type="number" value={l.quantity} onChange={(e) => updateLine(i, { quantity: Number(e.target.value) })} style={{ width: 70 }} />
                </td>
                <td style={td}>
                  <select value={l.uoMId} onChange={(e) => updateLine(i, { uoMId: e.target.value })} style={{ width: 80 }}>
                    <option value="">—</option>
                    {uoms.map((u) => (
                      <option key={u.id} value={u.id}>
                        {u.code}
                      </option>
                    ))}
                  </select>
                </td>
                <td style={td}>
                  <input type="number" value={l.returnQuantity} onChange={(e) => updateLine(i, { returnQuantity: Number(e.target.value) })} style={{ width: 80 }} />
                </td>
                <td style={td}>
                  <select value={l.returnTarget} onChange={(e) => updateLine(i, { returnTarget: e.target.value as any })} style={{ width: 120 }}>
                    <option value="Imported">Imported</option>
                    <option value="InProduction">InProduction</option>
                  </select>
                </td>
                <td style={td}>
                  <input type="number" value={l.customsValue} onChange={(e) => updateLine(i, { customsValue: Number(e.target.value) })} style={{ width: 80 }} />
                </td>
                <td style={td}>
                  <input value={l.tariffCode} onChange={(e) => updateLine(i, { tariffCode: e.target.value })} style={{ width: 90 }} />
                </td>
                <td style={td}>
                  <button onClick={() => setLines(lines.filter((_, idx) => idx !== i))} disabled={lines.length === 1}>
                    ✕
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        <button onClick={() => setLines([...lines, newLine()])} style={{ marginTop: 8 }}>
          + {t('returnDecl.addLine', 'Додај ставка')}
        </button>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 16 }}>
          <button onClick={onClose} disabled={saving}>
            {t('common.cancel')}
          </button>
          <button
            onClick={submit}
            disabled={!canSubmit || saving}
            style={{ background: '#b45309', color: '#fff', padding: '6px 14px' }}
          >
            {saving ? '…' : t('returnDecl.submit', 'Регистрирај Return')}
          </button>
        </div>
      </div>
    </div>
  );
};

const th: React.CSSProperties = { padding: '4px 6px', textAlign: 'left', borderBottom: '1px solid #ccc', fontSize: 11 };
const td: React.CSSProperties = { padding: '4px 6px', borderBottom: '1px solid #eee' };

export default ReturnDeclarationModal;
