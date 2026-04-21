import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { financeApi, masterDataApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate } from '../../utils/format';

/**
 * P12.3 — client contracts + rate cards.
 *
 * Left pane: list of contracts (filterable by partner / active-only).
 * Right pane: selected contract's header + rate card table with inline
 * upsert of entries. Rates are either PerPiece (requires Item) or
 * PerMinute (requires OperationCode). Currency defaults from the contract.
 */

type RateEntry = {
  id: string;
  contractId: string;
  rateType: number;
  itemId: string | null;
  itemCode: string | null;
  itemName: string | null;
  operationCode: string | null;
  ratePerUnit: number;
  currency: string;
  validFrom: string;
  validTo: string | null;
  notes: string | null;
};

type Contract = {
  id: string;
  number: string;
  partnerId: string;
  partnerName: string;
  validFrom: string;
  validTo: string | null;
  paymentTermsDays: number;
  currency: string;
  isActive: boolean;
  notes: string | null;
  rateCard: RateEntry[];
};

type Partner = { id: string; code: string; name: string };
type Item = { id: string; code: string; name: string };

const RATE_TYPES = [
  { value: 1, key: 'perPiece' },
  { value: 2, key: 'perMinute' },
];

const emptyRateDraft = () => ({
  entryId: '' as string,
  rateType: 1,
  itemId: '',
  operationCode: '',
  ratePerUnit: 0,
  currency: 'EUR',
  validFrom: new Date().toISOString().slice(0, 10),
  validTo: '',
  notes: '',
});

