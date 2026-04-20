import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi, masterDataApi } from '../../services/api';

/**
 * P2.6a — EX (re-export, procedure 3151) declaration.
 *
 * One modal: header (date / procedure / partner / currency / sender / destination)
 * + one or more lines, each referencing a SourceMRN (previous IM 4200 that
 * brought the raw material in) + a DischargeQuantity that the backend credits
 * back to the guarantee ledger pro-rata.
 *
 * Backend: POST /api/customs/declarations/export — the handler walks the FG
 * batch, consolidates it across Imported/InProduction→Exported, writes a
 * TraceLink IM→EX, and credits the matching Guarantee Debit proportionally.
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
  dischargeQuantity: number;
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
  dischargeQuantity: 0,
});

const ExportDeclarationModal: React.FC<Props> = ({ onClose, onSuccess }) => {
  const { t } = useTranslation();
  const today = new Date().toISOString().slice(0, 10);

  const [items, setItems] = useState<Item[]>([]);
  const [uoms, setUoms] = useState<Uom[]>([]);
  const [procedures, setProcedures] = useState<Procedure[]>([]);
  const [partners, setPartners] = useState<Partner[]>([]);

  const [declarationNumber, setDeclarationNumber] = useState(
    `EX-${new Date().toISOString().slice(0, 10).replace(/-/g, '')}-${Math.floor(Math.random() * 9999)
      .toString()
      .padStart(4, '0')}`,
  );
  const [mrn, setMrn] = useState('');
  const [declarationDate, setDeclarationDate] = useState(today);
  const [procedureId, setProcedureId] = useState('');
  const [partnerId, setPartnerId] = useState('');
  const [currency, setCurrency] = useState('EUR');
  const [totalCustomsValue, setTotalCustomsValue] = useState<number>(0);
  const [countryOfDestination, setCountryOfDestination] = useState('');
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
      // 3151 = Re-export of LON goods (seeded by P2.6a migration)
      setProcedures(all);
      const exP = all.find((p) => p.code === '3151');
      if (exP) setProcedureId(exP.id);
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
        countryOfDestination: countryOfDestination || null,
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
          dischargeQuantity: Number(l.dischargeQuantity) || 0,
        })),
      };
      const r = await (customsApi as any).createExportDeclaration
        ? await (customsApi as any).createExportDeclaration(payload)
        : await (await import('../../services/api')).api.post('/Customs/declarations/export', payload);
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
    lines.every((l) => l.itemId && l.uoMId && l.batchNumber && l.sourceMRN && l.quantity > 0 && l.dischargeQuantity > 0);

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
      <div style={{ background: '#fff', borderRadius: 8, padding: 22, width: 960, maxHeight: '92vh', overflow: 'auto' }}>
        <h3 style={{ marginTop: 0 }}>🚢 {t('exportDecl.title', 'Извозна декларација (EX / 3151)')}</h3>
        <p style={{ color: '#666', marginTop: -6, fontSize: 13 }}>
          {t(
            'exportDecl.hint',
            'Ре-извоз на LON-роба. Ќе ја разреши IM-MRN-ат, ќе ја кредитира гаранцијата и ќе ги премести FG балансите во Exported.',
          )}
        </p>

        {err && (
          <div style={{ background: '#fde', color: '#b00020', padding: 8, borderRadius: 4, marginBottom: 10 }}>{err}</div>
        )}

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10, marginBottom: 14 }}>
          <label>
            {t('exportDecl.number', 'Број')}*
            <input value={declarationNumber} onChange={(e) => setDeclarationNumber(e.target.value)} style={{ width: '100%' }} />
          </label>
          <label>
            {t('exportDecl.date', 'Датум')}
            <input type="date" value={declarationDate} onChange={(e) => setDeclarationDate(e.target.value)} style={{ width: '100%' }} />
          </label>
          <label>
            MRN
            <input value={mrn} onChange={(e) => setMrn(e.target.value)} placeholder="auto" style={{ width: '100%' }} />
          </label>
          <label>
            {t('exportDecl.procedure', 'Постапка')}*
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
            {t('exportDecl.partner', 'Партнер')}
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
            {t('exportDecl.currency', 'Валута')}
            <input value={currency} onChange={(e) => setCurrency(e.target.value)} style={{ width: '100%' }} />
          </label>
          <label>
            {t('exportDecl.totalValue', 'Вкупна царинска вредност')}
            <input type="number" value={totalCustomsValue} onChange={(e) => setTotalCustomsValue(Number(e.target.value))} style={{ width: '100%' }} />
          </label>
          <label>
            {t('exportDecl.destination', 'Држава на дестинација')}
            <input value={countryOfDestination} onChange={(e) => setCountryOfDestination(e.target.value)} maxLength={2} style={{ width: '100%' }} />
          </label>
          <label>
            {t('exportDecl.remarks', 'Забелешки')}
            <input value={specialRemarks} onChange={(e) => setSpecialRemarks(e.target.value)} style={{ width: '100%' }} />
          </label>
        </div>

        <h4 style={{ marginBottom: 6 }}>{t('exportDecl.lines', 'Ставки')}</h4>
        <table style={{ width: '100%', fontSize: 12, borderCollapse: 'collapse' }}>
          <thead>
            <tr style={{ background: '#eee' }}>
              <th style={th}>Item</th>
              <th style={th}>Batch</th>
              <th style={th}>Source MRN</th>
              <th style={th}>Qty</th>
              <th style={th}>UoM</th>
              <th style={th}>Discharge Qty</th>
              <th style={th}>Customs Value</th>
              <th style={th}>Tariff</th>
              <th style={th}>Country</th>
              <th style={th}>Net W</th>
              <th style={th}>Gross W</th>
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
                  <input type="number" value={l.dischargeQuantity} onChange={(e) => updateLine(i, { dischargeQuantity: Number(e.target.value) })} style={{ width: 80 }} />
                </td>
                <td style={td}>
                  <input type="number" value={l.customsValue} onChange={(e) => updateLine(i, { customsValue: Number(e.target.value) })} style={{ width: 80 }} />
                </td>
                <td style={td}>
                  <input value={l.tariffCode} onChange={(e) => updateLine(i, { tariffCode: e.target.value })} style={{ width: 90 }} />
                </td>
                <td style={td}>
                  <input value={l.countryOfOrigin} onChange={(e) => updateLine(i, { countryOfOrigin: e.target.value })} maxLength={2} style={{ width: 45 }} />
                </td>
                <td style={td}>
                  <input type="number" value={l.netWeight} onChange={(e) => updateLine(i, { netWeight: Number(e.target.value) })} style={{ width: 60 }} />
                </td>
                <td style={td}>
                  <input type="number" value={l.grossWeight} onChange={(e) => updateLine(i, { grossWeight: Number(e.target.value) })} style={{ width: 60 }} />
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
          + {t('exportDecl.addLine', 'Додај ставка')}
        </button>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 16 }}>
          <button onClick={onClose} disabled={saving}>
            {t('common.cancel')}
          </button>
          <button
            onClick={submit}
            disabled={!canSubmit || saving}
            style={{ background: '#2b6cb0', color: '#fff', padding: '6px 14px' }}
          >
            {saving ? '…' : t('exportDecl.submit', 'Регистрирај EX')}
          </button>
        </div>
      </div>
    </div>
  );
};

const th: React.CSSProperties = { padding: '4px 6px', textAlign: 'left', borderBottom: '1px solid #ccc', fontSize: 11 };
const td: React.CSSProperties = { padding: '4px 6px', borderBottom: '1px solid #eee' };

export default ExportDeclarationModal;
