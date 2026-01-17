import React, { useEffect, useState } from 'react';
import { wmsApi, masterDataApi } from '../../services/api';

const WarehouseUtilization: React.FC = () => {
  const [inventory, setInventory] = useState<any[]>([]);
  const [warehouses, setWarehouses] = useState<any[]>([]);
  const [locations, setLocations] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedWarehouse, setSelectedWarehouse] = useState('');

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [inventoryRes, warehousesRes, locationsRes] = await Promise.all([
        wmsApi.getInventory(),
        masterDataApi.getWarehouses(),
        masterDataApi.getLocations(),
      ]);
      setInventory(inventoryRes.data);
      setWarehouses(warehousesRes.data);
      setLocations(locationsRes.data);
    } catch (err) {
      console.error('Failed to load warehouse utilization data', err);
    } finally {
      setLoading(false);
    }
  };

  // Filter by selected warehouse
  const filteredLocations = selectedWarehouse
    ? locations.filter(loc => loc.warehouseId.toString() === selectedWarehouse)
    : locations;

  const filteredInventory = selectedWarehouse
    ? inventory.filter(inv => {
        const loc = locations.find(l => l.id === inv.locationId);
        return loc && loc.warehouseId.toString() === selectedWarehouse;
      })
    : inventory;

  // Calculate utilization metrics
  const totalLocations = filteredLocations.length;
  const occupiedLocationIds = new Set(filteredInventory.map(inv => inv.locationId));
  const occupiedLocations = occupiedLocationIds.size;
  const emptyLocations = totalLocations - occupiedLocations;
  const utilizationPercentage = totalLocations > 0 ? (occupiedLocations / totalLocations * 100).toFixed(1) : 0;

  // Utilization by Warehouse
  const warehouseUtilization = warehouses.map(wh => {
    const whLocations = locations.filter(loc => loc.warehouseId === wh.id);
    const whInventory = inventory.filter(inv => {
      const loc = locations.find(l => l.id === inv.locationId);
      return loc && loc.warehouseId === wh.id;
    });
    const whOccupiedLocationIds = new Set(whInventory.map(inv => inv.locationId));
    const whOccupied = whOccupiedLocationIds.size;
    const whTotal = whLocations.length;
    const whUtilization = whTotal > 0 ? (whOccupied / whTotal * 100).toFixed(1) : 0;

    return {
      id: wh.id,
      name: wh.name,
      code: wh.code,
      totalLocations: whTotal,
      occupiedLocations: whOccupied,
      emptyLocations: whTotal - whOccupied,
      utilizationPercentage: whUtilization,
    };
  }).sort((a, b) => parseFloat(String(b.utilizationPercentage)) - parseFloat(String(a.utilizationPercentage)));

  // Utilization by Zone (if locations have zones)
  const zoneUtilization = filteredLocations.reduce((acc: any, loc: any) => {
    const zone = loc.zone || 'Unassigned';
    const isOccupied = occupiedLocationIds.has(loc.id);
    
    if (!acc[zone]) {
      acc[zone] = { zone, total: 0, occupied: 0, empty: 0 };
    }
    acc[zone].total++;
    if (isOccupied) {
      acc[zone].occupied++;
    } else {
      acc[zone].empty++;
    }
    return acc;
  }, {});

  const zoneUtilizationList = Object.values(zoneUtilization).map((zone: any) => ({
    ...zone,
    utilizationPercentage: zone.total > 0 ? ((zone.occupied / zone.total) * 100).toFixed(1) : 0,
  })).sort((a: any, b: any) => parseFloat(b.utilizationPercentage) - parseFloat(a.utilizationPercentage));

  // Location details (Top 20 occupied locations by item count)
  const locationDetails = filteredLocations.map(loc => {
    const locInventory = filteredInventory.filter(inv => inv.locationId === loc.id);
    const totalQuantity = locInventory.reduce((sum, inv) => sum + inv.quantity, 0);
    const uniqueItems = new Set(locInventory.map(inv => inv.itemId)).size;
    const wh = warehouses.find(w => w.id === loc.warehouseId);

    return {
      id: loc.id,
      name: loc.name,
      code: loc.code,
      warehouseName: wh?.name || 'Unknown',
      zone: loc.zone || '-',
      locationType: loc.locationType || '-',
      capacity: loc.capacity || 0,
      itemCount: locInventory.length,
      uniqueItems,
      totalQuantity,
      isOccupied: locInventory.length > 0,
    };
  });

  const topOccupiedLocations = locationDetails
    .filter(loc => loc.isOccupied)
    .sort((a, b) => b.itemCount - a.itemCount)
    .slice(0, 20);

  const emptyLocationsList = locationDetails
    .filter(loc => !loc.isOccupied)
    .sort((a, b) => a.name.localeCompare(b.name))
    .slice(0, 20);

  // Export to Excel
  const exportToExcel = () => {
    const headers = ['Location Code', 'Location Name', 'Warehouse', 'Zone', 'Type', 'Capacity', 'Item Count', 'Unique Items', 'Total Quantity', 'Status'];
    const rows = locationDetails.map(loc => [
      loc.code,
      loc.name,
      loc.warehouseName,
      loc.zone,
      loc.locationType,
      loc.capacity,
      loc.itemCount,
      loc.uniqueItems,
      loc.totalQuantity.toFixed(2),
      loc.isOccupied ? 'Occupied' : 'Empty',
    ]);

    const csv = [headers, ...rows].map(row => row.join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `warehouse_utilization_${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
  };

  if (loading) return <div className="loading">Loading warehouse utilization data...</div>;

  return (
    <div>
      <div className="header">
        <h2>🏭 Warehouse Utilization Report</h2>
        <button className="btn btn-primary" onClick={exportToExcel}>
          📊 Export to Excel
        </button>
      </div>

      {/* Alert */}
      <div className="alert alert-info" style={{ marginBottom: '20px' }}>
        <strong>ℹ️ Warehouse Space Utilization</strong><br />
        Tracks how efficiently warehouse space is being used. Target: 70-85% utilization for optimal operations.
      </div>

      {/* Filter */}
      <div className="card" style={{ padding: '15px', marginBottom: '20px' }}>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 3fr', gap: '10px', alignItems: 'center' }}>
          <label>Filter by Warehouse:</label>
          <select
            className="form-control"
            value={selectedWarehouse}
            onChange={(e) => setSelectedWarehouse(e.target.value)}
          >
            <option value="">All Warehouses</option>
            {warehouses.map(wh => (
              <option key={wh.id} value={wh.id}>{wh.name}</option>
            ))}
          </select>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="summary-cards" style={{ 
        display: 'grid', 
        gridTemplateColumns: 'repeat(4, 1fr)', 
        gap: '15px', 
        marginBottom: '30px' 
      }}>
        <div className="card" style={{ padding: '20px', textAlign: 'center' }}>
          <div style={{ fontSize: '12px', color: '#666', marginBottom: '5px' }}>Total Locations</div>
          <div style={{ fontSize: '32px', fontWeight: 'bold' }}>{totalLocations}</div>
        </div>
        <div className="card" style={{ padding: '20px', textAlign: 'center' }}>
          <div style={{ fontSize: '12px', color: '#666', marginBottom: '5px' }}>Occupied Locations</div>
          <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#28a745' }}>{occupiedLocations}</div>
        </div>
        <div className="card" style={{ padding: '20px', textAlign: 'center' }}>
          <div style={{ fontSize: '12px', color: '#666', marginBottom: '5px' }}>Empty Locations</div>
          <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#6c757d' }}>{emptyLocations}</div>
        </div>
        <div className="card" style={{ padding: '20px', textAlign: 'center' }}>
          <div style={{ fontSize: '12px', color: '#666', marginBottom: '5px' }}>Utilization %</div>
          <div style={{ 
            fontSize: '32px', 
            fontWeight: 'bold',
            color: parseFloat(utilizationPercentage as string) >= 70 && parseFloat(utilizationPercentage as string) <= 85 ? '#28a745' :
                   parseFloat(utilizationPercentage as string) >= 60 ? '#ffc107' : '#dc3545'
          }}>
            {utilizationPercentage}%
          </div>
          <div style={{ fontSize: '12px' }}>Target: 70-85%</div>
        </div>
      </div>

      {/* Utilization by Warehouse */}
      <div className="card" style={{ padding: '15px', marginBottom: '30px' }}>
        <h5>🏢 Utilization by Warehouse</h5>
        <div className="table-container" style={{ marginTop: '15px' }}>
          <table>
            <thead>
              <tr>
                <th>Warehouse Code</th>
                <th>Warehouse Name</th>
                <th>Total Locations</th>
                <th>Occupied</th>
                <th>Empty</th>
                <th>Utilization %</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {warehouseUtilization.map(wh => (
                <tr key={wh.id} style={{
                  backgroundColor: 
                    parseFloat(String(wh.utilizationPercentage)) >= 70 && parseFloat(String(wh.utilizationPercentage)) <= 85 ? '#d4edda' :
                    parseFloat(String(wh.utilizationPercentage)) >= 60 ? '#fff3cd' :
                    parseFloat(String(wh.utilizationPercentage)) > 85 ? '#f8d7da' : '#e9ecef'
                }}>
                  <td><strong>{wh.code}</strong></td>
                  <td>{wh.name}</td>
                  <td>{wh.totalLocations}</td>
                  <td className="text-success">{wh.occupiedLocations}</td>
                  <td className="text-secondary">{wh.emptyLocations}</td>
                  <td>
                    <strong style={{
                      color: parseFloat(String(wh.utilizationPercentage)) >= 70 && parseFloat(String(wh.utilizationPercentage)) <= 85 ? '#28a745' :
                             parseFloat(String(wh.utilizationPercentage)) >= 60 ? '#ffc107' : '#dc3545'
                    }}>
                      {wh.utilizationPercentage}%
                    </strong>
                  </td>
                  <td>
                    {parseFloat(String(wh.utilizationPercentage)) >= 70 && parseFloat(String(wh.utilizationPercentage)) <= 85 ? (
                      <span className="badge badge-success">✅ Optimal</span>
                    ) : parseFloat(String(wh.utilizationPercentage)) >= 60 && parseFloat(String(wh.utilizationPercentage)) < 70 ? (
                      <span className="badge badge-warning">⚠️ Underutilized</span>
                    ) : parseFloat(String(wh.utilizationPercentage)) > 85 ? (
                      <span className="badge badge-danger">⚠️ Overcrowded</span>
                    ) : (
                      <span className="badge badge-secondary">❌ Very Low</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Utilization by Zone */}
      {zoneUtilizationList.length > 0 && (
        <div className="card" style={{ padding: '15px', marginBottom: '30px' }}>
          <h5>📍 Utilization by Zone</h5>
          <div className="table-container" style={{ marginTop: '15px' }}>
            <table>
              <thead>
                <tr>
                  <th>Zone</th>
                  <th>Total Locations</th>
                  <th>Occupied</th>
                  <th>Empty</th>
                  <th>Utilization %</th>
                  <th>Visual</th>
                </tr>
              </thead>
              <tbody>
                {zoneUtilizationList.map((zone: any, idx: number) => (
                  <tr key={idx}>
                    <td><strong>{zone.zone}</strong></td>
                    <td>{zone.total}</td>
                    <td className="text-success">{zone.occupied}</td>
                    <td className="text-secondary">{zone.empty}</td>
                    <td>
                      <strong>{zone.utilizationPercentage}%</strong>
                    </td>
                    <td>
                      <div style={{ 
                        width: '100%', 
                        height: '20px', 
                        background: '#e9ecef', 
                        borderRadius: '4px',
                        overflow: 'hidden'
                      }}>
                        <div style={{ 
                          width: `${zone.utilizationPercentage}%`, 
                          height: '100%', 
                          background: parseFloat(zone.utilizationPercentage) >= 70 && parseFloat(zone.utilizationPercentage) <= 85 ? '#28a745' :
                                     parseFloat(zone.utilizationPercentage) >= 60 ? '#ffc107' : '#dc3545',
                          transition: 'width 0.3s ease'
                        }} />
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Top Occupied Locations & Empty Locations */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px', marginBottom: '30px' }}>
        {/* Top Occupied Locations */}
        <div className="card" style={{ padding: '15px' }}>
          <h5>📦 Top 20 Occupied Locations (by Item Count)</h5>
          <div className="table-container" style={{ marginTop: '15px', maxHeight: '500px', overflowY: 'auto' }}>
            <table>
              <thead>
                <tr>
                  <th>Location</th>
                  <th>Warehouse</th>
                  <th>Zone</th>
                  <th>Items</th>
                  <th>Unique</th>
                  <th>Total Qty</th>
                </tr>
              </thead>
              <tbody>
                {topOccupiedLocations.map(loc => (
                  <tr key={loc.id}>
                    <td><strong>{loc.name}</strong></td>
                    <td>{loc.warehouseName}</td>
                    <td>{loc.zone}</td>
                    <td>{loc.itemCount}</td>
                    <td>{loc.uniqueItems}</td>
                    <td>{loc.totalQuantity.toFixed(2)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Empty Locations */}
        <div className="card" style={{ padding: '15px' }}>
          <h5>📭 Empty Locations (First 20)</h5>
          <div className="table-container" style={{ marginTop: '15px', maxHeight: '500px', overflowY: 'auto' }}>
            <table>
              <thead>
                <tr>
                  <th>Location</th>
                  <th>Warehouse</th>
                  <th>Zone</th>
                  <th>Type</th>
                  <th>Capacity</th>
                </tr>
              </thead>
              <tbody>
                {emptyLocationsList.map(loc => (
                  <tr key={loc.id} style={{ background: '#f8f9fa' }}>
                    <td><strong>{loc.name}</strong></td>
                    <td>{loc.warehouseName}</td>
                    <td>{loc.zone}</td>
                    <td>{loc.locationType}</td>
                    <td>{loc.capacity > 0 ? loc.capacity : '-'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      {/* Recommendations */}
      <div className="card" style={{ padding: '15px', background: '#f8f9fa' }}>
        <h5>💡 Utilization Recommendations</h5>
        <div style={{ marginTop: '10px' }}>
          <div style={{ marginBottom: '10px' }}>
            <strong>Optimal Utilization: 70-85%</strong>
            <ul style={{ marginTop: '5px', marginBottom: 0 }}>
              <li>Below 60%: Consider consolidating locations or using smaller warehouse footprint</li>
              <li>60-70%: Underutilized - good availability but may have excess capacity</li>
              <li>70-85%: Optimal - good balance between space usage and operational flexibility</li>
              <li>Above 85%: Overcrowded - may cause picking delays, consider expansion or offsite storage</li>
            </ul>
          </div>
          {parseFloat(utilizationPercentage as string) > 85 && (
            <div className="alert alert-danger" style={{ marginTop: '10px' }}>
              <strong>⚠️ Warehouse overcrowding detected!</strong><br />
              Current utilization ({utilizationPercentage}%) exceeds optimal range. Consider:
              <ul style={{ marginBottom: 0 }}>
                <li>Reviewing slow-moving inventory for disposal</li>
                <li>Expanding warehouse capacity</li>
                <li>Using offsite or overflow storage</li>
              </ul>
            </div>
          )}
          {parseFloat(utilizationPercentage as string) < 60 && (
            <div className="alert alert-warning" style={{ marginTop: '10px' }}>
              <strong>ℹ️ Low utilization detected</strong><br />
              Current utilization ({utilizationPercentage}%) is below optimal. Consider consolidating locations or adjusting warehouse layout.
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default WarehouseUtilization;
