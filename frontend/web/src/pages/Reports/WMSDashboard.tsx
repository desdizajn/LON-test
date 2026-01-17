import React, { useEffect, useState } from 'react';
import { wmsApi, analyticsApi } from '../../services/api';

const WMSDashboard: React.FC = () => {
  const [inventory, setInventory] = useState<any[]>([]);
  const [receipts, setReceipts] = useState<any[]>([]);
  const [shipments, setShipments] = useState<any[]>([]);
  const [pickTasks, setPickTasks] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [inventoryRes, receiptsRes, shipmentsRes, pickTasksRes] = await Promise.all([
        wmsApi.getInventory(),
        wmsApi.getReceipts(1, 100), // Last 100
        wmsApi.getShipments(1, 100),
        wmsApi.getPickTasks(),
      ]);
      setInventory(inventoryRes.data);
      setReceipts(receiptsRes.data);
      setShipments(shipmentsRes.data);
      setPickTasks(pickTasksRes.data);
    } catch (err) {
      console.error('Failed to load dashboard data', err);
    } finally {
      setLoading(false);
    }
  };

  // Calculate KPIs
  const totalInventoryValue = inventory.reduce((sum, inv) => sum + (inv.quantity * (inv.item?.unitCost || 0)), 0);
  const totalItems = inventory.length;
  const uniqueItems = new Set(inventory.map(inv => inv.itemId)).size;
  const totalLocations = new Set(inventory.map(inv => inv.locationId)).size;

  // Quality Status Breakdown
  const okInventory = inventory.filter(inv => inv.qualityStatus === 1);
  const blockedInventory = inventory.filter(inv => inv.qualityStatus === 2);
  const quarantineInventory = inventory.filter(inv => inv.qualityStatus === 3);
  const blockedPercentage = totalItems > 0 ? ((blockedInventory.length + quarantineInventory.length) / totalItems * 100).toFixed(1) : 0;

  // Pick Task Metrics
  const pendingPickTasks = pickTasks.filter(pt => pt.status === 1).length;
  const completedPickTasks = pickTasks.filter(pt => pt.status === 4).length;
  const pickTaskCompletionRate = pickTasks.length > 0 ? (completedPickTasks / pickTasks.length * 100).toFixed(1) : 0;

  // Movement Metrics (Last 30 days)
  const thirtyDaysAgo = new Date();
  thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30);
  
  const recentReceipts = receipts.filter(r => new Date(r.receiptDate) >= thirtyDaysAgo);
  const recentShipments = shipments.filter(s => new Date(s.shipmentDate) >= thirtyDaysAgo);

  // Top Items by Quantity
  const itemQuantities = inventory.reduce((acc: any, inv: any) => {
    const itemCode = inv.item?.code || 'Unknown';
    const itemName = inv.item?.name || 'Unknown';
    if (!acc[itemCode]) {
      acc[itemCode] = { code: itemCode, name: itemName, quantity: 0 };
    }
    acc[itemCode].quantity += inv.quantity;
    return acc;
  }, {});
  const topItems = Object.values(itemQuantities)
    .sort((a: any, b: any) => b.quantity - a.quantity)
    .slice(0, 10);

  // Inventory by Location (Top 10)
  const locationQuantities = inventory.reduce((acc: any, inv: any) => {
    const locationName = inv.location?.name || 'Unknown';
    if (!acc[locationName]) {
      acc[locationName] = { name: locationName, quantity: 0, items: 0 };
    }
    acc[locationName].quantity += inv.quantity;
    acc[locationName].items++;
    return acc;
  }, {});
  const topLocations = Object.values(locationQuantities)
    .sort((a: any, b: any) => b.items - a.items)
    .slice(0, 10);

  // Daily movements (last 7 days)
  const last7Days = Array.from({ length: 7 }, (_, i) => {
    const date = new Date();
    date.setDate(date.getDate() - (6 - i));
    return date.toISOString().split('T')[0];
  });

  const dailyMovements = last7Days.map(date => {
    const receiptsCount = receipts.filter(r => 
      new Date(r.receiptDate).toISOString().split('T')[0] === date
    ).length;
    const shipmentsCount = shipments.filter(s => 
      new Date(s.shipmentDate).toISOString().split('T')[0] === date
    ).length;
    return { date, receipts: receiptsCount, shipments: shipmentsCount };
  });

  if (loading) return <div className="loading">Loading WMS Dashboard...</div>;

  return (
    <div>
      <div className="header">
        <h2>📊 WMS Dashboard</h2>
        <button className="btn btn-primary" onClick={loadData}>
          🔄 Refresh
        </button>
      </div>

      {/* Main KPI Cards */}
      <div className="kpi-cards" style={{ 
        display: 'grid', 
        gridTemplateColumns: 'repeat(4, 1fr)', 
        gap: '15px', 
        marginBottom: '30px' 
      }}>
        <div className="card" style={{ padding: '20px', background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)', color: 'white' }}>
          <div style={{ fontSize: '14px', marginBottom: '5px' }}>Total Inventory Value</div>
          <div style={{ fontSize: '32px', fontWeight: 'bold' }}>${totalInventoryValue.toFixed(0)}</div>
        </div>
        <div className="card" style={{ padding: '20px', background: 'linear-gradient(135deg, #f093fb 0%, #f5576c 100%)', color: 'white' }}>
          <div style={{ fontSize: '14px', marginBottom: '5px' }}>Total Inventory Lines</div>
          <div style={{ fontSize: '32px', fontWeight: 'bold' }}>{totalItems}</div>
          <div style={{ fontSize: '12px' }}>{uniqueItems} unique items</div>
        </div>
        <div className="card" style={{ padding: '20px', background: 'linear-gradient(135deg, #4facfe 0%, #00f2fe 100%)', color: 'white' }}>
          <div style={{ fontSize: '14px', marginBottom: '5px' }}>Active Locations</div>
          <div style={{ fontSize: '32px', fontWeight: 'bold' }}>{totalLocations}</div>
        </div>
        <div className="card" style={{ padding: '20px', background: 'linear-gradient(135deg, #43e97b 0%, #38f9d7 100%)', color: 'white' }}>
          <div style={{ fontSize: '14px', marginBottom: '5px' }}>Blocked Inventory</div>
          <div style={{ fontSize: '32px', fontWeight: 'bold' }}>{blockedPercentage}%</div>
          <div style={{ fontSize: '12px' }}>Target: {'<'}5%</div>
        </div>
      </div>

      {/* Secondary KPIs */}
      <div className="secondary-kpis" style={{ 
        display: 'grid', 
        gridTemplateColumns: 'repeat(3, 1fr)', 
        gap: '15px', 
        marginBottom: '30px' 
      }}>
        <div className="card" style={{ padding: '15px' }}>
          <h5>📦 Quality Status</h5>
          <div style={{ marginTop: '10px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '5px' }}>
              <span>OK (Available)</span>
              <strong className="text-success">{okInventory.length} ({(okInventory.length / totalItems * 100).toFixed(1)}%)</strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '5px' }}>
              <span>Blocked</span>
              <strong className="text-danger">{blockedInventory.length} ({(blockedInventory.length / totalItems * 100).toFixed(1)}%)</strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span>Quarantine</span>
              <strong className="text-warning">{quarantineInventory.length} ({(quarantineInventory.length / totalItems * 100).toFixed(1)}%)</strong>
            </div>
          </div>
        </div>

        <div className="card" style={{ padding: '15px' }}>
          <h5>🎯 Pick Tasks</h5>
          <div style={{ marginTop: '10px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '5px' }}>
              <span>Pending</span>
              <strong className="text-warning">{pendingPickTasks}</strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '5px' }}>
              <span>Completed</span>
              <strong className="text-success">{completedPickTasks}</strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span>Completion Rate</span>
              <strong>{pickTaskCompletionRate}%</strong>
            </div>
          </div>
        </div>

        <div className="card" style={{ padding: '15px' }}>
          <h5>📈 Movement (Last 30 Days)</h5>
          <div style={{ marginTop: '10px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '5px' }}>
              <span>📥 Receipts</span>
              <strong className="text-success">{recentReceipts.length}</strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '5px' }}>
              <span>📤 Shipments</span>
              <strong className="text-info">{recentShipments.length}</strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span>Net Movement</span>
              <strong>{recentReceipts.length - recentShipments.length}</strong>
            </div>
          </div>
        </div>
      </div>

      {/* Charts/Tables Section */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px', marginBottom: '30px' }}>
        {/* Top 10 Items by Quantity */}
        <div className="card" style={{ padding: '15px' }}>
          <h5>📊 Top 10 Items by Quantity</h5>
          <div className="table-container" style={{ marginTop: '15px' }}>
            <table>
              <thead>
                <tr>
                  <th>Item Code</th>
                  <th>Item Name</th>
                  <th>Total Qty</th>
                </tr>
              </thead>
              <tbody>
                {topItems.map((item: any, idx) => (
                  <tr key={idx}>
                    <td><strong>{item.code}</strong></td>
                    <td>{item.name}</td>
                    <td><strong>{item.quantity.toFixed(2)}</strong></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Top 10 Locations */}
        <div className="card" style={{ padding: '15px' }}>
          <h5>📍 Top 10 Locations by Items</h5>
          <div className="table-container" style={{ marginTop: '15px' }}>
            <table>
              <thead>
                <tr>
                  <th>Location</th>
                  <th>Items</th>
                  <th>Total Qty</th>
                </tr>
              </thead>
              <tbody>
                {topLocations.map((loc: any, idx) => (
                  <tr key={idx}>
                    <td><strong>{loc.name}</strong></td>
                    <td>{loc.items}</td>
                    <td>{loc.quantity.toFixed(2)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      {/* Daily Movements Chart (Simple Table) */}
      <div className="card" style={{ padding: '15px', marginBottom: '30px' }}>
        <h5>📈 Daily Movements (Last 7 Days)</h5>
        <div className="table-container" style={{ marginTop: '15px' }}>
          <table>
            <thead>
              <tr>
                <th>Date</th>
                <th>📥 Receipts</th>
                <th>📤 Shipments</th>
                <th>Net</th>
              </tr>
            </thead>
            <tbody>
              {dailyMovements.map((day: any) => (
                <tr key={day.date}>
                  <td><strong>{new Date(day.date).toLocaleDateString()}</strong></td>
                  <td className="text-success">{day.receipts}</td>
                  <td className="text-info">{day.shipments}</td>
                  <td>
                    <strong className={day.receipts - day.shipments >= 0 ? 'text-success' : 'text-danger'}>
                      {day.receipts - day.shipments > 0 ? '+' : ''}{day.receipts - day.shipments}
                    </strong>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Alerts Section */}
      <div className="alerts-section">
        <h5>⚠️ Alerts & Recommendations</h5>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '15px', marginTop: '15px' }}>
          {parseFloat(blockedPercentage as string) > 5 && (
            <div className="alert alert-danger">
              <strong>⚠️ High Blocked Inventory!</strong><br />
              {blockedPercentage}% of inventory is blocked (target {'<'}5%). Review and release quality holds.
            </div>
          )}
          {pendingPickTasks > 10 && (
            <div className="alert alert-warning">
              <strong>⚠️ High Pending Pick Tasks!</strong><br />
              {pendingPickTasks} pick tasks pending. Assign to employees to avoid delays.
            </div>
          )}
          {parseFloat(blockedPercentage as string) <= 5 && pendingPickTasks <= 10 && (
            <div className="alert alert-success">
              <strong>✅ All Systems Nominal!</strong><br />
              No critical alerts. WMS operating within normal parameters.
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default WMSDashboard;
