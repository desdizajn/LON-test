import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { wmsApi, masterDataApi, api } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';

/**
 * P15.8 — Podelba (multi-producer distribution) page.
 *
 * Two sections:
 *  1. "Unassigned balances" — OK inventory at receiving-dock style locations
 *     whose AssignedProducerId is null. Operator picks one row + clicks
 *     "Распредели" to open the allocation modal.
 *  2. "По производител" — aggregated view of what's currently sitting at
 *     which producer. Read-only visibility.
 */

type Balance = {
  id: string;
  itemId: string;
  locationId: string;
  batchNumber?: string | null;
  mrn?: string | null;
  quantity: number;
  qualityStatus: number;
  lonProcessState?: number | null;
  assignedProducerId?: string | null;
  item?: { code: string; name: string } | null;
  location?: { id?: string; code: string; name: string } | null;
  uoM?: { code: string; name: string } | null;
};

type Partner = {
  id: string;
  code: string;
  name: string;
  partnerType: number;
};

type ProducerRow = {
  id: string;
  itemCode: string;
  itemName: string;
  batchNumber?: string | null;
  mrn?: string | null;
  quantity: number;
  qualityStatus: number;
  lonProcessState?: number | null;
  producerId: string;
  producerCode: string;
  producerName: string;
};

type Allocation = { producerId: string; quantity: number };

