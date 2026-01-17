import React, { useState } from 'react';
import { wmsApi, masterDataApi } from '../../services/api';

const LocationInquiry: React.FC = () => {
  const [searchCode, setSearchCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [locationData, setLocationData] = useState<any>(null);
  const [inventory, setInventory] = useState<any[]>([]);
  const [recentMovements, setRecentMovements] = useState<any[]>([]);

  const searchLocation = async () => {
    if (!searchCode.trim()) {
      alert('Please enter a location code');
      return;
    }

    try {
      setLoading(true);

      // Get all locations
      const locationsRes = await masterDataApi.getLocations();
      const location = locationsRes.data.find((loc: any) => 
        loc.code.toLowerCase() === searchCode.toLowerCase() || 
        loc.name.toLowerCase() === searchCode.toLowerCase()
      );

      if (!location) {
        alert('Location not found');
        setLoading(false);
        return;
      }

      // Get warehouse info
      const warehousesRes = await masterDataApi.getWarehouses();
      const warehouse = warehousesRes.data.find((wh: any) => wh.id === location.warehouseId);

      // Get inventory for this location
      const inventoryRes = await wmsApi.getInventory();
      const locationInventory = inventoryRes.data.filter((inv: any) => inv.locationId === location.id);

      setInventory(locationInventory);

      // Calculate metrics
      const totalQuantity = locationInventory.reduce((sum: number, inv: any) => sum + inv.quantity, 0);
      const totalValue = locationInventory.reduce((sum: number, inv: any) => 
        sum + (inv.quantity * (inv.item?.unitCost || 0)), 0
      );
      const uniqueItems = new Set(locationInventory.map((inv: any) => inv.itemId)).size;
      const uniqueBatches = new Set(locationInventory.map((inv: any) => inv.batchNumber)).size;
      const uniqueMRNs = new Set(locationInventory.map((inv: any) => inv.mrn).filter(Boolean)).size;

      // Quality breakdown
      const okQty = locationInventory.filter((inv: any) => inv.qualityStatus === 1).length;
      const blockedQty = locationInventory.filter((inv: any) => inv.qualityStatus === 2).length;
      const quarantineQty = locationInventory.filter((inv: any) => inv.qualityStatus === 3).length;

      // Mock recent movements (last 10)
      const mockMovements = [
        {
          date: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
          type: 'Issue',
          item: locationInventory[0]?.item,
          quantity: -50,
          reference: 'PROD-010',
          user: 'System'
        },
        {
          date: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString(),
          type: 'Receipt',
          item: locationInventory[0]?.item,
          quantity: 200,
          reference: 'REC-152',
          user: 'John Doe'
        },
        {
          date: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(),
          type: 'Transfer',
          item: locationInventory[1]?.item,
          quantity: -75,
          reference: 'TRF-089',
          user: 'Jane Smith'
        },
        {
          date: new Date(Date.now() - 10 * 24 * 60 * 60 * 1000).toISOString(),
          type: 'CycleCount',
          item: locationInventory[0]?.item,
          quantity: 0,
          reference: 'CC-045',
          user: 'Jane Smith'
        },
        {
          date: new Date(Date.now() - 15 * 24 * 60 * 60 * 1000).toISOString(),
          type: 'Transfer',
          item: locationInventory[2]?.item,
          quantity: 100,
          reference: 'TRF-087',
          user: 'John Doe'
        }
      ];

      setRecentMovements(mockMovements);

      setLocationData({
        ...location,
        warehouse,
        totalQuantity,
        totalValue,
        uniqueItems,
        uniqueBatches,
        uniqueMRNs,
        itemCount: locationInventory.length,
        okQty,
        blockedQty,
        quarantineQty,
        utilization: location.capacity ? ((locationInventory.length / location.capacity) * 100).toFixed(1) : 'N/A'
      });

    } catch (err) {
      console.error('Failed to load location data', err);
      alert('Failed to load location data');
    } finally {
      setLoading(false);
    }
  };

  const handleTransferAll = () => {
    alert('Transfer All functionality would open transfer form with all inventory in this location pre-selected');
  };

  const handleCycleCount = () => {
    alert('Cycle Count functionality would open cycle count form with this location pre-selected');
  };

  const handleBlockLocation = () => {
    if (window.confirm(`Are you sure you want to BLOCK location ${locationData.code}? No further receipts or transfers will be allowed.`)) {
      alert('Location blocked successfully (mock - would update location.isBlocked = true)');
    }
  };

  const getMovementIcon = (type: string) => {
    switch (type) {
      case 'Receipt': return '📥';
      case 'Transfer': return '🔄';
      case 'Issue': return '📤';
      case 'CycleCount': return '📊';
      case 'Adjustment': return '⚙️';
      default: return '📋';
    }
  };

  return (
    <div>
      <div className="header">
        <h2>📍 Location Inquiry</h2>
      </div>

      {/* Alert */}
      <div className="alert alert-info" style={{ marginBottom: '20px' }}>
        <strong>ℹ️ Quick Location Lookup</strong><br />
        Search by location code or name to view all inventory, recent movements, and perform quick actions.
      </div>

      {/* Search Section */}
      <div className="card" style={{ padding: '20px', marginBottom: '30px' }}>
        <div style={{ display: 'flex', gap: '10px', alignItems: 'flex-end' }}>
          <div style={{ flex: 1 }}>
            <label><strong>Location Code or Name:</strong></label>
            <input
              type="text"
              className="form-control"
              placeholder="Enter location code or name..."
              value={searchCode}
              onChange={(e) => setSearchCode(e.target.value)}
              onKeyPress={(e) => e.key === 'Enter' && searchLocation()}
            />
          </div>
          <button 
            className="btn btn-primary" 
            onClick={searchLocation}
            disabled={loading}
            style={{ height: '38px' }}
          >
            {loading ? '🔍 Searching...' : '🔍 Search'}
          </button>
        </div>
      </div>

      {locationData && (
        <>
          {/* Location Header */}
          <div className="card" style={{ 
            padding: '20px', 
            marginBottom: '30px', 
            background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)', 
            color: 'white' 
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '15px' }}>
              <div>
                <h3 style={{ margin: 0 }}>📍 {locationData.code} - {locationData.name}</h3>
                <div style={{ fontSize: '14px', opacity: 0.9, marginTop: '5px' }}>
                  Warehouse: <strong>{locationData.warehouse?.name || 'Unknown'}</strong> | 
                  Zone: <strong>{locationData.zone || 'N/A'}</strong> | 
                  Type: <strong>{locationData.locationType || 'N/A'}</strong>
                </div>
              </div>
              <div style={{ textAlign: 'right' }}>
                <div style={{ fontSize: '12px', opacity: 0.9 }}>Status</div>
                <div style={{ fontSize: '24px', fontWeight: 'bold' }}>
                  {locationData.isActive ? '✅ Active' : '❌ Inactive'}
                </div>
              </div>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: '15px' }}>
              <div>
                <div style={{ fontSize: '12px', opacity: 0.9 }}>Inventory Lines</div>
                <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{locationData.itemCount}</div>
              </div>
              <div>
                <div style={{ fontSize: '12px', opacity: 0.9 }}>Unique Items</div>
                <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{locationData.uniqueItems}</div>
              </div>
              <div>
                <div style={{ fontSize: '12px', opacity: 0.9 }}>Total Quantity</div>
                <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{locationData.totalQuantity.toFixed(0)}</div>
              </div>
              <div>
                <div style={{ fontSize: '12px', opacity: 0.9 }}>Total Value</div>
                <div style={{ fontSize: '24px', fontWeight: 'bold' }}>${locationData.totalValue.toFixed(0)}</div>
              </div>
              <div>
                <div style={{ fontSize: '12px', opacity: 0.9 }}>Utilization</div>
                <div style={{ fontSize: '24px', fontWeight: 'bold' }}>
                  {typeof locationData.utilization === 'number' ? `${locationData.utilization}%` : locationData.utilization}
                </div>
              </div>
            </div>
          </div>

          {/* Quality Breakdown & Actions */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px', marginBottom: '30px' }}>
            <div className="card" style={{ padding: '15px' }}>
              <h5>📊 Quality Status Breakdown</h5>
              <div style={{ marginTop: '15px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
                  <span>OK (Available):</span>
                  <strong className="text-success">{locationData.okQty} lines</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
                  <span>Blocked:</span>
                  <strong className="text-danger">{locationData.blockedQty} lines</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
                  <span>Quarantine:</span>
                  <strong className="text-warning">{locationData.quarantineQty} lines</strong>
                </div>
                <div style={{ marginTop: '15px', padding: '10px', background: '#f8f9fa', borderRadius: '4px' }}>
                  <div style={{ fontSize: '12px', marginBottom: '5px' }}>
                    <strong>Batches:</strong> {locationData.uniqueBatches}
                  </div>
                  <div style={{ fontSize: '12px' }}>
                    <strong>MRNs:</strong> {locationData.uniqueMRNs}
                  </div>
                </div>
              </div>
            </div>

            <div className="card" style={{ padding: '15px' }}>
              <h5>⚡ Quick Actions</h5>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', marginTop: '15px' }}>
                <button 
                  className="btn btn-primary"
                  onClick={handleTransferAll}
                >
                  🔄 Transfer All Inventory
                </button>
                <button 
                  className="btn btn-info"
                  onClick={handleCycleCount}
                >
                  📊 Start Cycle Count
                </button>
                <button 
                  className="btn btn-danger"
                  onClick={handleBlockLocation}
                >
                  🔒 Block Location
                </button>
              </div>
              <div style={{ marginTop: '15px', padding: '10px', background: '#fff3cd', borderRadius: '4px', fontSize: '12px' }}>
                <strong>Note:</strong> Blocking a location prevents new receipts and transfers in. Existing inventory can still be issued.
              </div>
            </div>
          </div>

          {/* Inventory Details */}
          <div className="card" style={{ padding: '15px', marginBottom: '30px' }}>
            <h5>📦 All Inventory in This Location</h5>
            {inventory.length === 0 ? (
              <div style={{ textAlign: 'center', padding: '40px', color: '#666' }}>
                <div style={{ fontSize: '48px', marginBottom: '10px' }}>📭</div>
                <div>No inventory in this location</div>
              </div>
            ) : (
              <div className="table-container" style={{ marginTop: '15px' }}>
                <table>
                  <thead>
                    <tr>
                      <th>Item Code</th>
                      <th>Item Name</th>
                      <th>Batch</th>
                      <th>MRN</th>
                      <th>Quantity</th>
                      <th>Unit Cost</th>
                      <th>Total Value</th>
                      <th>Quality Status</th>
                      <th>Last Movement</th>
                    </tr>
                  </thead>
                  <tbody>
                    {inventory.map((inv, idx) => (
                      <tr key={idx}>
                        <td><strong>{inv.item?.code}</strong></td>
                        <td>{inv.item?.name}</td>
                        <td>{inv.batchNumber}</td>
                        <td>{inv.mrn || '-'}</td>
                        <td>{inv.quantity.toFixed(2)}</td>
                        <td>${(inv.item?.unitCost || 0).toFixed(2)}</td>
                        <td><strong>${(inv.quantity * (inv.item?.unitCost || 0)).toFixed(2)}</strong></td>
                        <td>
                          <span className={`badge badge-${inv.qualityStatus === 1 ? 'success' : inv.qualityStatus === 2 ? 'danger' : 'warning'}`}>
                            {inv.qualityStatus === 1 ? 'OK' : inv.qualityStatus === 2 ? 'Blocked' : 'Quarantine'}
                          </span>
                        </td>
                        <td>{new Date(inv.lastMovementDate || inv.createdAt).toLocaleDateString()}</td>
                      </tr>
                    ))}
                    <tr style={{ background: '#f8f9fa', fontWeight: 'bold' }}>
                      <td colSpan={4}>TOTAL:</td>
                      <td>{locationData.totalQuantity.toFixed(2)}</td>
                      <td></td>
                      <td>${locationData.totalValue.toFixed(2)}</td>
                      <td colSpan={2}></td>
                    </tr>
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* Recent Movements */}
          <div className="card" style={{ padding: '15px' }}>
            <h5>📅 Recent Movements (Last 10)</h5>
            {recentMovements.length === 0 ? (
              <div style={{ textAlign: 'center', padding: '40px', color: '#666' }}>
                No recent movements
              </div>
            ) : (
              <div className="table-container" style={{ marginTop: '15px' }}>
                <table>
                  <thead>
                    <tr>
                      <th>Date</th>
                      <th>Type</th>
                      <th>Item</th>
                      <th>Quantity</th>
                      <th>Reference</th>
                      <th>User</th>
                    </tr>
                  </thead>
                  <tbody>
                    {recentMovements.map((movement, idx) => (
                      <tr key={idx}>
                        <td>{new Date(movement.date).toLocaleDateString()}</td>
                        <td>
                          <span style={{ marginRight: '5px' }}>{getMovementIcon(movement.type)}</span>
                          {movement.type}
                        </td>
                        <td>{movement.item?.code} - {movement.item?.name}</td>
                        <td>
                          <strong style={{ color: movement.quantity > 0 ? '#28a745' : '#dc3545' }}>
                            {movement.quantity > 0 ? '+' : ''}{movement.quantity}
                          </strong>
                        </td>
                        <td>{movement.reference}</td>
                        <td>{movement.user}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
};

export default LocationInquiry;
