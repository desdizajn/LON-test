import React, { useState } from 'react';
import { wmsApi, masterDataApi } from '../../services/api';

const ItemInquiry: React.FC = () => {
  const [searchCode, setSearchCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [itemData, setItemData] = useState<any>(null);
  const [inventory, setInventory] = useState<any[]>([]);
  const [pickTasks, setPickTasks] = useState<any[]>([]);
  const [batches, setBatches] = useState<any[]>([]);

  const searchItem = async () => {
    if (!searchCode.trim()) {
      alert('Please enter an item code');
      return;
    }

    try {
      setLoading(true);

      // Get all items
      const itemsRes = await masterDataApi.getItems();
      const item = itemsRes.data.find((itm: any) => 
        itm.code.toLowerCase() === searchCode.toLowerCase() || 
        itm.name.toLowerCase().includes(searchCode.toLowerCase())
      );

      if (!item) {
        alert('Item not found');
        setLoading(false);
        return;
      }

      // Get inventory for this item
      const inventoryRes = await wmsApi.getInventory();
      const itemInventory = inventoryRes.data.filter((inv: any) => inv.itemId === item.id);

      setInventory(itemInventory);

      // Get pending pick tasks for this item (mock)
      const mockPickTasks = [
        {
          taskNumber: 'PT-001',
          orderType: 'Production Order',
          orderNumber: 'PROD-015',
          quantity: 50,
          pickedQuantity: 0,
          status: 'Pending',
          priority: 1,
          createdDate: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString()
        },
        {
          taskNumber: 'PT-005',
          orderType: 'Shipment',
          orderNumber: 'SHIP-089',
          quantity: 75,
          pickedQuantity: 0,
          status: 'Assigned',
          priority: 2,
          createdDate: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString()
        }
      ];

      setPickTasks(mockPickTasks);

      // Calculate metrics
      const totalQuantity = itemInventory.reduce((sum: number, inv: any) => sum + inv.quantity, 0);
      const totalValue = totalQuantity * (item.unitCost || 0);
      const uniqueLocations = new Set(itemInventory.map((inv: any) => inv.location?.name)).size;
      const uniqueBatches = new Set(itemInventory.map((inv: any) => inv.batchNumber)).size;
      const uniqueMRNs = new Set(itemInventory.map((inv: any) => inv.mrn).filter(Boolean)).size;
      const uniqueWarehouses = new Set(
        itemInventory.map((inv: any) => inv.location?.warehouse?.name || 'Unknown')
      ).size;

      // Reserved quantity (from pending pick tasks)
      const reservedQuantity = mockPickTasks.reduce((sum, pt) => sum + (pt.quantity - pt.pickedQuantity), 0);
      const availableQuantity = totalQuantity - reservedQuantity;

      // Quality breakdown
      const okInventory = itemInventory.filter((inv: any) => inv.qualityStatus === 1);
      const blockedInventory = itemInventory.filter((inv: any) => inv.qualityStatus === 2);
      const quarantineInventory = itemInventory.filter((inv: any) => inv.qualityStatus === 3);

      const okQty = okInventory.reduce((sum: number, inv: any) => sum + inv.quantity, 0);
      const blockedQty = blockedInventory.reduce((sum: number, inv: any) => sum + inv.quantity, 0);
      const quarantineQty = quarantineInventory.reduce((sum: number, inv: any) => sum + inv.quantity, 0);

      // Group by batch
      const batchGroups = itemInventory.reduce((acc: any, inv: any) => {
        const batch = inv.batchNumber || 'No Batch';
        if (!acc[batch]) {
          acc[batch] = {
            batchNumber: batch,
            mrns: new Set(),
            locations: new Set(),
            totalQuantity: 0,
            qualityBreakdown: { ok: 0, blocked: 0, quarantine: 0 }
          };
        }
        acc[batch].mrns.add(inv.mrn || 'N/A');
        acc[batch].locations.add(inv.location?.name || 'Unknown');
        acc[batch].totalQuantity += inv.quantity;
        if (inv.qualityStatus === 1) acc[batch].qualityBreakdown.ok += inv.quantity;
        if (inv.qualityStatus === 2) acc[batch].qualityBreakdown.blocked += inv.quantity;
        if (inv.qualityStatus === 3) acc[batch].qualityBreakdown.quarantine += inv.quantity;
        return acc;
      }, {});

      const batchList = Object.values(batchGroups).map((batch: any) => ({
        ...batch,
        mrns: Array.from(batch.mrns),
        locations: Array.from(batch.locations)
      }));

      setBatches(batchList);

      setItemData({
        ...item,
        totalQuantity,
        totalValue,
        reservedQuantity,
        availableQuantity,
        uniqueLocations,
        uniqueBatches,
        uniqueMRNs,
        uniqueWarehouses,
        okQty,
        blockedQty,
        quarantineQty,
        inventoryLines: itemInventory.length
      });

    } catch (err) {
      console.error('Failed to load item data', err);
      alert('Failed to load item data');
    } finally {
      setLoading(false);
    }
  };

  const handleTransfer = () => {
    alert('Transfer functionality would open transfer form with this item pre-selected');
  };

  const handleCreatePickTask = () => {
    alert('Create Pick Task functionality would open pick task form with this item pre-selected');
  };

  const handleCycleCount = () => {
    alert('Cycle Count functionality would open cycle count form with all locations containing this item');
  };

  return (
    <div>
      <div className="header">
        <h2>📦 Item Inquiry</h2>
      </div>

      {/* Alert */}
      <div className="alert alert-info" style={{ marginBottom: '20px' }}>
        <strong>ℹ️ Quick Item Lookup</strong><br />
        Search by item code or name to view all locations, batches, reserved quantity, and perform quick actions.
      </div>

      {/* Search Section */}
      <div className="card" style={{ padding: '20px', marginBottom: '30px' }}>
        <div style={{ display: 'flex', gap: '10px', alignItems: 'flex-end' }}>
          <div style={{ flex: 1 }}>
            <label><strong>Item Code or Name:</strong></label>
            <input
              type="text"
              className="form-control"
              placeholder="Enter item code or name..."
              value={searchCode}
              onChange={(e) => setSearchCode(e.target.value)}
              onKeyPress={(e) => e.key === 'Enter' && searchItem()}
            />
          </div>
          <button 
            className="btn btn-primary" 
            onClick={searchItem}
            disabled={loading}
            style={{ height: '38px' }}
          >
            {loading ? '🔍 Searching...' : '🔍 Search'}
          </button>
        </div>
      </div>

      {itemData && (
        <>
          {/* Item Header */}
          <div className="card" style={{ 
            padding: '20px', 
            marginBottom: '30px', 
            background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)', 
            color: 'white' 
          }}>
            <div style={{ marginBottom: '15px' }}>
              <h3 style={{ margin: 0 }}>{itemData.code} - {itemData.name}</h3>
              <div style={{ fontSize: '14px', opacity: 0.9, marginTop: '5px' }}>
                Type: <strong>{itemData.itemType || 'N/A'}</strong> | 
                UoM: <strong>{itemData.uom || 'N/A'}</strong> | 
                Unit Cost: <strong>${(itemData.unitCost || 0).toFixed(2)}</strong>
              </div>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(6, 1fr)', gap: '15px' }}>
              <div>
                <div style={{ fontSize: '12px', opacity: 0.9 }}>Total Quantity</div>
                <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{itemData.totalQuantity.toFixed(2)}</div>
              </div>
              <div>
                <div style={{ fontSize: '12px', opacity: 0.9 }}>Reserved</div>
                <div style={{ fontSize: '24px', fontWeight: 'bold', color: '#ffc107' }}>{itemData.reservedQuantity}</div>
              </div>
              <div>
                <div style={{ fontSize: '12px', opacity: 0.9 }}>Available</div>
                <div style={{ fontSize: '24px', fontWeight: 'bold', color: '#90EE90' }}>{itemData.availableQuantity.toFixed(2)}</div>
              </div>
              <div>
                <div style={{ fontSize: '12px', opacity: 0.9 }}>Total Value</div>
                <div style={{ fontSize: '24px', fontWeight: 'bold' }}>${itemData.totalValue.toFixed(0)}</div>
              </div>
              <div>
                <div style={{ fontSize: '12px', opacity: 0.9 }}>Locations</div>
                <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{itemData.uniqueLocations}</div>
              </div>
              <div>
                <div style={{ fontSize: '12px', opacity: 0.9 }}>Batches</div>
                <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{itemData.uniqueBatches}</div>
              </div>
            </div>
          </div>

          {/* Quality & Actions */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px', marginBottom: '30px' }}>
            <div className="card" style={{ padding: '15px' }}>
              <h5>📊 Quality Status Breakdown</h5>
              <div style={{ marginTop: '15px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
                  <span>OK (Available):</span>
                  <strong className="text-success">{itemData.okQty.toFixed(2)}</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
                  <span>Blocked:</span>
                  <strong className="text-danger">{itemData.blockedQty.toFixed(2)}</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
                  <span>Quarantine:</span>
                  <strong className="text-warning">{itemData.quarantineQty.toFixed(2)}</strong>
                </div>
                <div style={{ marginTop: '15px', padding: '10px', background: '#f8f9fa', borderRadius: '4px' }}>
                  <div style={{ fontSize: '12px', marginBottom: '5px' }}>
                    <strong>MRNs:</strong> {itemData.uniqueMRNs}
                  </div>
                  <div style={{ fontSize: '12px', marginBottom: '5px' }}>
                    <strong>Warehouses:</strong> {itemData.uniqueWarehouses}
                  </div>
                  <div style={{ fontSize: '12px' }}>
                    <strong>Inventory Lines:</strong> {itemData.inventoryLines}
                  </div>
                </div>
              </div>
            </div>

            <div className="card" style={{ padding: '15px' }}>
              <h5>⚡ Quick Actions</h5>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', marginTop: '15px' }}>
                <button 
                  className="btn btn-primary"
                  onClick={handleTransfer}
                >
                  🔄 Transfer Item
                </button>
                <button 
                  className="btn btn-success"
                  onClick={handleCreatePickTask}
                >
                  🎯 Create Pick Task
                </button>
                <button 
                  className="btn btn-info"
                  onClick={handleCycleCount}
                >
                  📊 Cycle Count (All Locations)
                </button>
              </div>
              {itemData.availableQuantity < itemData.reorderPoint && (
                <div style={{ marginTop: '15px', padding: '10px', background: '#fff3cd', borderRadius: '4px', fontSize: '12px' }}>
                  <strong>⚠️ Low Stock Warning!</strong><br />
                  Available quantity is below reorder point.
                </div>
              )}
            </div>
          </div>

          {/* Pending Pick Tasks */}
          {pickTasks.length > 0 && (
            <div className="card" style={{ padding: '15px', marginBottom: '30px' }}>
              <h5>🎯 Pending Pick Tasks (Reserved Quantity)</h5>
              <div className="table-container" style={{ marginTop: '15px' }}>
                <table>
                  <thead>
                    <tr>
                      <th>Task #</th>
                      <th>Order Type</th>
                      <th>Order #</th>
                      <th>Quantity Required</th>
                      <th>Picked</th>
                      <th>Remaining</th>
                      <th>Status</th>
                      <th>Priority</th>
                      <th>Created Date</th>
                    </tr>
                  </thead>
                  <tbody>
                    {pickTasks.map((task, idx) => (
                      <tr key={idx} style={{ background: '#fff3cd' }}>
                        <td><strong>{task.taskNumber}</strong></td>
                        <td>{task.orderType}</td>
                        <td>{task.orderNumber}</td>
                        <td>{task.quantity}</td>
                        <td>{task.pickedQuantity}</td>
                        <td><strong style={{ color: '#dc3545' }}>{task.quantity - task.pickedQuantity}</strong></td>
                        <td>
                          <span className={`badge badge-${task.status === 'Pending' ? 'warning' : 'info'}`}>
                            {task.status}
                          </span>
                        </td>
                        <td>
                          <span className={`badge badge-${task.priority === 1 ? 'danger' : task.priority === 2 ? 'warning' : 'secondary'}`}>
                            P{task.priority}
                          </span>
                        </td>
                        <td>{new Date(task.createdDate).toLocaleDateString()}</td>
                      </tr>
                    ))}
                    <tr style={{ background: '#f8f9fa', fontWeight: 'bold' }}>
                      <td colSpan={5}>TOTAL RESERVED:</td>
                      <td>{itemData.reservedQuantity}</td>
                      <td colSpan={3}></td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* Batch Breakdown */}
          <div className="card" style={{ padding: '15px', marginBottom: '30px' }}>
            <h5>📦 Inventory by Batch</h5>
            <div className="table-container" style={{ marginTop: '15px' }}>
              <table>
                <thead>
                  <tr>
                    <th>Batch Number</th>
                    <th>MRNs</th>
                    <th>Locations</th>
                    <th>OK Qty</th>
                    <th>Blocked Qty</th>
                    <th>Quarantine Qty</th>
                    <th>Total Qty</th>
                  </tr>
                </thead>
                <tbody>
                  {batches.map((batch: any, idx) => (
                    <tr key={idx}>
                      <td><strong>{batch.batchNumber}</strong></td>
                      <td>{batch.mrns.join(', ')}</td>
                      <td>{batch.locations.join(', ')}</td>
                      <td className="text-success">{batch.qualityBreakdown.ok.toFixed(2)}</td>
                      <td className="text-danger">{batch.qualityBreakdown.blocked.toFixed(2)}</td>
                      <td className="text-warning">{batch.qualityBreakdown.quarantine.toFixed(2)}</td>
                      <td><strong>{batch.totalQuantity.toFixed(2)}</strong></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {/* All Locations with Inventory */}
          <div className="card" style={{ padding: '15px' }}>
            <h5>📍 All Locations with Inventory</h5>
            <div className="table-container" style={{ marginTop: '15px' }}>
              <table>
                <thead>
                  <tr>
                    <th>Location</th>
                    <th>Warehouse</th>
                    <th>Batch</th>
                    <th>MRN</th>
                    <th>Quantity</th>
                    <th>Quality Status</th>
                    <th>Last Movement</th>
                  </tr>
                </thead>
                <tbody>
                  {inventory
                    .sort((a, b) => b.quantity - a.quantity)
                    .map((inv, idx) => (
                      <tr key={idx}>
                        <td><strong>{inv.location?.name || 'Unknown'}</strong></td>
                        <td>{inv.location?.warehouse?.name || 'Unknown'}</td>
                        <td>{inv.batchNumber}</td>
                        <td>{inv.mrn || '-'}</td>
                        <td>{inv.quantity.toFixed(2)}</td>
                        <td>
                          <span className={`badge badge-${inv.qualityStatus === 1 ? 'success' : inv.qualityStatus === 2 ? 'danger' : 'warning'}`}>
                            {inv.qualityStatus === 1 ? 'OK' : inv.qualityStatus === 2 ? 'Blocked' : 'Quarantine'}
                          </span>
                        </td>
                        <td>{new Date(inv.lastMovementDate || inv.createdAt).toLocaleDateString()}</td>
                      </tr>
                    ))}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}
    </div>
  );
};

export default ItemInquiry;
