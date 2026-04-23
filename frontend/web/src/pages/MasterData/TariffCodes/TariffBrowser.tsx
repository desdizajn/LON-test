import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { api } from '../../../services/api';
import { translateError } from '../../../utils/translateError';
import { formatQuantity } from '../../../utils/format';

/**
 * TARIC (customs tariff) browser + what-if duty calculator.
 *
 * Left side: paginated searchable list of TariffCode rows (GET
 * /api/KnowledgeBase/tariff-codes). Right side: "What if" panel — the
 * operator enters a customs value + currency + exchange rate + date
 * (optionally preferential origin), clicks Calculate, and sees the full
 * duty + VAT breakdown using the legacy PresmetajDavackiPoNaim formula.
 */

type TariffRow = {
  id: string;
  tariffNumber: string;
  tarbr: string;
  taroz1?: string;
  taroz2?: string;
  taroz3?: string;
  description?: string;
  customsRate?: number | null;
  vatRate?: number | null;
  unitMeasure?: string | null;
};

type WhatIfResult = {
  tariffCode: string;
  description?: string;
  customsValue: number;
  currency: string;
  exchangeRate: number;
  customsBase: number;
  dutyRate: number;
  dutyAmount: number;
  vatRate: number;
  vatBase: number;
  vatAmount: number;
  totalDuties: number;
  rateSource: string;
  preferentialApplied: boolean;
  warningMessage?: string | null;
};

