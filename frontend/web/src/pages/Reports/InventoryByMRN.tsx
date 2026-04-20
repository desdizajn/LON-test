import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { wmsApi } from '../../services/api';

const InventoryByMRN: React.FC = () => {
  const { t } = useTranslation();
  const [inventory, setInventory] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [filterStatus, setFilterStatus] = useState<'all' | 'active' | 'depleted'>('active');

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

  const inventoryByMRN = inventory
    .filter((inv: any) => inv.mrn)
    .reduce((acc: any, inv: any) => {
      const mrn = inv.mrn;
      if (!acc[mrn]) {
        acc[mrn] = { mrn, items: [], totalQuantity: 0, locations: new Set<string>(), batches: new Set<string>() };
      }
      acc[mrn].items.push(inv);
      acc[mrn].totalQuantity += inv.quantity;
      if (inv.location?.name) acc[mrn].locations.add(inv.location.name);
      if (inv.batchNumber) acc[mrn].batches.add(inv.batchNumber);
      return acc;
    }, {});

  const mrnList = Object.values(inventoryByMRN).sort((a: any, b: any) => b.totalQuantity - a.totalQuantity);

  const filteredMRNList = mrnList.filter((mrn: any) => {
    if (filterStatus === 'active') return mrn.totalQuantity > 0;
    if (filterStatus === 'depleted') return mrn.totalQuantity === 0;
    return true;
  });

  const totalMRNs = filteredMRNList.length;
  const totalQuantity = filteredMRNList.reduce((sum: number, mrn: any) => sum + mrn.totalQuantity, 0);
  const activeMRNs = mrnList.filter((mrn: any) => mrn.totalQuantity > 0).length;
  const depletedMRNs = mrnList.filter((mrn: any) => mrn.totalQuantity === 0).length;

  const exportToCsv = () => {
    const headers = [
      t('reports.common.mrn'), t('reports.common.totalQty'),
      t('reports.common.location'), t('reports.common.batch'),
      t('inventoryByMrn.itemCount'), t('reports.common.qualityStatus'),
    ];
    const rows = filteredMRNList.map((mrn: any) => [
      mrn.mrn,
      mrn.totalQuantity.toFixed(2),
      Array.from(mrn.locations as Set<string>).join('; '),
      Array.from(mrn.batches as Set<string>).join('; '),
      mrn.items.length,
      mrn.totalQuantity > 0 ? t('inventoryByMrn.active') : t('inventoryByMrn.depleted'),
    ]);
    const csv = '\uFEFF' + [headers, ...rows].map(r => r.join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `inventory_by_mrn_${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
  };

  return (
    <div>
      <div className="header">
        <h2>🛃 {t('reports.inventoryByMrn.title')}</h2>
        <button onClick={exportToCsv} style={{ background: 'var(--success)', color: 'white', borderColor: 'var(--success)' }}>
          📥 {t('reports.common.exportCsv')}
        </button>
      </div>

      <div style={{ background: 'var(--info-bg)', border: '1px solid var(--taris-blue-100)', borderRadius: 8, padding: 12, marginBottom: 16 }}>
        <strong>ℹ️ {t('inventoryByMrn.criticalHeader')}</strong><br />
        {t('inventoryByMrn.description')}
      </div>

      <div style={{ marginBottom: 16 }}>
        <label style={{ marginRight: 10 }}>{t('reports.common.filters')}:</label>
        <select style={{ width: 220, display: 'inline-block' }} value={filterStatus} onChange={(e) => setFilterStatus(e.target.value as any)}>
          <option value="all">{t('inventoryByMrn.allMrns')}</option>
          <option value="active">{t('inventoryByMrn.activeOnly')}</option>
          <option value="depleted">{t('inventoryByMrn.depletedOnly')}</option>
        </select>
      </div>

      <div className="card-grid">
        <div className="card info"><h3>{t('inventoryByMrn.totalMrns')}</h3><div className="value">{totalMRNs}</div></div>
        <div className="card success"><h3>{t('inventoryByMrn.activeMrns')}</h3><div className="value">{activeMRNs}</div></div>
        <div className="card danger"><h3>{t('inventoryByMrn.depletedMrns')}</h3><div className="value">{depletedMRNs}</div></div>
        <div className="card warning"><h3>{t('reports.common.totalQuantity')}</h3><div className="value">{totalQuantity.toFixed(0)}</div></div>
      </div>

      {loading ? (
        <div className="loading">{t('reports.common.loading')}</div>
      ) : (
        <>
          {filteredMRNList.map((mrnData: any) => {
            const active = mrnData.totalQuantity > 0;
            return (
              <div key={mrnData.mrn} style={{ marginBottom: 20 }}>
                <div style={{
                  background: active ? 'var(--success-bg)' : 'var(--danger-bg)',
                  padding: 14, borderRadius: 8, marginBottom: 10,
                  border: `1px solid ${active ? 'rgba(22,163,74,0.2)' : 'rgba(220,38,38,0.2)'}`,
                }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 10 }}>
                    <div>
                      <strong style={{ fontSize: 16 }}>🛃 MRN: {mrnData.mrn}</strong>
                      <span className={`badge ${active ? 'badge-success' : 'badge-danger'}`} style={{ marginLeft: 10 }}>
                        {active ? t('inventoryByMrn.active') : t('inventoryByMrn.depleted')}
                      </span>
                    </div>
                    <div style={{ fontSize: 13, color: 'var(--ink-700)' }}>
                      <strong>{t('reports.common.totalQty')}:</strong> {mrnData.totalQuantity.toFixed(2)}
                      <span style={{ marginLeft: 10 }}><strong>{t('reports.common.location')}:</strong> {mrnData.locations.size}</span>
                      <span style={{ marginLeft: 10 }}><strong>{t('reports.common.batch')}:</strong> {mrnData.batches.size}</span>
                      <span style={{ marginLeft: 10 }}><strong>{t('inventoryByMrn.items')}:</strong> {mrnData.items.length}</span>
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
                        <th>{t('reports.common.batch')}</th>
                        <th>{t('reports.common.quantity')}</th>
                        <th>{t('reports.common.qualityStatus')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {mrnData.items.map((inv: any, idx: number) => (
                        <tr key={idx}>
                          <td><strong>{inv.item?.code}</strong></td>
                          <td>{inv.item?.name}</td>
                          <td>{inv.location?.name}</td>
                          <td>{inv.batchNumber || '-'}</td>
                          <td><strong>{inv.quantity.toFixed(2)}</strong> {inv.uoM?.code}</td>
                          <td>
                            <span className={`badge badge-${inv.qualityStatus === 1 ? 'success' : inv.qualityStatus === 2 ? 'danger' : 'warning'}`}>
                              {inv.qualityStatus === 1 ? t('qualityStatus.ok') : inv.qualityStatus === 2 ? t('qualityStatus.blocked') : t('qualityStatus.quarantine')}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            );
          })}

          {filteredMRNList.length === 0 && (
            <div className="card" style={{ textAlign: 'center', color: 'var(--ink-500)' }}>
              {t('inventoryByMrn.noResults')}
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default InventoryByMRN;
