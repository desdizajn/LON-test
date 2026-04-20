import React, { useEffect, useState, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { wmsApi, masterDataApi } from '../../services/api';

const InventoryByLocation: React.FC = () => {
  const { t } = useTranslation();
  const [inventory, setInventory] = useState<any[]>([]);
  const [warehouses, setWarehouses] = useState<any[]>([]);
  const [locations, setLocations] = useState<any[]>([]);
  const [items, setItems] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);

  const [selectedWarehouse, setSelectedWarehouse] = useState<string>('');
  const [selectedLocation, setSelectedLocation] = useState<string>('');
  const [selectedItem, setSelectedItem] = useState<string>('');
  const [selectedQuality, setSelectedQuality] = useState<string>('');

  const loadInventory = useCallback(async () => {
    try {
      setLoading(true);
      const response = await wmsApi.getInventory(
        selectedItem || undefined,
        selectedLocation || undefined
      );
      setInventory(response.data);
    } catch (err) {
      console.error('Failed to load inventory', err);
    } finally {
      setLoading(false);
    }
  }, [selectedItem, selectedLocation]);

  useEffect(() => {
    const loadMasterData = async () => {
      try {
        const [warehousesRes, itemsRes] = await Promise.all([
          masterDataApi.getWarehouses(),
          masterDataApi.getItems()
        ]);
        setWarehouses(warehousesRes.data);
        setItems(itemsRes.data);
      } catch (err) {
        console.error('Failed to load master data', err);
      }
    };
    loadMasterData();
    loadInventory();
  }, [loadInventory]);

  useEffect(() => {
    if (selectedWarehouse) {
      masterDataApi.getLocations(selectedWarehouse)
        .then((r) => setLocations(r.data))
        .catch((e) => console.error('Failed to load locations', e));
    } else {
      setLocations([]);
    }
  }, [selectedWarehouse]);

  const handleReset = () => {
    setSelectedWarehouse('');
    setSelectedLocation('');
    setSelectedItem('');
    setSelectedQuality('');
  };

  const getQualityStatusLabel = (status: number) => {
    if (status === 1) return t('qualityStatus.ok');
    if (status === 2) return t('qualityStatus.blocked');
    if (status === 3) return t('qualityStatus.quarantine');
    return '—';
  };

  const getQualityStatusBadge = (status: number) => {
    const map: Record<number, string> = { 1: 'badge-success', 2: 'badge-danger', 3: 'badge-warning' };
    return `badge ${map[status] || ''}`;
  };

  const inventoryByLocation = inventory.reduce((acc: any, inv: any) => {
    const locationKey = inv.location?.name || '—';
    if (!acc[locationKey]) {
      acc[locationKey] = { location: inv.location, items: [], totalItems: 0, totalQuantity: 0 };
    }
    acc[locationKey].items.push(inv);
    acc[locationKey].totalItems++;
    acc[locationKey].totalQuantity += inv.quantity;
    return acc;
  }, {});

  const filteredInventory = selectedQuality
    ? inventory.filter((inv: any) => inv.qualityStatus === parseInt(selectedQuality))
    : inventory;

  const totalQuantity = filteredInventory.reduce((sum: number, inv: any) => sum + inv.quantity, 0);
  const totalItems = filteredInventory.length;
  const uniqueItems = new Set(filteredInventory.map((inv: any) => inv.itemId)).size;

  const exportToCsv = () => {
    const headers = [
      t('reports.common.location'),
      t('reports.common.warehouse'),
      t('reports.common.itemCode'),
      t('reports.common.itemName'),
      t('reports.common.batch'),
      t('reports.common.mrn'),
      t('reports.common.quantity'),
      'UoM',
      t('reports.common.qualityStatus'),
      t('reports.common.lastMovement'),
    ];
    const rows = filteredInventory.map((inv: any) => [
      inv.location?.name || '',
      inv.location?.warehouse?.name || '',
      inv.item?.code || '',
      inv.item?.name || '',
      inv.batchNumber || '',
      inv.mrn || '',
      inv.quantity.toFixed(2),
      inv.uoM?.code || '',
      getQualityStatusLabel(inv.qualityStatus),
      inv.lastMovementDate ? new Date(inv.lastMovementDate).toLocaleDateString() : '',
    ]);
    const csv = '\uFEFF' + [headers, ...rows].map((row) => row.join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `inventory_by_location_${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
  };

  return (
    <div>
      <div className="header">
        <h2>📍 {t('reports.inventoryByLocation.title')}</h2>
        <button onClick={exportToCsv} style={{ background: 'var(--success)', color: 'white', borderColor: 'var(--success)' }}>
          📥 {t('reports.common.exportCsv')}
        </button>
      </div>

      <div className="card" style={{ marginBottom: 16 }}>
        <h4 style={{ marginBottom: 10 }}>{t('reports.common.filters')}</h4>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12, marginBottom: 10 }}>
          <div>
            <label>{t('reports.common.warehouse')}</label>
            <select value={selectedWarehouse} onChange={(e) => setSelectedWarehouse(e.target.value)}>
              <option value="">— {t('reports.common.allWarehouses')} —</option>
              {warehouses.map((wh) => <option key={wh.id} value={wh.id}>{wh.name}</option>)}
            </select>
          </div>
          <div>
            <label>{t('reports.common.location')}</label>
            <select value={selectedLocation} onChange={(e) => setSelectedLocation(e.target.value)} disabled={!selectedWarehouse}>
              <option value="">— {t('reports.common.allLocations')} —</option>
              {locations.map((loc) => <option key={loc.id} value={loc.id}>{loc.name}</option>)}
            </select>
          </div>
          <div>
            <label>{t('reports.common.item')}</label>
            <select value={selectedItem} onChange={(e) => setSelectedItem(e.target.value)}>
              <option value="">— {t('reports.common.allItems')} —</option>
              {items.map((item) => <option key={item.id} value={item.id}>{item.code} — {item.name}</option>)}
            </select>
          </div>
          <div>
            <label>{t('reports.common.qualityStatus')}</label>
            <select value={selectedQuality} onChange={(e) => setSelectedQuality(e.target.value)}>
              <option value="">— {t('reports.common.allStatuses')} —</option>
              <option value="1">{t('qualityStatus.ok')}</option>
              <option value="2">{t('qualityStatus.blocked')}</option>
              <option value="3">{t('qualityStatus.quarantine')}</option>
            </select>
          </div>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="btn-primary" onClick={loadInventory}>{t('reports.common.apply')}</button>
          <button onClick={handleReset}>{t('reports.common.reset')}</button>
        </div>
      </div>

      <div className="card-grid">
        <div className="card info">
          <h3>{t('reports.common.totalBalances')}</h3>
          <div className="value">{totalItems}</div>
        </div>
        <div className="card success">
          <h3>{t('reports.common.uniqueItems')}</h3>
          <div className="value">{uniqueItems}</div>
        </div>
        <div className="card warning">
          <h3>{t('reports.common.locationsWithInventory')}</h3>
          <div className="value">{Object.keys(inventoryByLocation).length}</div>
        </div>
        <div className="card">
          <h3>{t('reports.common.totalQuantity')}</h3>
          <div className="value">{totalQuantity.toFixed(0)}</div>
        </div>
      </div>

      {loading ? (
        <div className="loading">{t('reports.common.loading')}</div>
      ) : (
        <>
          {Object.entries(inventoryByLocation).map(([locationName, data]: [string, any]) => (
            <div key={locationName} style={{ marginBottom: 20 }}>
              <div style={{
                background: 'var(--taris-blue-50)', padding: '10px 14px', borderRadius: 8, marginBottom: 10,
                display: 'flex', justifyContent: 'space-between', alignItems: 'center',
                border: '1px solid var(--taris-blue-100)',
              }}>
                <div>
                  <strong>📍 {locationName}</strong>
                  {data.location?.warehouse?.name && (
                    <span style={{ marginLeft: 12, color: 'var(--ink-500)' }}>({data.location.warehouse.name})</span>
                  )}
                </div>
                <div style={{ fontSize: 13, color: 'var(--ink-600)' }}>
                  {data.totalItems} · {t('reports.common.totalQty')}: {data.totalQuantity.toFixed(2)}
                </div>
              </div>

              <div className="table-container">
                <table>
                  <thead>
                    <tr>
                      <th>{t('reports.common.itemCode')}</th>
                      <th>{t('reports.common.itemName')}</th>
                      <th>{t('reports.common.batch')}</th>
                      <th>{t('reports.common.mrn')}</th>
                      <th>{t('reports.common.quantity')}</th>
                      <th>{t('reports.common.qualityStatus')}</th>
                      <th>{t('reports.common.lastMovement')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.items.map((inv: any, idx: number) => (
                      <tr key={idx}>
                        <td><strong>{inv.item?.code}</strong></td>
                        <td>{inv.item?.name}</td>
                        <td>{inv.batchNumber || '-'}</td>
                        <td>{inv.mrn || '-'}</td>
                        <td><strong>{inv.quantity.toFixed(2)}</strong> {inv.uoM?.code}</td>
                        <td><span className={getQualityStatusBadge(inv.qualityStatus)}>{getQualityStatusLabel(inv.qualityStatus)}</span></td>
                        <td>{inv.lastMovementDate ? new Date(inv.lastMovementDate).toLocaleDateString() : '-'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ))}

          {Object.keys(inventoryByLocation).length === 0 && (
            <div className="card" style={{ textAlign: 'center', color: 'var(--ink-500)' }}>
              {t('reports.common.noResults')}
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default InventoryByLocation;