const TariffBrowser: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<TariffRow[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(25);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(false);
  const [selected, setSelected] = useState<TariffRow | null>(null);

  // Calculator state
  const [calcValue, setCalcValue] = useState<number>(1000);
  const [calcCurrency, setCalcCurrency] = useState('EUR');
  const [calcRate, setCalcRate] = useState<number>(61.5); // MKD per EUR default
  const [calcDate, setCalcDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [calcQty, setCalcQty] = useState<number>(1);
  const [calcCountry, setCalcCountry] = useState<string>('');
  const [calcPref, setCalcPref] = useState(false);
  const [calcResult, setCalcResult] = useState<WhatIfResult | null>(null);
  const [calcBusy, setCalcBusy] = useState(false);

  const load = async (p = 1, term = search) => {
    setLoading(true);
    try {
      const r = await api.get('/KnowledgeBase/tariff-codes', {
        params: { search: term || undefined, page: p, pageSize },
      });
      setRows((r.data.items as TariffRow[]) ?? []);
      setTotal(r.data.total ?? 0);
      setPage(p);
    } catch (err) {
      toast.error(translateError(err));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load(1, '');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const doSearch = () => load(1, search);

  const calculate = async () => {
    if (!selected) {
      toast.error(t('tariffBrowser.pickFirst', 'Прво избери тарифна ознака од листата.'));
      return;
    }
    setCalcBusy(true);
    try {
      const r = await api.post('/customs/duty-calculator', {
        tariffCode: selected.tariffNumber,
        customsValue: calcValue,
        currency: calcCurrency,
        exchangeRate: calcRate,
        date: calcDate,
        quantity: calcQty,
        countryOfOrigin: calcCountry || null,
        isPreferentialOrigin: calcPref,
      });
      setCalcResult(r.data as WhatIfResult);
    } catch (err) {
      toast.error(translateError(err));
      setCalcResult(null);
    } finally {
      setCalcBusy(false);
    }
  };

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  return (
    <div style={{ padding: 20 }}>
      <div style={{ marginBottom: 20 }}>
        <h1 style={{ margin: 0 }}>{t('tariffBrowser.title', 'Царинска тарифа (TARIC)')}</h1>
        <div style={{ color: '#666', fontSize: 13, marginTop: 5 }}>
          {t(
            'tariffBrowser.subtitle',
            'Пребарувачка база на 10-цифрените TARIC ознаки + „што ако" калкулатор за давачки (царина + ДДВ) по legacy формула PresmetajDavackiPoNaim.'
          )}
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: 20, alignItems: 'start' }}>
        {/* Left — tariff table */}
        <section>
          <div style={{ display: 'flex', gap: 10, marginBottom: 10, alignItems: 'center' }}>
            <input
              placeholder={t('tariffBrowser.searchPlaceholder', 'Барај по броj / TARBR / опис...') as string}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && doSearch()}
              style={{ flex: 1, padding: 8, border: '1px solid #ccc', borderRadius: 4 }}
            />
            <button className="btn btn-primary btn-sm" onClick={doSearch} disabled={loading}>
              {t('common.search', 'Барај')}
            </button>
          </div>

          {loading && <div className="loading">{t('common.loading')}</div>}
          {!loading && rows.length === 0 && (
            <div style={{ padding: 40, textAlign: 'center', color: '#888' }}>
              {t('tariffBrowser.empty', 'Нема тарифи.')}
            </div>
          )}
          {!loading && rows.length > 0 && (
            <>
              <table className="data-table" style={{ fontSize: 12 }}>
                <thead>
                  <tr>
                    <th>{t('tariffBrowser.col.number', 'TARIC')}</th>
                    <th>{t('tariffBrowser.col.description', 'Опис')}</th>
                    <th style={{ textAlign: 'right' }}>{t('tariffBrowser.col.customsRate', 'Царина %')}</th>
                    <th style={{ textAlign: 'right' }}>{t('tariffBrowser.col.vatRate', 'ДДВ %')}</th>
                    <th>{t('tariffBrowser.col.uom', 'ЕМ')}</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr
                      key={r.id}
                      onClick={() => setSelected(r)}
                      style={{
                        cursor: 'pointer',
                        background: selected?.id === r.id ? '#e3f2fd' : undefined,
                      }}
                    >
                      <td><code>{r.tariffNumber}</code></td>
                      <td style={{ maxWidth: 420 }}>{r.description || <span style={{ color: '#aaa' }}>—</span>}</td>
                      <td style={{ textAlign: 'right', fontFamily: 'monospace' }}>
                        {r.customsRate != null ? (r.customsRate ?? 0).toFixed(2) + '%' : <span style={{ color: '#aaa' }}>—</span>}
                      </td>
                      <td style={{ textAlign: 'right', fontFamily: 'monospace' }}>
                        {r.vatRate != null ? (r.vatRate ?? 0).toFixed(2) + '%' : <span style={{ color: '#aaa' }}>—</span>}
                      </td>
                      <td>{r.unitMeasure || <span style={{ color: '#aaa' }}>—</span>}</td>
                    </tr>
                  ))}
                </tbody>
              </table>

              <div style={{ display: 'flex', gap: 10, marginTop: 10, alignItems: 'center', fontSize: 13 }}>
                <button
                  className="btn btn-sm btn-outline"
                  onClick={() => load(page - 1, search)}
                  disabled={page <= 1 || loading}
                >
                  ← {t('common.previous', 'Претходна')}
                </button>
                <span>
                  {t('common.page', 'Страница')} {page} / {totalPages} ({total})
                </span>
                <button
                  className="btn btn-sm btn-outline"
                  onClick={() => load(page + 1, search)}
                  disabled={page >= totalPages || loading}
                >
                  {t('common.next', 'Следна')} →
                </button>
              </div>
            </>
          )}
        </section>

        {/* Right — what-if calculator */}
        <section
          style={{
            border: '1px solid #e0e0e0',
            borderRadius: 8,
            padding: 15,
            background: '#fafafa',
            position: 'sticky',
            top: 20,
          }}
        >
          <h3 style={{ marginTop: 0, fontSize: 15 }}>
            🧮 {t('tariffBrowser.calculator', '„Што ако" — калкулатор на давачки')}
          </h3>

          {selected ? (
            <div style={{ fontSize: 12, marginBottom: 10, padding: 8, background: '#e3f2fd', borderRadius: 4 }}>
              <strong>{selected.tariffNumber}</strong>
              {selected.description && (
                <div style={{ color: '#666', marginTop: 3 }}>{selected.description}</div>
              )}
            </div>
          ) : (
            <div style={{ fontSize: 12, marginBottom: 10, color: '#888', fontStyle: 'italic' }}>
              {t('tariffBrowser.pickFirst', 'Прво избери тарифна ознака од листата.')}
            </div>
          )}

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, fontSize: 13 }}>
            <label>
              {t('tariffBrowser.calc.value', 'Вредност')}
              <input
                type="number"
                step="0.01"
                value={calcValue}
                onChange={(e) => setCalcValue(parseFloat(e.target.value) || 0)}
                style={{ width: '100%', padding: 6, textAlign: 'right', fontFamily: 'monospace' }}
              />
            </label>
            <label>
              {t('tariffBrowser.calc.currency', 'Валута')}
              <select
                value={calcCurrency}
                onChange={(e) => setCalcCurrency(e.target.value)}
                style={{ width: '100%', padding: 6 }}
              >
                <option value="EUR">EUR</option>
                <option value="USD">USD</option>
                <option value="MKD">MKD</option>
                <option value="GBP">GBP</option>
                <option value="CHF">CHF</option>
                <option value="TRY">TRY</option>
              </select>
            </label>
            <label>
              {t('tariffBrowser.calc.rate', 'Курс (MKD)')}
              <input
                type="number"
                step="0.0001"
                value={calcRate}
                onChange={(e) => setCalcRate(parseFloat(e.target.value) || 0)}
                style={{ width: '100%', padding: 6, textAlign: 'right', fontFamily: 'monospace' }}
              />
            </label>
            <label>
              {t('tariffBrowser.calc.date', 'Датум')}
              <input
                type="date"
                value={calcDate}
                onChange={(e) => setCalcDate(e.target.value)}
                style={{ width: '100%', padding: 6 }}
              />
            </label>
            <label>
              {t('tariffBrowser.calc.qty', 'Количина')}
              <input
                type="number"
                step="0.0001"
                value={calcQty}
                onChange={(e) => setCalcQty(parseFloat(e.target.value) || 0)}
                style={{ width: '100%', padding: 6, textAlign: 'right', fontFamily: 'monospace' }}
              />
            </label>
            <label>
              {t('tariffBrowser.calc.country', 'Земја (ISO-2)')}
              <input
                value={calcCountry}
                onChange={(e) => setCalcCountry(e.target.value.toUpperCase())}
                placeholder="DE / TR / CN"
                maxLength={2}
                style={{ width: '100%', padding: 6, textTransform: 'uppercase' }}
              />
            </label>
            <label style={{ gridColumn: '1 / span 2', display: 'flex', alignItems: 'center', gap: 8 }}>
              <input
                type="checkbox"
                checked={calcPref}
                onChange={(e) => setCalcPref(e.target.checked)}
              />
              {t('tariffBrowser.calc.preferential', 'Преференцијално потекло (EU / TR / CEFTA)')}
            </label>
          </div>

          <button
            className="btn btn-primary"
            onClick={calculate}
            disabled={calcBusy || !selected}
            style={{ width: '100%', marginTop: 12 }}
          >
            {calcBusy ? t('common.calculating', 'Пресметувам...') : t('tariffBrowser.calc.run', 'Пресметај')}
          </button>

          {calcResult && (
            <div style={{ marginTop: 15, fontSize: 13 }}>
              <div style={{ background: '#fff', padding: 12, borderRadius: 4, border: '1px solid #ddd' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                  <span>{t('tariffBrowser.result.customsBase', 'Царинска основа (MKD)')}</span>
                  <strong style={{ fontFamily: 'monospace' }}>{formatQuantity(calcResult.customsBase)}</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                  <span>
                    {t('tariffBrowser.result.duty', 'Царина')} ({(calcResult.dutyRate ?? 0).toFixed(2)}%)
                  </span>
                  <strong style={{ fontFamily: 'monospace', color: '#d9534f' }}>
                    {formatQuantity(calcResult.dutyAmount)}
                  </strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                  <span>{t('tariffBrowser.result.vatBase', 'ДДВ основа')}</span>
                  <strong style={{ fontFamily: 'monospace' }}>{formatQuantity(calcResult.vatBase)}</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 8 }}>
                  <span>
                    {t('tariffBrowser.result.vat', 'ДДВ')} ({(calcResult.vatRate ?? 0).toFixed(2)}%)
                  </span>
                  <strong style={{ fontFamily: 'monospace', color: '#f0ad4e' }}>
                    {formatQuantity(calcResult.vatAmount)}
                  </strong>
                </div>
                <hr style={{ border: 0, borderTop: '1px solid #eee', margin: '8px 0' }} />
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 14 }}>
                  <strong>{t('tariffBrowser.result.total', 'Вкупно давачки')}</strong>
                  <strong style={{ fontFamily: 'monospace', color: '#0066cc', fontSize: 16 }}>
                    {formatQuantity(calcResult.totalDuties)} MKD
                  </strong>
                </div>
              </div>
              <div style={{ fontSize: 11, color: '#666', marginTop: 8 }}>
                <strong>{t('tariffBrowser.result.source', 'Извор на стапка')}:</strong> {calcResult.rateSource}
                {calcResult.preferentialApplied && (
                  <div style={{ marginTop: 3, color: '#5cb85c' }}>
                    ✓ {t('tariffBrowser.result.preferentialApplied', 'Преференцијал применет')}
                  </div>
                )}
              </div>
              {calcResult.warningMessage && (
                <div
                  style={{
                    marginTop: 8,
                    padding: 8,
                    background: '#fff3cd',
                    border: '1px solid #ffeaa7',
                    borderRadius: 4,
                    fontSize: 12,
                    color: '#856404',
                  }}
                >
                  ⚠️ {calcResult.warningMessage}
                </div>
              )}
            </div>
          )}
        </section>
      </div>
    </div>
  );
};

export default TariffBrowser;
