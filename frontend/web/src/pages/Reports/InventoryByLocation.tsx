import React, { useEffect, useState } from 'react';
import { wmsApi, masterDataApi } from '../../services/api';

const InventoryByLocation: React.FC = () => {
  const [inventory, setInventory] = useState<any[]>([]);
  const [warehouses, setWarehouses] = useState<any[]>([]);
  const [locations, setLocations] = useState<any[]>([]);
  const [items, setItems] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  
  // Filters
  const [selectedWarehouse, setSelectedWarehouse] = useState<string>('');
  const [selectedLocation, setSelectedLocation] = useState<string>('');
  const [selectedItem, setSelectedItem] = useState<string>('');
  const [selectedQuality, setSelectedQuality] = useState<string>('');

  useEffect(() => {
    loadMasterData();
    loadInventory();
  }, []);

  useEffect(() => {
    if (selectedWarehouse) {
      loadLocationsForWarehouse(selectedWarehouse);
    }
  }, [selectedWarehouse]);

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

  const loadLocationsForWarehouse = async (warehouseId: string) => {
    try {
      const response = await masterDataApi.getLocations(warehouseId);
      setLocations(response.data);
    } catch (err) {
      console.error('Failed to load locations', err);
    }
  };

  const loadInventory = async () => {
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
  };

  const handleFilterChange = () => {
    loadInventory();
  };

  const handleReset = () => {
    setSelectedWarehouse('');
    setSelectedLocation('');
    setSelectedItem('');
    setSelectedQuality('');
    loadInventory();
  };

  const getQualityStatusLabel = (status: number) => {
    const labels: { [key: number]: string } = {
      1: 'OK',
      2: 'Blocked',
      3: 'Quarantine',
    };
    return labels[status] || 'Unknown';
  };

  const getQualityStatusBadge = (status: number) => {
    const badges: { [key: number]: string } = {
      1: 'badge-success',
      2: 'badge-danger',
      3: 'badge-warning',
    };
    return `badge ${badges[status] || 'badge-secondary'}`;
  };

  // Group by location
  const inventoryByLocation = inventory.reduce((acc: any, inv: any) => {
    const locationKey = inv.location?.name || 'Unknown Location';
    if (!acc[locationKey]) {
      acc[locationKey] = {
        location: inv.location,
        items: [],
        totalItems: 0,
        totalQuantity: 0,
      };
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

  const exportToExcel = () => {
    // Simple CSV export
    const headers = ['Location', 'Warehouse', 'Item Code', 'Item Name', 'Batch', 'MRN', 'Quantity', 'UoM', 'Quality Status', 'Last Movement'];
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

    const csv = [headers, ...rows].map(row => row.join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `inventory_by_location_${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
  };

  return (
    <div>
      <div className="header">
        <h2>📍 Inventory by Location</h2>
        <button className="btn btn-success" onClick={exportToExcel}>
          📥 Export to Excel
        </button>
      </div>

      {/* Filters */}
      <div className="filters" style={{ 
        background: '#f8f9fa', 
        padding: '20px', 
        borderRadius: '8px', 
        marginBottom: '20px' 
      }}>
        <h4>Filters</h4>
        <div className="form-row">
          <div className="form-group col-md-3">
            <label>Warehouse</label>
            <select
              className="form-control"
              value={selectedWarehouse}
              onChange={(e) => setSelectedWarehouse(e.target.value)}
            >
              <option value="">-- All Warehouses --</option>
              {warehouses.map((wh) => (
                <option key={wh.id} value={wh.id}>
                  {wh.name}
                </option>
              ))}
            </select>
          </div>

          <div className="form-group col-md-3">
            <label>Location</label>
            <select
              className="form-control"
              value={selectedLocation}
              onChange={(e) => setSelectedLocation(e.target.value)}
              disabled={!selectedWarehouse}
            >
              <option value="">-- All Locations --</option>
              {locations.map((loc) => (
                <option key={loc.id} value={loc.id}>
                  {loc.name}
                </option>
              ))}
            </select>
          </div>

          <div className="form-group col-md-3">
            <label>Item</label>
            <select
              className="form-control"
              value={selectedItem}
              onChange={(e) => setSelectedItem(e.target.value)}
            >
              <option value="">-- All Items --</option>
              {items.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.code} - {item.name}
                </option>
              ))}
            </select>
          </div>

          <div className="form-group col-md-3">
            <label>Quality Status</label>
            <select
              className="form-control"
              value={selectedQuality}
              onChange={(e) => setSelectedQuality(e.target.value)}
            >
              <option value="">-- All Statuses --</option>
              <option value="1">OK</option>
              <option value="2">Blocked</option>
              <option value="3">Quarantine</option>
            </select>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '10px' }}>
          <button className="btn btn-primary" onClick={handleFilterChange}>
            Apply Filters
          </button>
          <button className="btn btn-secondary" onClick={handleReset}>
            Reset
          </button>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="summary-cards" style={{ 
        display: 'grid', 
        gridTemplateColumns: 'repeat(4, 1fr)', 
        gap: '15px', 
        marginBottom: '20px' 
      }}>
        <div className="card" style={{ padding: '15px', background: '#e3f2fd' }}>
          <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{totalItems}</div>
          <div style={{ fontSize: '12px', color: '#666' }}>Total Balances</div>
        </div>
        <div className="card" style={{ padding: '15px', background: '#e8f5e9' }}>
          <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{uniqueItems}</div>
          <div style={{ fontSize: '12px', color: '#666' }}>Unique Items</div>
        </div>
        <div className="card" style={{ padding: '15px', background: '#fff3e0' }}>
          <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{Object.keys(inventoryByLocation).length}</div>
          <div style={{ fontSize: '12px', color: '#666' }}>Locations with Inventory</div>
        </div>
        <div className="card" style={{ padding: '15px', background: '#f3e5f5' }}>
          <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{totalQuantity.toFixed(0)}</div>
          <div style={{ fontSize: '12px', color: '#666' }}>Total Quantity (all UoM)</div>
        </div>
      </div>

      {loading ? (
        <div className="loading">Loading inventory...</div>
      ) : (
        <>
          {/* Grouped View */}
          <div className="location-groups">
            {Object.entries(inventoryByLocation).map(([locationName, data]: [string, any]) => (
              <div key={locationName} style={{ marginBottom: '30px' }}>
                <div style={{ 
                  background: '#e3f2fd', 
                  padding: '10px 15px', 
                  borderRadius: '4px',
                  marginBottom: '10px',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center'
                }}>
                  <div>
                    <strong>📍 {locationName}</strong>
                    <span style={{ marginLeft: '15px', color: '#666' }}>
                      ({data.location?.warehouse?.name})
                    </span>
                  </div>
                  <div style={{ fontSize: '14px', color: '#666' }}>
                    {data.totalItems} items | Total Qty: {data.totalQuantity.toFixed(2)}
                  </div>
                </div>

                <div className="table-container">
                  <table>
                    <thead>
                      <tr>
                        <th>Item Code</th>
                        <th>Item Name</th>
                        <th>Batch</th>
                        <th>MRN</th>
                        <th>Quantity</th>
                        <th>Quality Status</th>
                        <th>Last Movement</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.items.map((inv: any, idx: number) => (
                        <tr key={idx}>
                          <td><strong>{inv.item?.code}</strong></td>
                          <td>{inv.item?.name}</td>
                          <td>{inv.batchNumber || '-'}</td>
                          <td>{inv.mrn || '-'}</td>
                          <td>
                            <strong>{inv.quantity.toFixed(2)}</strong> {inv.uoM?.code}
                          </td>
                          <td>
                            <span className={getQualityStatusBadge(inv.qualityStatus)}>
                              {getQualityStatusLabel(inv.qualityStatus)}
                            </span>
                          </td>
                          <td>
                            {inv.lastMovementDate 
                              ? new Date(inv.lastMovementDate).toLocaleDateString()
                              : '-'}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            ))}
          </div>

          {Object.keys(inventoryByLocation).length === 0 && (
            <div className="alert alert-info">
              No inventory found with the selected filters.
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default InventoryByLocation;
