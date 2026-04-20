import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { wmsApi } from '../../services/api';

const BlockedInventory: React.FC = () => {
  const { t } = useTranslation();
  const [inventory, setInventory] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [filterStatus, setFilterStatus] = useState<number>(2);

  useEffect(() => { loadInventory(); }, []);

  const loadInventory = async () => {
    try {
      setLoading(true);
      const response = await wmsApi.getInventory();
      setInventory(response.data);
    } catch (err) {
      console.error('Failed to load inventory', err);
    } finally {
      setLoading(false);
    }
  };

  const handleReleaseClick = async (inventoryId: string) => {
    const confirm = window.confirm(t('blockedInventory.confirmRelease'));
    if (!confirm) return;

    try {
      await wmsApi.updateQualityStatus({
        inventoryBalanceId: inventoryId,
        newQualityStatus: 1,
        reason: t('blockedInventory.releasedReason'),
        notes: t('blockedInventory.releasedNotes'),
      });
      loadInventory();
    } catch (err: any) {
      alert('Failed to release: ' + (err.response?.data?.error || err.message));
    }
  };

  const filteredInventory = inventory.filter((inv: any) => inv.qualityStatus === filterStatus);

  const calculateAging = (lastMovementDate?: string, createdAt?: string) => {
    const date = lastMovementDate || createdAt;
    if (!date) return 0;
    const diffTime = Math.abs(new Date().getTime() - new Date(date).getTime());
    return Math.ceil(diffTime / (1000 * 60 * 60 * 24));
  };

  const inventoryWithAging = filteredInventory.map((inv: any) => ({
    ...inv,
    agingDays: calculateAging(inv.lastMovementDate, inv.createdAt),
  }));
  const sortedInventory = inventoryWithAging.sort((a, b) => b.agingDays - a.agingDays);

  const totalQuantity = sortedInventory.reduce((sum, inv) => sum + inv.quantity, 0);
  const totalItems = sortedInventory.length;
  const oldItems = sortedInventory.filter((inv) => inv.agingDays > 30).length;
  const criticalItems = sortedInventory.filter((inv) => inv.agingDays > 90).length;

  const getAgingBadge = (days: number) => {
    if (days > 90) return <span className="badge badge-danger">{t('blockedInventory.aging.critical', { days })}</span>;
    if (days > 30) return <span className="badge badge-warning">{t('blockedInventory.aging.old', { days })}</span>;
    return <span className="badge badge-info">{t('blockedInventory.aging.days', { days })}</span>;
  };

  const exportToCsv = () => {
    const headers = [
      t('reports.common.itemCode'), t('reports.common.itemName'), t('reports.common.location'),
      t('reports.common.batch'), t('reports.common.mrn'), t('reports.common.quantity'), 'UoM',
      t('reports.common.qualityStatus'), t('blockedInventory.agingDays'), t('reports.common.lastMovement'),
    ];
    const rows = sortedInventory.map((inv: any) => [
      inv.item?.code || '', inv.item?.name || '', inv.location?.name || '',
      inv.batchNumber || '', inv.mrn || '', inv.quantity.toFixed(2), inv.uoM?.code || '',
      inv.qualityStatus === 2 ? t('qualityStatus.blocked') : t('qualityStatus.quarantine'),
      inv.agingDays,
      inv.lastMovementDate ? new Date(inv.lastMovementDate).toLocaleDateString() : '',
    ]);
    const csv = '\uFEFF' + [headers, ...rows].map(row => row.join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `blocked_inventory_${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
  };

  return (
    <div>
      <div className="header">
        <h2>🔒 {t('reports.blockedInventory.title')}</h2>
        <button onClick={exportToCsv} style={{ background: 'var(--success)', color: 'white', borderColor: 'var(--success)' }}>
          📥 {t('reports.common.exportCsv')}
        </button>
      </div>

      <div style={{ background: 'var(--warning-bg)', border: '1px solid var(--warning)', borderRadius: 8, padding: 12, marginBottom: 16 }}>
        <strong>⚠️ {t('blockedInventory.warningTitle')}</strong><br />
        {t('blockedInventory.warningBody')}
      </div>

      <div style={{ marginBottom: 16 }}>
        <label style={{ marginRight: 10 }}>{t('reports.common.qualityStatus')}:</label>
        <select
          style={{ width: 220, display: 'inline-block' }}
          value={filterStatus}
          onChange={(e) => setFilterStatus(parseInt(e.target.value))}
        >
          <option value="2">{t('qualityStatus.blocked')}</option>
          <option value="3">{t('qualityStatus.quarantine')}</option>
        </select>
      </div>

      <div className="card-grid">
        <div className="card danger">
          <h3>{t('blockedInventory.summary.onHold')}</h3>
          <div className="value">{totalItems}</div>
        </div>
        <div className="card warning">
          <h3>{t('blockedInventory.summary.over30')}</h3>
          <div className="value">{oldItems}</div>
        </div>
        <div className="card danger">
          <h3>{t('blockedInventory.summary.critical90')}</h3>
          <div className="value">{criticalItems}</div>
        </div>
        <div className="card info">
          <h3>{t('reports.common.totalQuantity')}</h3>
          <div className="value">{totalQuantity.toFixed(0)}</div>
        </div>
      </div>

      {loading ? (
        <div className="loading">{t('reports.common.loading')}</div>
      ) : (
        <>
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>{t('reports.common.itemCode')}</th>
                  <th>{t('reports.common.itemName')}</th>
                  <th>{t('reports.common.location')}</th>
                  <th>{t('reports.common.batch')}</th>
                  <th>{t('reports.common.mrn')}</th>
                  <th>{t('reports.common.quantity')}</th>
                  <th>{t('reports.common.qualityStatus')}</th>
                  <th>{t('blockedInventory.aging.label')}</th>
                  <th>{t('reports.common.lastMovement')}</th>
                  <th>{t('common.actions')}</th>
                </tr>
              </thead>
              <tbody>
                {sortedInventory.length === 0 ? (
                  <tr>
                    <td colSpan={10} style={{ textAlign: 'center', padding: 20 }}>
                      ✅ {t('blockedInventory.none', { status: filterStatus === 2 ? t('qualityStatus.blocked') : t('qualityStatus.quarantine') })}
                    </td>
                  </tr>
                ) : (
                  sortedInventory.map((inv: any, idx: number) => (
                    <tr key={idx}>
                      <td><strong>{inv.item?.code}</strong></td>
                      <td>{inv.item?.name}</td>
                      <td>{inv.location?.name}</td>
                      <td>{inv.batchNumber || '-'}</td>
                      <td>{inv.mrn || '-'}</td>
                      <td><strong>{inv.quantity.toFixed(2)}</strong> {inv.uoM?.code}</td>
                      <td>
                        <span className={`badge badge-${inv.qualityStatus === 2 ? 'danger' : 'warning'}`}>
                          {inv.qualityStatus === 2 ? t('qualityStatus.blocked') : t('qualityStatus.quarantine')}
                        </span>
                      </td>
                      <td>{getAgingBadge(inv.agingDays)}</td>
                      <td>{inv.lastMovementDate ? new Date(inv.lastMovementDate).toLocaleDateString() : '-'}</td>
                      <td>
                        <button
                          onClick={() => handleReleaseClick(inv.id)}
                          style={{ background: 'var(--success)', color: 'white', borderColor: 'var(--success)' }}
                        >
                          ✅ {t('blockedInventory.release')}
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {sortedInventory.length > 0 && (
            <div style={{ background: 'var(--info-bg)', border: '1px solid var(--taris-blue-100)', borderRadius: 8, padding: 12, marginTop: 16 }}>
              <strong>💡 {t('blockedInventory.tip')}</strong> {t('blockedInventory.tipBody')}
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default BlockedInventory;