const ClientContracts: React.FC = () => {
  const { t } = useTranslation();
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [partners, setPartners] = useState<Partner[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [activeOnly, setActiveOnly] = useState<boolean>(true);
  const [partnerFilter, setPartnerFilter] = useState<string>('');
  const [search, setSearch] = useState('');

  // New contract draft
  const [draftOpen, setDraftOpen] = useState(false);
  const [cNumber, setCNumber] = useState('');
  const [cPartner, setCPartner] = useState('');
  const [cValidFrom, setCValidFrom] = useState(new Date().toISOString().slice(0, 10));
  const [cValidTo, setCValidTo] = useState('');
  const [cTerms, setCTerms] = useState(30);
  const [cCurrency, setCCurrency] = useState('EUR');
  const [cNotes, setCNotes] = useState('');
  const [saving, setSaving] = useState(false);

  // Rate editor state
  const [rateDraft, setRateDraft] = useState(emptyRateDraft());

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [cs, ps, is_] = await Promise.all([
        financeApi.getContracts({ activeOnly, partnerId: partnerFilter || undefined }),
        masterDataApi.getPartners(),
        masterDataApi.getItems(),
      ]);
      const cEnv = cs.data as { data?: Contract[] };
      setContracts(cEnv?.data ?? (cs.data as Contract[]) ?? []);
      setPartners((ps.data as Partner[]) ?? []);
      setItems((is_.data as Item[]) ?? []);
    } catch (err) {
      setError(translateError(err));
    } finally {
      setLoading(false);
    }
  }, [activeOnly, partnerFilter]);

  useEffect(() => { load(); }, [load]);

  const selected = useMemo(
    () => contracts.find((c) => c.id === selectedId) ?? null,
    [contracts, selectedId],
  );

  const createContract = async () => {
    if (!cNumber.trim() || !cPartner) {
      setError(t('finance.contracts.errors.numberAndPartnerRequired'));
      return;
    }
    setSaving(true);
    try {
      await financeApi.createContract({
        number: cNumber.trim(),
        partnerId: cPartner,
        validFrom: cValidFrom,
        validTo: cValidTo || null,
        paymentTermsDays: cTerms,
        currency: cCurrency,
        notes: cNotes.trim() || null,
      });
      setDraftOpen(false);
      setCNumber(''); setCPartner(''); setCValidTo(''); setCNotes('');
      await load();
    } catch (err) {
      setError(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  const saveRate = async () => {
    if (!selected) return;
    if (rateDraft.rateType === 1 && !rateDraft.itemId) {
      setError(t('finance.contracts.errors.rateMissingItem'));
      return;
    }
    if (rateDraft.rateType === 2 && !rateDraft.operationCode.trim()) {
      setError(t('finance.contracts.errors.rateMissingOperation'));
      return;
    }
    setSaving(true);
    try {
      await financeApi.upsertRate(selected.id, {
        entryId: rateDraft.entryId || null,
        rateType: rateDraft.rateType,
        itemId: rateDraft.itemId || null,
        operationCode: rateDraft.operationCode.trim() || null,
        ratePerUnit: Number(rateDraft.ratePerUnit) || 0,
        currency: rateDraft.currency || selected.currency,
        validFrom: rateDraft.validFrom,
        validTo: rateDraft.validTo || null,
        notes: rateDraft.notes.trim() || null,
      });
      setRateDraft(emptyRateDraft());
      await load();
    } catch (err) {
      setError(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  const editRate = (r: RateEntry) => {
    setRateDraft({
      entryId: r.id,
      rateType: r.rateType,
      itemId: r.itemId ?? '',
      operationCode: r.operationCode ?? '',
      ratePerUnit: r.ratePerUnit,
      currency: r.currency,
      validFrom: r.validFrom.slice(0, 10),
      validTo: r.validTo ? r.validTo.slice(0, 10) : '',
      notes: r.notes ?? '',
    });
  };

  const deleteRate = async (r: RateEntry) => {
    if (!selected) return;
    if (!window.confirm(t('finance.contracts.confirmDeleteRate'))) return;
    try {
      await financeApi.deleteRate(selected.id, r.id);
      await load();
    } catch (err) {
      setError(translateError(err));
    }
  };

  const toggleActive = async () => {
    if (!selected) return;
    setSaving(true);
    try {
      await financeApi.updateContract(selected.id, {
        validTo: selected.validTo,
        paymentTermsDays: selected.paymentTermsDays,
        isActive: !selected.isActive,
        notes: selected.notes,
      });
      await load();
    } catch (err) {
      setError(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('finance.contracts.title')}</h1>
      <p style={{ color: '#666' }}>{t('finance.contracts.subtitle')}</p>

      {error && (
        <div style={{ padding: 10, background: '#ffebee', color: '#c62828', marginBottom: 12, borderRadius: 4 }}>
          {error}
          <button onClick={() => setError(null)} style={{ marginLeft: 8 }}>×</button>
        </div>
      )}

      <div style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'center', flexWrap: 'wrap' }}>
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('finance.contracts.searchPlaceholder') as string}
          style={{ padding: 6, minWidth: 220 }}
        />
        <label>
          {t('finance.contracts.filterPartner')}:{' '}
          <select value={partnerFilter} onChange={(e) => setPartnerFilter(e.target.value)}>
            <option value="">{t('common.all')}</option>
            {partners.map((p) => (
              <option key={p.id} value={p.id}>{p.code} — {p.name}</option>
            ))}
          </select>
        </label>
        <label>
          <input type="checkbox" checked={activeOnly} onChange={(e) => setActiveOnly(e.target.checked)} />{' '}
          {t('finance.contracts.activeOnly')}
        </label>
        <button onClick={() => setDraftOpen(!draftOpen)}>
          {draftOpen ? t('common.cancel') : t('finance.contracts.newContract')}
        </button>
      </div>

      {draftOpen && (
        <div style={{ padding: 12, background: '#f5f5f5', borderRadius: 4, marginBottom: 12 }}>
          <h3 style={{ marginTop: 0 }}>{t('finance.contracts.newContract')}</h3>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 8 }}>
            <label>{t('finance.contracts.number')}
              <input value={cNumber} onChange={(e) => setCNumber(e.target.value)} />
            </label>
            <label>{t('finance.contracts.partner')}
              <select value={cPartner} onChange={(e) => setCPartner(e.target.value)}>
                <option value="">— {t('common.select')} —</option>
                {partners.map((p) => (
                  <option key={p.id} value={p.id}>{p.code} — {p.name}</option>
                ))}
              </select>
            </label>
            <label>{t('finance.contracts.currency')}
              <input value={cCurrency} onChange={(e) => setCCurrency(e.target.value.toUpperCase())} maxLength={3} />
            </label>
            <label>{t('finance.contracts.validFrom')}
              <input type="date" value={cValidFrom} onChange={(e) => setCValidFrom(e.target.value)} />
            </label>
            <label>{t('finance.contracts.validTo')}
              <input type="date" value={cValidTo} onChange={(e) => setCValidTo(e.target.value)} />
            </label>
            <label>{t('finance.contracts.paymentTerms')}
              <input type="number" min={1} value={cTerms} onChange={(e) => setCTerms(Number(e.target.value))} />
            </label>
            <label style={{ gridColumn: '1 / span 3' }}>{t('finance.contracts.notes')}
              <textarea rows={2} value={cNotes} onChange={(e) => setCNotes(e.target.value)} />
            </label>
          </div>
          <button onClick={createContract} disabled={saving} style={{ marginTop: 8 }}>
            {saving ? t('common.saving') : t('common.save')}
          </button>
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: 16 }}>
        <div>
          {(() => {
            const q = search.trim().toLowerCase();
            const filteredContracts = q
              ? contracts.filter((c) => c.number.toLowerCase().includes(q) || c.partnerName.toLowerCase().includes(q))
              : contracts;
            return (
          <>
          <h3>{t('finance.contracts.listTitle')} ({filteredContracts.length})</h3>
          {loading ? <div>{t('common.loading')}</div> : (
            <table style={{ width: '100%', fontSize: 13 }}>
              <thead>
                <tr><th>{t('finance.contracts.number')}</th><th>{t('finance.contracts.partner')}</th><th>{t('finance.contracts.validity')}</th><th></th></tr>
              </thead>
              <tbody>
                {filteredContracts.map((c) => (
                  <tr key={c.id}
                      onClick={() => setSelectedId(c.id)}
                      style={{ background: selectedId === c.id ? '#e3f2fd' : undefined, cursor: 'pointer' }}>
                    <td>{c.number}</td>
                    <td>{c.partnerName}</td>
                    <td style={{ fontSize: 12, color: '#666' }}>
                      {formatDate(c.validFrom)} → {c.validTo ? formatDate(c.validTo) : '∞'}
                    </td>
                    <td style={{ textAlign: 'right' }}>
                      {c.isActive ? (
                        <span style={{ color: '#2e7d32' }}>●</span>
                      ) : (
                        <span style={{ color: '#999' }}>○</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          </>
            );
          })()}
        </div>

        <div>
          {!selected ? (
            <div style={{ color: '#999', padding: 24, textAlign: 'center' }}>
              {t('finance.contracts.selectAContract')}
            </div>
          ) : (
            <>
              <div style={{ padding: 12, background: '#f9f9f9', borderRadius: 4, marginBottom: 12 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <h3 style={{ margin: 0 }}>{selected.number}</h3>
                  <button onClick={toggleActive} disabled={saving}>
                    {selected.isActive ? t('finance.contracts.deactivate') : t('finance.contracts.activate')}
                  </button>
                </div>
                <div style={{ fontSize: 13, color: '#555', marginTop: 4 }}>
                  {selected.partnerName} · {selected.currency} · {t('finance.contracts.paymentTermsShort', { days: selected.paymentTermsDays })}<br/>
                  {formatDate(selected.validFrom)} → {selected.validTo ? formatDate(selected.validTo) : '∞'}
                </div>
                {selected.notes && <div style={{ fontSize: 12, color: '#666', marginTop: 6 }}>{selected.notes}</div>}
              </div>

              <h4>{t('finance.contracts.rateCard')} ({selected.rateCard.length})</h4>
              <table style={{ width: '100%', fontSize: 13 }}>
                <thead>
                  <tr>
                    <th>{t('finance.contracts.rateType')}</th>
                    <th>{t('finance.contracts.target')}</th>
                    <th style={{ textAlign: 'right' }}>{t('finance.contracts.rate')}</th>
                    <th>{t('finance.contracts.validity')}</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {selected.rateCard.map((r) => (
                    <tr key={r.id}>
                      <td>{t(`finance.contracts.rateTypes.${RATE_TYPES.find(x => x.value === r.rateType)?.key ?? 'unknown'}`)}</td>
                      <td>{r.itemCode ? `${r.itemCode} — ${r.itemName}` : r.operationCode}</td>
                      <td style={{ textAlign: 'right' }}>{r.ratePerUnit.toFixed(4)} {r.currency}</td>
                      <td style={{ fontSize: 12, color: '#666' }}>
                        {formatDate(r.validFrom)} → {r.validTo ? formatDate(r.validTo) : '∞'}
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        <button onClick={() => editRate(r)} style={{ marginRight: 4 }}>{t('common.edit')}</button>
                        <button onClick={() => deleteRate(r)} style={{ color: '#c62828' }}>×</button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>

              <div style={{ marginTop: 12, padding: 12, background: '#f0f7ff', borderRadius: 4 }}>
                <h4 style={{ marginTop: 0 }}>
                  {rateDraft.entryId ? t('finance.contracts.editRate') : t('finance.contracts.newRate')}
                </h4>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 8 }}>
                  <label>{t('finance.contracts.rateType')}
                    <select value={rateDraft.rateType} onChange={(e) => setRateDraft({ ...rateDraft, rateType: Number(e.target.value) })}>
                      {RATE_TYPES.map(rt => (
                        <option key={rt.value} value={rt.value}>{t(`finance.contracts.rateTypes.${rt.key}`)}</option>
                      ))}
                    </select>
                  </label>
                  {rateDraft.rateType === 1 ? (
                    <label>{t('finance.contracts.item')}
                      <select value={rateDraft.itemId} onChange={(e) => setRateDraft({ ...rateDraft, itemId: e.target.value })}>
                        <option value="">— {t('common.select')} —</option>
                        {items.map(i => <option key={i.id} value={i.id}>{i.code} — {i.name}</option>)}
                      </select>
                    </label>
                  ) : (
                    <label>{t('finance.contracts.operationCode')}
                      <input value={rateDraft.operationCode} onChange={(e) => setRateDraft({ ...rateDraft, operationCode: e.target.value })} />
                    </label>
                  )}
                  <label>{t('finance.contracts.rate')}
                    <input type="number" step="0.0001" value={rateDraft.ratePerUnit} onChange={(e) => setRateDraft({ ...rateDraft, ratePerUnit: Number(e.target.value) })} />
                  </label>
                  <label>{t('finance.contracts.currency')}
                    <input value={rateDraft.currency} maxLength={3} onChange={(e) => setRateDraft({ ...rateDraft, currency: e.target.value.toUpperCase() })} />
                  </label>
                  <label>{t('finance.contracts.validFrom')}
                    <input type="date" value={rateDraft.validFrom} onChange={(e) => setRateDraft({ ...rateDraft, validFrom: e.target.value })} />
                  </label>
                  <label>{t('finance.contracts.validTo')}
                    <input type="date" value={rateDraft.validTo} onChange={(e) => setRateDraft({ ...rateDraft, validTo: e.target.value })} />
                  </label>
                </div>
                <button onClick={saveRate} disabled={saving} style={{ marginTop: 8 }}>
                  {saving ? t('common.saving') : t('common.save')}
                </button>
                {rateDraft.entryId && (
                  <button onClick={() => setRateDraft(emptyRateDraft())} style={{ marginLeft: 8 }}>
                    {t('common.cancel')}
                  </button>
                )}
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
};

export default ClientContracts;
