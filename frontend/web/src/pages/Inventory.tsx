import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { useQueryClient } from '@tanstack/react-query';
import {
  inventoryKeys,
  useAllLocationsQuery,
  useBulkMoveBalances,
  useInventoryQuery,
  useQualityStatusChange,
  type InventoryRow,
} from '../hooks/queries/useInventory';
import ReceiptForm from '../components/WMS/ReceiptForm';
import TransferForm from '../components/WMS/TransferForm';
import ShipmentForm from '../components/WMS/ShipmentForm';
import CycleCountForm from '../components/WMS/CycleCountForm';
import AdjustmentForm from '../components/WMS/AdjustmentForm';
import QualityStatusChangeForm from '../components/WMS/QualityStatusChangeForm';
import MoveBatchModal from '../components/WMS/MoveBatchModal';
import BulkActionBar, { BulkAction } from '../components/common/BulkActionBar';
import SearchableSelect, { SearchableOption } from '../components/common/SearchableSelect';
import { useRowSelection } from '../hooks/useRowSelection';
import { exportToCsv } from '../utils/export';
import { translateError } from '../utils/translateError';

const QC_OK = 1;
const QC_BLOCKED = 2;
const QC_QUARANTINE = 3;

const Inventory: React.FC = () => {
  const { t } = useTranslation();
  const qc = useQueryClient();

  const { data: inventory = [], isLoading: loading } = useInventoryQuery();
  const { data: allLocations = [] } = useAllLocationsQuery();
  const bulkMoveMutation = useBulkMoveBalances();
  const qcStatusMutation = useQualityStatusChange();

  const [activeForm, setActiveForm] = useState<string | null>(null);
  const [moveBatchRow, setMoveBatchRow] = useState<InventoryRow | null>(null);

  // Filters
  const [fItem, setFItem] = useState('');
  const [fLocation, setFLocation] = useState('');
  const [fBatch, setFBatch] = useState('');
  const [fMrn, setFMrn] = useState('');
  const [fQc, setFQc] = useState<string>('');

  // Bulk QC modal
  const [bulkQcModal, setBulkQcModal] = useState<null | { target: number; reason: string }>(null);
  const [bulkRunning, setBulkRunning] = useState(false);

  // Bulk move modal
  const [bulkMoveModal, setBulkMoveModal] = useState<null | { targetLocationId: string; reason: string }>(null);

  const refreshInventory = () => qc.invalidateQueries({ queryKey: inventoryKeys.all });

  const handleFormSuccess = () => {
    setActiveForm(null);
    refreshInventory();
  };

  const handleFormCancel = () => {
    setActiveForm(null);
  };

  // Derived filter options — only values that actually exist in current data.
  const locationOptions = useMemo<SearchableOption[]>(() => {
    const map = new Map<string, string>();
    inventory.forEach((r) => {
      if (r.location?.id && r.location?.code) {
        map.set(r.location.id, `${r.location.code}${r.location.name ? ' · ' + r.location.name : ''}`);
      }
    });
    return Array.from(map.entries())
      .sort((a, b) => a[1].localeCompare(b[1]))
      .map(([id, label]) => ({ value: id, label }));
  }, [inventory]);

  const batchOptions = useMemo<SearchableOption[]>(() => {
    const set = new Set<string>();
    inventory.forEach((r) => r.batchNumber && set.add(r.batchNumber));
    return Array.from(set)
      .sort()
      .map((b) => ({ value: b, label: b }));
  }, [inventory]);

  const mrnOptions = useMemo<SearchableOption[]>(() => {
    const set = new Set<string>();
    inventory.forEach((r) => r.mrn && set.add(r.mrn));
    return Array.from(set)
      .sort()
      .map((m) => ({ value: m, label: m }));
  }, [inventory]);

  // Filtered rows.
  const filtered = useMemo(() => {
    const iq = fItem.trim().toLowerCase();
    return inventory.filter((r) => {
      if (iq) {
        const hay = `${r.item?.code ?? ''} ${r.item?.name ?? ''}`.toLowerCase();
        if (!hay.includes(iq)) return false;
      }
      if (fLocation && r.location?.id !== fLocation) return false;
      if (fBatch && r.batchNumber !== fBatch) return false;
      if (fMrn && r.mrn !== fMrn) return false;
      if (fQc) {
        if (r.qualityStatus !== Number(fQc)) return false;
      }
      return true;
    });
  }, [inventory, fItem, fLocation, fBatch, fMrn, fQc]);

  const selection = useRowSelection(filtered, (r) => r.id);
  const selectedQty = selection.selectedRows.reduce((s, r) => s + r.quantity, 0);

  const clearFilters = () => {
    setFItem('');
    setFLocation('');
    setFBatch('');
    setFMrn('');
    setFQc('');
  };

  const hasAnyFilter = !!fItem || !!fLocation || !!fBatch || !!fMrn || !!fQc;

  async function runBulkMove(targetLocationId: string, reason: string) {
    const ids = selection.selectedRows.map((r) => r.id);
    if (ids.length === 0) return;
    setBulkRunning(true);
    try {
      const env = (await bulkMoveMutation.mutateAsync({
        balanceIds: ids,
        targetLocationId,
        reason: reason || null,
      })) as { isSuccess?: boolean; data?: { balancesMoved: number; balancesSkipped: number; totalQuantityMoved: number }; errorMessage?: string; errorCode?: string };
      if (env && env.isSuccess === false) {
        toast.error(translateError(env));
      } else {
        const data = env.data ?? (env as any);
        toast.success(
          t('inventory.bulkMove.success', {
            moved: data.balancesMoved,
            skipped: data.balancesSkipped ?? 0,
            qty: Number((data.totalQuantityMoved ?? 0).toFixed(2)),
          }) as string
        );
      }
    } catch (err: any) {
      toast.error(translateError(err));
    } finally {
      setBulkRunning(false);
      setBulkMoveModal(null);
      selection.clear();
    }
  }

  async function runBulkQc(target: number, reason: string) {
    const rows = selection.selectedRows;
    if (rows.length === 0) return;
    setBulkRunning(true);
    let ok = 0;
    let failed = 0;
    const firstError: string[] = [];
    for (const r of rows) {
      try {
        await qcStatusMutation.mutateAsync({
          inventoryBalanceId: r.id,
          newQualityStatus: target,
          reason,
        });
        ok++;
      } catch (err: any) {
        failed++;
        if (firstError.length === 0) firstError.push(translateError(err));
      }
    }
    setBulkRunning(false);
    setBulkQcModal(null);
    selection.clear();
    if (failed === 0) {
      toast.success(t('inventory.bulkQc.successAll', { count: ok }) as string);
    } else {
      toast.error(
        t('inventory.bulkQc.partial', { ok, failed, first: firstError[0] ?? '' }) as string
      );
    }
  }

  function exportSelectedCsv(rows: InventoryRow[]) {
    exportToCsv(
      rows,
      [
        { key: 'code', label: t('inventory.columns.itemCode') as string, get: (r: InventoryRow) => r.item?.code ?? '' },
        { key: 'name', label: t('inventory.columns.itemName') as string, get: (r: InventoryRow) => r.item?.name ?? '' },
        { key: 'location', label: t('inventory.columns.location') as string, get: (r: InventoryRow) => r.location?.code ?? '' },
        { key: 'batchNumber', label: t('inventory.columns.batch') as string },
        { key: 'mrn', label: t('inventory.columns.mrn') as string },
        { key: 'quantity', label: t('inventory.columns.quantity') as string, type: 'number' },
        { key: 'uomCode', label: 'UoM', get: (r: InventoryRow) => r.uoM?.code ?? '' },
        {
          key: 'qualityStatus',
          label: t('inventory.columns.qualityStatus') as string,
          get: (r: InventoryRow) =>
            r.qualityStatus === QC_OK
              ? (t('qualityStatus.ok') as string)
              : r.qualityStatus === QC_BLOCKED
                ? (t('qualityStatus.blocked') as string)
                : (t('qualityStatus.quarantine') as string),
        },
      ],
      'inventory'
    );
  }

  const bulkActions: BulkAction[] = [
    {
      key: 'move',
      label: t('inventory.bulkMove.action') as string,
      variant: 'primary',
      onClick: () => setBulkMoveModal({ targetLocationId: '', reason: '' }),
    },
    {
      key: 'export',
      label: t('common.exportExcel') as string,
      onClick: () => exportSelectedCsv(selection.selectedRows),
    },
    {
      key: 'qc-blocked',
      label: t('inventory.bulkQc.blockAction') as string,
      variant: 'danger',
      onClick: () => setBulkQcModal({ target: QC_BLOCKED, reason: '' }),
    },
    {
      key: 'qc-ok',
      label: t('inventory.bulkQc.releaseAction') as string,
      onClick: () => setBulkQcModal({ target: QC_OK, reason: '' }),
    },
  ];

  const moveLocationOptions = useMemo<SearchableOption[]>(() => {
    return allLocations
      .map((l) => ({ value: l.id, label: l.name, hint: l.code }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }, [allLocations]);

  // Render active form (full-page mode)
  if (activeForm === 'receipt') {
    return <ReceiptForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }
  if (activeForm === 'transfer') {
    return <TransferForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }
  if (activeForm === 'shipment') {
    return <ShipmentForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }
  if (activeForm === 'cyclecount') {
    return <CycleCountForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }
  if (activeForm === 'adjustment') {
    return <AdjustmentForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }
  if (activeForm === 'qualitychange') {
    return <QualityStatusChangeForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }

  if (loading) return <div className="loading">{t('inventory.loading')}</div>;

  return (
    <div>
      <div className="header">
        <h2>📦 {t('inventory.title')}</h2>
        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
          <button onClick={() => setActiveForm('receipt')} style={{ background: 'var(--success)', color: 'white', borderColor: 'var(--success)' }}>
            ➕ {t('inventory.actions.receipt')}
          </button>
          <button className="btn-primary" onClick={() => setActiveForm('transfer')}>
            🔄 {t('inventory.actions.transfer')}
          </button>
          <button onClick={() => setActiveForm('shipment')} style={{ background: 'var(--info)', color: 'white', borderColor: 'var(--info)' }}>
            📤 {t('inventory.actions.shipment')}
          </button>
          <button onClick={() => setActiveForm('cyclecount')} style={{ background: 'var(--warning)', color: 'white', borderColor: 'var(--warning)' }}>
            📊 {t('inventory.actions.cycleCount')}
          </button>
          <button onClick={() => setActiveForm('adjustment')}>
            ⚙️ {t('inventory.actions.adjustment')}
          </button>
          <button onClick={() => setActiveForm('qualitychange')} style={{ background: 'var(--danger)', color: 'white', borderColor: 'var(--danger)' }}>
            🔒 {t('inventory.actions.qualityStatus')}
          </button>
        </div>
      </div>

      {/* Filter bar */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
          gap: 8,
          padding: 10,
          background: 'var(--ink-50, #f8fafc)',
          border: '1px solid var(--border, #e5e7eb)',
          borderRadius: 6,
          marginBottom: 10,
        }}
      >
        <input
          type="text"
          value={fItem}
          onChange={(e) => setFItem(e.target.value)}
          placeholder={t('inventory.filters.itemPlaceholder') as string}
          style={{ padding: 6 }}
        />
        <SearchableSelect
          value={fLocation}
          onChange={(v) => setFLocation(v)}
          options={locationOptions}
          placeholder={t('inventory.filters.locationPlaceholder') as string}
        />
        <SearchableSelect
          value={fBatch}
          onChange={(v) => setFBatch(v)}
          options={batchOptions}
          placeholder={t('inventory.filters.batchPlaceholder') as string}
        />
        <SearchableSelect
          value={fMrn}
          onChange={(v) => setFMrn(v)}
          options={mrnOptions}
          placeholder={t('inventory.filters.mrnPlaceholder') as string}
        />
        <select
          value={fQc}
          onChange={(e) => setFQc(e.target.value)}
          style={{ padding: 6 }}
        >
          <option value="">{t('inventory.filters.qcAll')}</option>
          <option value={String(QC_OK)}>{t('qualityStatus.ok')}</option>
          <option value={String(QC_BLOCKED)}>{t('qualityStatus.blocked')}</option>
          <option value={String(QC_QUARANTINE)}>{t('qualityStatus.quarantine')}</option>
        </select>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <span style={{ fontSize: 12, color: '#666' }}>
            {t('inventory.filters.showing', { count: filtered.length, total: inventory.length })}
          </span>
          {hasAnyFilter && (
            <button
              type="button"
              onClick={clearFilters}
              style={{ padding: '4px 10px', fontSize: 12 }}
            >
              {t('inventory.filters.clear')}
            </button>
          )}
        </div>
      </div>

      <BulkActionBar
        selectedCount={selection.count}
        totalCount={filtered.length}
        actions={bulkActions}
        onClearSelection={selection.clear}
        summary={
          selection.count > 0
            ? (t('inventory.bulkSummary', { qty: selectedQty.toFixed(2) }) as string)
            : undefined
        }
      />

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th style={{ width: 34 }}>
                <input
                  type="checkbox"
                  checked={selection.allVisibleSelected}
                  ref={(el) => {
                    if (el) el.indeterminate = selection.someVisibleSelected;
                  }}
                  onChange={selection.toggleAllVisible}
                  aria-label={t('bulkActions.selectAll') as string}
                />
              </th>
              <th>{t('inventory.columns.itemCode')}</th>
              <th>{t('inventory.columns.itemName')}</th>
              <th>{t('inventory.columns.location')}</th>
              <th>{t('inventory.columns.batch')}</th>
              <th>{t('inventory.columns.mrn')}</th>
              <th>{t('inventory.columns.quantity')}</th>
              <th>{t('inventory.columns.qualityStatus')}</th>
              <th>{t('common.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 && (
              <tr>
                <td colSpan={9} style={{ textAlign: 'center', padding: 20, color: '#888' }}>
                  {hasAnyFilter ? t('inventory.filters.noResults') : t('common.noData')}
                </td>
              </tr>
            )}
            {filtered.map((inv) => (
              <tr
                key={inv.id}
                style={{
                  background: selection.isSelected(inv.id) ? 'var(--taris-blue-50, #e7f2fe)' : undefined,
                }}
              >
                <td>
                  <input
                    type="checkbox"
                    checked={selection.isSelected(inv.id)}
                    onChange={() => selection.toggle(inv.id)}
                    aria-label={t('bulkActions.selectRow') as string}
                  />
                </td>
                <td>{inv.item?.code}</td>
                <td>{inv.item?.name}</td>
                <td>{inv.location?.name}</td>
                <td>{inv.batchNumber || '-'}</td>
                <td>{inv.mrn || '-'}</td>
                <td>{inv.quantity.toFixed(2)} {inv.uoM?.code}</td>
                <td>
                  <span className={`badge badge-${
                    inv.qualityStatus === QC_OK ? 'success' :
                    inv.qualityStatus === QC_BLOCKED ? 'danger' : 'warning'
                  }`}>
                    {inv.qualityStatus === QC_OK ? t('qualityStatus.ok') :
                     inv.qualityStatus === QC_BLOCKED ? t('qualityStatus.blocked') : t('qualityStatus.quarantine')}
                  </span>
                </td>
                <td>
                  {inv.batchNumber && inv.quantity > 0 && (
                    <button
                      onClick={() => setMoveBatchRow(inv)}
                      style={{ fontSize: 12, padding: '2px 8px' }}
                    >
                      🔀 {t('moveBatch.button')}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {moveBatchRow && (
        <MoveBatchModal
          defaultBatchNumber={moveBatchRow.batchNumber ?? ''}
          defaultWarehouseId={moveBatchRow.location?.warehouseId}
          onCancel={() => setMoveBatchRow(null)}
          onSuccess={(summary) => {
            setMoveBatchRow(null);
            toast.success(
              t('moveBatch.success', {
                count: summary.balancesMoved,
                qty: summary.totalQty,
              })
            );
            refreshInventory();
          }}
        />
      )}

      {bulkMoveModal && (
        <div
          onClick={() => !bulkRunning && setBulkMoveModal(null)}
          style={{
            position: 'fixed',
            inset: 0,
            background: 'rgba(15,23,42,0.4)',
            zIndex: 200,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <div
            onClick={(e) => e.stopPropagation()}
            style={{
              background: 'white',
              borderRadius: 8,
              padding: 20,
              minWidth: 420,
              maxWidth: 520,
              boxShadow: '0 10px 30px rgba(15,23,42,0.2)',
            }}
          >
            <h3 style={{ marginTop: 0 }}>{t('inventory.bulkMove.title')}</h3>
            <p style={{ color: '#555' }}>
              {t('inventory.bulkMove.intro', {
                count: selection.count,
                qty: selectedQty.toFixed(2),
              })}
            </p>
            <label style={{ display: 'block', marginTop: 12 }}>
              <div style={{ fontSize: 12, color: '#666', marginBottom: 4 }}>
                {t('inventory.bulkMove.targetLabel')}
              </div>
              <SearchableSelect
                value={bulkMoveModal.targetLocationId}
                onChange={(v) => setBulkMoveModal({ ...bulkMoveModal, targetLocationId: v })}
                options={moveLocationOptions}
                placeholder={t('inventory.bulkMove.targetPlaceholder') as string}
                disabled={bulkRunning}
              />
            </label>
            <label style={{ display: 'block', marginTop: 12 }}>
              <div style={{ fontSize: 12, color: '#666', marginBottom: 4 }}>
                {t('inventory.bulkMove.reasonLabel')}
              </div>
              <textarea
                value={bulkMoveModal.reason}
                onChange={(e) => setBulkMoveModal({ ...bulkMoveModal, reason: e.target.value })}
                rows={2}
                style={{ width: '100%', padding: 6 }}
                placeholder={t('inventory.bulkMove.reasonPlaceholder') as string}
                disabled={bulkRunning}
              />
            </label>
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 16 }}>
              <button
                type="button"
                onClick={() => setBulkMoveModal(null)}
                disabled={bulkRunning}
                style={{ padding: '6px 14px' }}
              >
                {t('common.cancel')}
              </button>
              <button
                type="button"
                onClick={() => runBulkMove(bulkMoveModal.targetLocationId, bulkMoveModal.reason.trim())}
                disabled={bulkRunning || !bulkMoveModal.targetLocationId}
                style={{
                  padding: '6px 14px',
                  background: 'var(--taris-blue-500, #1e88e5)',
                  color: 'white',
                  border: 'none',
                  borderRadius: 4,
                }}
              >
                {bulkRunning ? t('common.saving') : t('inventory.bulkMove.confirm')}
              </button>
            </div>
          </div>
        </div>
      )}

      {bulkQcModal && (
        <div
          onClick={() => !bulkRunning && setBulkQcModal(null)}
          style={{
            position: 'fixed',
            inset: 0,
            background: 'rgba(15,23,42,0.4)',
            zIndex: 200,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <div
            onClick={(e) => e.stopPropagation()}
            style={{
              background: 'white',
              borderRadius: 8,
              padding: 20,
              minWidth: 380,
              maxWidth: 480,
              boxShadow: '0 10px 30px rgba(15,23,42,0.2)',
            }}
          >
            <h3 style={{ marginTop: 0 }}>
              {bulkQcModal.target === QC_BLOCKED
                ? t('inventory.bulkQc.blockTitle')
                : t('inventory.bulkQc.releaseTitle')}
            </h3>
            <p style={{ color: '#555' }}>
              {t('inventory.bulkQc.intro', {
                count: selection.count,
                qty: selectedQty.toFixed(2),
              })}
            </p>
            <label style={{ display: 'block', marginTop: 12 }}>
              <div style={{ fontSize: 12, color: '#666', marginBottom: 4 }}>
                {t('inventory.bulkQc.reasonLabel')}
              </div>
              <textarea
                value={bulkQcModal.reason}
                onChange={(e) => setBulkQcModal({ ...bulkQcModal, reason: e.target.value })}
                rows={3}
                style={{ width: '100%', padding: 6 }}
                placeholder={t('inventory.bulkQc.reasonPlaceholder') as string}
                disabled={bulkRunning}
              />
            </label>
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 16 }}>
              <button
                type="button"
                onClick={() => setBulkQcModal(null)}
                disabled={bulkRunning}
                style={{ padding: '6px 14px' }}
              >
                {t('common.cancel')}
              </button>
              <button
                type="button"
                onClick={() => runBulkQc(bulkQcModal.target, bulkQcModal.reason.trim())}
                disabled={bulkRunning || !bulkQcModal.reason.trim()}
                style={{
                  padding: '6px 14px',
                  background: bulkQcModal.target === QC_BLOCKED ? 'var(--taris-red-500, #e53935)' : 'var(--taris-blue-500, #1e88e5)',
                  color: 'white',
                  border: 'none',
                  borderRadius: 4,
                }}
              >
                {bulkRunning ? t('common.saving') : t('inventory.bulkQc.confirm')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default Inventory;