const Podelba: React.FC = () => {
  const { t } = useTranslation();
  const [unassigned, setUnassigned] = useState<Balance[]>([]);
  const [byProducer, setByProducer] = useState<ProducerRow[]>([]);
  const [producers, setProducers] = useState<Partner[]>([]);
  const [loading, setLoading] = useState(false);
  const [source, setSource] = useState<Balance | null>(null);
  const [allocations, setAllocations] = useState<Allocation[]>([]);
  const [saving, setSaving] = useState(false);
  const [filterProducer, setFilterProducer] = useState<string>('');

  const load = async () => {
    setLoading(true);
    try {
      const [invRes, prodRes, byProdRes] = await Promise.all([
        wmsApi.getInventory(),
        masterDataApi.getPartners(),
        api.get('/WMS/inventory-by-producer'),
      ]);
      const allInv = (invRes.data as Balance[]) ?? [];
      // Unassigned = OK balance (QualityStatus=1), qty > 0, no producer.
      setUnassigned(
        allInv.filter(
          (b) => b.qualityStatus === 1 && b.quantity > 0 && !b.assignedProducerId
        )
      );
      const partners = (prodRes.data as Partner[]) ?? [];
      setProducers(partners.filter((p) => p.partnerType === 6));
      setByProducer((byProdRes.data as ProducerRow[]) ?? []);
    } catch (err) {
      toast.error(translateError(err));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const openAllocate = (b: Balance) => {
    setSource(b);
    // Seed one empty allocation per producer row but start with a single row.
    setAllocations([{ producerId: producers[0]?.id ?? '', quantity: b.quantity }]);
  };

  const closeAllocate = () => {
    setSource(null);
    setAllocations([]);
  };

  const updateAlloc = (i: number, patch: Partial<Allocation>) =>
    setAllocations((a) => a.map((x, idx) => (idx === i ? { ...x, ...patch } : x)));

  const addAlloc = () =>
    setAllocations((a) => [...a, { producerId: '', quantity: 0 }]);

  const removeAlloc = (i: number) =>
    setAllocations((a) => a.filter((_, idx) => idx !== i));

  const sumAlloc = useMemo(
    () => allocations.reduce((s, a) => s + (a.quantity || 0), 0),
    [allocations]
  );
  const remaining = (source?.quantity ?? 0) - sumAlloc;

  const distributeRemaining = () => {
    if (!source || allocations.length === 0) return;
    const lastIdx = allocations.length - 1;
    const delta = source.quantity - sumAlloc + allocations[lastIdx].quantity;
    updateAlloc(lastIdx, { quantity: Math.max(0, delta) });
  };

  const submit = async () => {
    if (!source) return;
    if (Math.abs(remaining) > 0.0001) {
      toast.error(
        t(
          'podelba.mustBeExact',
          `Σ allocations (${sumAlloc}) мора да е еднакво на source quantity (${source.quantity}).`
        )
      );
      return;
    }
    const payload = {
      sourceBalanceId: source.id,
      allocations: allocations
        .filter((a) => a.producerId && a.quantity > 0)
        .map((a) => ({ producerId: a.producerId, quantity: a.quantity })),
    };
    if (payload.allocations.length === 0) {
      toast.error(t('podelba.noAllocations', 'Нема валидни линии.'));
      return;
    }
    setSaving(true);
    try {
      await api.post('/WMS/podelba', payload);
      toast.success(t('podelba.success', 'Podelba е извршена.'));
      closeAllocate();
      load();
    } catch (err) {
      toast.error(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  const byProducerFiltered = filterProducer
    ? byProducer.filter((r) => r.producerId === filterProducer)
    : byProducer;

  return (
    <div style={{ padding: 20 }}>
      <div style={{ marginBottom: 25 }}>
        <h1 style={{ margin: 0 }}>{t('podelba.title', 'Podelba — дистрибуција по производител')}</h1>
        <div style={{ color: '#666', fontSize: 13, marginTop: 5 }}>
          {t(
            'podelba.subtitle',
            'Поделба на примени материјали кон подизведувачи (legacy frmPodeliBaranjaBrz). Собран — физички материјал останува на истата локација, само logically се тагира со AssignedProducerId.'
          )}
        </div>
      </div>

      {loading && <div className="loading">{t('common.loading')}</div>}

      {!loading && (
        <>
          {/* Section 1: Unassigned balances */}
          <section style={{ marginBottom: 40 }}>
            <h2 style={{ fontSize: 16 }}>
              {t('podelba.unassigned', 'Нераспределени баланси')}{' '}
              <span style={{ color: '#888', fontSize: 13 }}>({unassigned.length})</span>
            </h2>
            {unassigned.length === 0 ? (
              <div style={{ padding: 20, color: '#888', fontStyle: 'italic' }}>
                {t('podelba.noUnassigned', 'Сите OK баланси се веќе распределени или потрошени.')}
              </div>
            ) : (
              <table className="data-table">
                <thead>
                  <tr>
                    <th>{t('podelba.col.item', 'Артикл')}</th>
                    <th>{t('podelba.col.batch', 'Batch')}</th>
                    <th>{t('podelba.col.mrn', 'MRN')}</th>
                    <th>{t('podelba.col.location', 'Локација')}</th>
                    <th style={{ textAlign: 'right' }}>{t('podelba.col.qty', 'Колич.')}</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {unassigned.map((b) => (
                    <tr key={b.id}>
                      <td>
                        <div style={{ fontWeight: 600 }}>{b.item?.code}</div>
                        <div style={{ fontSize: 12, color: '#888' }}>{b.item?.name}</div>
                      </td>
                      <td>{b.batchNumber || <span style={{ color: '#aaa' }}>—</span>}</td>
                      <td>{b.mrn || <span style={{ color: '#aaa' }}>—</span>}</td>
                      <td>{b.location?.code}</td>
                      <td style={{ textAlign: 'right', fontFamily: 'monospace' }}>
                        {formatQuantity(b.quantity)} {b.uoM?.code}
                      </td>
                      <td>
                        <button className="btn btn-sm btn-primary" onClick={() => openAllocate(b)}>
                          {t('podelba.allocate', 'Распредели')}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>

          {/* Section 2: By producer view */}
          <section>
            <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: 10 }}>
              <h2 style={{ fontSize: 16, margin: 0 }}>
                {t('podelba.byProducer', 'Распоред по производител')}{' '}
                <span style={{ color: '#888', fontSize: 13 }}>({byProducerFiltered.length})</span>
              </h2>
              <select
                value={filterProducer}
                onChange={(e) => setFilterProducer(e.target.value)}
                style={{ padding: 6 }}
              >
                <option value="">{t('podelba.allProducers', 'Сите производители')}</option>
                {producers.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.code} · {p.name}
                  </option>
                ))}
              </select>
            </div>
            {byProducerFiltered.length === 0 ? (
              <div style={{ padding: 20, color: '#888', fontStyle: 'italic' }}>
                {t('podelba.noProducerAssignments', 'Нема активни распределби.')}
              </div>
            ) : (
              <table className="data-table">
                <thead>
                  <tr>
                    <th>{t('podelba.col.producer', 'Производител')}</th>
                    <th>{t('podelba.col.item', 'Артикл')}</th>
                    <th>{t('podelba.col.batch', 'Batch')}</th>
                    <th>{t('podelba.col.mrn', 'MRN')}</th>
                    <th style={{ textAlign: 'right' }}>{t('podelba.col.qty', 'Колич.')}</th>
                    <th>{t('podelba.col.state', 'Состојба')}</th>
                  </tr>
                </thead>
                <tbody>
                  {byProducerFiltered.map((r) => (
                    <tr key={r.id}>
                      <td>
                        <div style={{ fontWeight: 600 }}>{r.producerCode}</div>
                        <div style={{ fontSize: 12, color: '#888' }}>{r.producerName}</div>
                      </td>
                      <td>
                        <div>{r.itemCode}</div>
                        <div style={{ fontSize: 12, color: '#888' }}>{r.itemName}</div>
                      </td>
                      <td>{r.batchNumber || <span style={{ color: '#aaa' }}>—</span>}</td>
                      <td>{r.mrn || <span style={{ color: '#aaa' }}>—</span>}</td>
                      <td style={{ textAlign: 'right', fontFamily: 'monospace' }}>
                        {formatQuantity(r.quantity)}
                      </td>
                      <td>
                        <span style={{ fontSize: 12, color: '#666' }}>
                          {r.lonProcessState === 1
                            ? 'Imported'
                            : r.lonProcessState === 6
                            ? 'InProduction'
                            : r.lonProcessState === 7
                            ? 'Exported'
                            : r.lonProcessState === 9
                            ? 'Waste'
                            : '—'}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>
        </>
      )}

      {source && (
        <div
          onClick={() => !saving && closeAllocate()}
          style={{
            position: 'fixed',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            background: 'rgba(0,0,0,0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
          }}
        >
          <div
            onClick={(e) => e.stopPropagation()}
            style={{
              background: 'white',
              padding: 25,
              borderRadius: 8,
              minWidth: 560,
              maxWidth: 720,
            }}
          >
            <h3 style={{ marginTop: 0 }}>
              {t('podelba.allocateTitle', 'Распредели:')} {source.item?.code}{' '}
              <span style={{ color: '#888', fontSize: 13 }}>
                {formatQuantity(source.quantity)} {source.uoM?.code}
              </span>
            </h3>
            <div style={{ fontSize: 13, color: '#666', marginBottom: 15 }}>
              Batch: {source.batchNumber || '—'} · MRN: {source.mrn || '—'}
            </div>

            {allocations.map((alloc, i) => (
              <div key={i} style={{ display: 'flex', gap: 10, marginBottom: 8, alignItems: 'center' }}>
                <select
                  value={alloc.producerId}
                  onChange={(e) => updateAlloc(i, { producerId: e.target.value })}
                  style={{ flex: 2, padding: 6 }}
                >
                  <option value="">{t('podelba.pickProducer', '-- избери производител --')}</option>
                  {producers.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.code} · {p.name}
                    </option>
                  ))}
                </select>
                <input
                  type="number"
                  step="0.0001"
                  value={alloc.quantity}
                  onChange={(e) => updateAlloc(i, { quantity: parseFloat(e.target.value) || 0 })}
                  style={{ width: 120, padding: 6, textAlign: 'right', fontFamily: 'monospace' }}
                />
                <button
                  className="btn btn-sm btn-outline"
                  onClick={() => removeAlloc(i)}
                  disabled={allocations.length === 1}
                >
                  ×
                </button>
              </div>
            ))}

            <div style={{ display: 'flex', gap: 10, marginTop: 8, alignItems: 'center' }}>
              <button className="btn btn-sm btn-outline" onClick={addAlloc}>
                + {t('podelba.addAlloc', 'Додади линија')}
              </button>
              <button className="btn btn-sm btn-outline" onClick={distributeRemaining}>
                {t('podelba.fillRemaining', 'Стави остаток во последна')}
              </button>
              <div style={{ marginLeft: 'auto', fontSize: 13 }}>
                <span>Σ = {formatQuantity(sumAlloc)}</span>{' '}
                <span style={{ color: Math.abs(remaining) < 0.0001 ? '#155724' : '#b00020', marginLeft: 10 }}>
                  {Math.abs(remaining) < 0.0001
                    ? '✓ OK'
                    : `Δ ${remaining > 0 ? '−' : '+'}${formatQuantity(Math.abs(remaining))}`}
                </span>
              </div>
            </div>

            <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end', marginTop: 20 }}>
              <button className="btn btn-outline" onClick={closeAllocate} disabled={saving}>
                {t('common.cancel', 'Откажи')}
              </button>
              <button
                className="btn btn-primary"
                onClick={submit}
                disabled={saving || Math.abs(remaining) > 0.0001}
              >
                {saving ? t('common.saving', 'Се зачувува...') : t('podelba.confirm', 'Изврши podelba')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default Podelba;
