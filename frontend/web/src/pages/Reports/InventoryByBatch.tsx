import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { wmsApi } from '../../services/api';

const InventoryByBatch: React.FC = () => {
  const { t } = useTranslation();
  const [inventory, setInventory] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchBatch, setSearchBatch] = useState('');

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

  const inventoryByBatch = inventory
    .filter((inv: any) => inv.batchNumber)
    .reduce((acc: any, inv: any) => {
      const batch = inv.batchNumber;
      if (!acc[batch]) {
        acc[batch] = {
          batchNumber: batch,
          items: [],
          totalQuantity: 0,
          locations: new Set<string>(),
          mrns: new Set<string>(),
          itemCodes: new Set<string>(),
        };
      }
      acc[batch].items.push(inv);
      acc[batch].totalQuantity += inv.quantity;
      if (inv.location?.name) acc[batch].locations.add(inv.location.name);
      if (inv.mrn) acc[batch].mrns.add(inv.mrn);
      if (inv.item?.code) acc[batch].itemCodes.add(inv.item.code);
      return acc;
    }, {});

  const batchList = Object.values(inventoryByBatch).sort((a: any, b: any) =>
    b.batchNumber.localeCompare(a.batchNumber)
  );

  const filteredBatchList = searchBatch
    ? batchList.filter((batch: any) => batch.batchNumber.toLowerCase().includes(searchBatch.toLowerCase()))
    : batchList;

  const totalBatches = filteredBatchList.length;
  const totalQuantity = filteredBatchList.reduce((sum: number, b: any) => sum + b.totalQuantity, 0);
  const activeBatches = filteredBatchList.filter((b: any) => b.totalQuantity > 0).length;

  const exportToCsv = () => {
    const headers = [
      t('reports.common.batch'),
      t('reports.common.totalQty'),
      t('reports.common.location'),
      t('reports.common.mrn'),
      t('inventoryByBatch.lines'),
      t('reports.common.itemCode'),
    ];
    const rows = filteredBatchList.map((batch: any) => [
      batch.batchNumber,
      batch.totalQuantity.toFixed(2),
      Array.from(batch.locations as Set<string>).join('; '),
      Array.from(batch.mrns as Set<string>).join('; '),
      batch.items.length,
      Array.from(batch.itemCodes as Set<string>).join('; '),
    ]);
    const csv = '\uFEFF' + [headers, ...rows].map(r => r.join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `inventory_by_batch_${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
  };

  return (
    <div>
      <div className="header">
        <h2>🏷️ {t('reports.inventoryByBatch.title')}</h2>
        <button onClick={exportToCsv} style={{ background: 'var(--success)', color: 'white', borderColor: 'var(--success)' }}>
          📥 {t('reports.common.exportCsv')}
        </button>
      </div>

      <div style={{ background: 'var(--info-bg)', border: '1px solid var(--taris-blue-100)', borderRadius: 8, padding: 12, marginBottom: 16 }}>
        <strong>ℹ️ {t('inventoryByBatch.subtitle')}</strong><br />
        {t('inventoryByBatch.description')}
      </div>

      <div style={{ marginBottom: 16 }}>
        <label style={{ marginRight: 10 }}>{t('inventoryByBatch.searchLabel')}:</label>
        <input
          type="text"
          style={{ width: 320, display: 'inline-block' }}
          placeholder={t('inventoryByBatch.searchPlaceholder') ?? ''}
          value={searchBatch}
          onChange={(e) => setSearchBatch(e.target.value)}
        />
      </div>

      <div className="card-grid">
        <div className="card info">
          <h3>{t('inventoryByBatch.totalBatches')}</h3>
          <div className="value">{totalBatches}</div>
        </div>
        <div className="card success">
          <h3>{t('inventoryByBatch.activeBatches')}</h3>
          <div className="value">{activeBatches}</div>
        </div>
        <div className="card warning">
          <h3>{t('reports.common.totalQuantity')}</h3>
          <div className="value">{totalQuantity.toFixed(0)}</div>
        </div>
      </div>

      {loading ? (
        <div className="loading">{t('reports.common.loading')}</div>
      ) : (
        <>
          {filteredBatchList.map((batchData: any) => (
            <div key={batchData.batchNumber} style={{ marginBottom: 20 }}>
              <div style={{ background: 'var(--success-bg)', padding: 14, borderRadius: 8, marginBottom: 10, border: '1px solid rgba(22,163,74,0.2)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 10 }}>
                  <div>
                    <strong style={{ fontSize: 16 }}>🏷️ {t('reports.common.batch')}: {batchData.batchNumber}</strong>
                    {batchData.mrns.size > 0 && (
                      <span style={{ marginLeft: 10, color: 'var(--ink-600)' }}>
                        {t('reports.common.mrn')}: {Array.from(batchData.mrns).join(', ')}
                      </span>
                    )}
                  </div>
                  <div style={{ fontSize: 13, color: 'var(--ink-700)' }}>
                    <strong>{t('reports.common.totalQty')}:</strong> {batchData.totalQuantity.toFixed(2)}
                    <span style={{ marginLeft: 10 }}><strong>{t('reports.common.allLocations').replace(/ .*/, '')}:</strong> {batchData.locations.size}</span>
                    <span style={{ marginLeft: 10 }}><strong>{t('inventoryByBatch.lines')}:</strong> {batchData.items.length}</span>
                  </div>
                </div>
              </div>

              <div className="table-container">
                <table>
                  <thead>
                    <tr>
                      <th>{t('reports.common.itemCode')}</th>
                      <th>{t('reports.common.itemName')}</th>
                      <th>{t('reports.common.location')}</th>
                      <th>{t('reports.common.mrn')}</th>
                      <th>{t('reports.common.quantity')}</th>
                      <th>{t('reports.common.qualityStatus')}</th>
                      <th>{t('reports.common.lastMovement')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {batchData.items.map((inv: any, idx: number) => (
                      <tr key={idx}>
                        <td><strong>{inv.item?.code}</strong></td>
                        <td>{inv.item?.name}</td>
                        <td>{inv.location?.name}</td>
                        <td>{inv.mrn || '-'}</td>
                        <td><strong>{inv.quantity.toFixed(2)}</strong> {inv.uoM?.code}</td>
                        <td>
                          <span className={`badge badge-${inv.qualityStatus === 1 ? 'success' : inv.qualityStatus === 2 ? 'danger' : 'warning'}`}>
                            {inv.qualityStatus === 1 ? t('qualityStatus.ok') : inv.qualityStatus === 2 ? t('qualityStatus.blocked') : t('qualityStatus.quarantine')}
                          </span>
                        </td>
                        <td>{inv.lastMovementDate ? new Date(inv.lastMovementDate).toLocaleDateString() : '-'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ))}

          {filteredBatchList.length === 0 && (
            <div className="card" style={{ textAlign: 'center', color: 'var(--ink-500)' }}>
              {t('inventoryByBatch.noResults')} {searchBatch && t('inventoryByBatch.tryDifferent')}
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default InventoryByBatch;
