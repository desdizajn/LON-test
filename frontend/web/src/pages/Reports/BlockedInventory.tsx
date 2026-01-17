import React, { useEffect, useState } from 'react';
import { wmsApi } from '../../services/api';

const BlockedInventory: React.FC = () => {
  const [inventory, setInventory] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [filterStatus, setFilterStatus] = useState<number>(2); // Default to Blocked

  useEffect(() => {
    loadInventory();
  }, []);

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
    const confirm = window.confirm('Release this inventory to OK status?');
    if (!confirm) return;

    try {
      await wmsApi.updateQualityStatus({
        inventoryBalanceId: inventoryId,
        newQualityStatus: 1, // OK
        reason: 'Released from quality hold',
        notes: 'Released via Blocked Inventory Report',
      });
      alert('Inventory released successfully!');
      loadInventory();
    } catch (err: any) {
      alert('Failed to release inventory: ' + (err.response?.data?.error || err.message));
    }
  };

  // Filter by quality status
  const filteredInventory = inventory.filter((inv: any) => 
    inv.qualityStatus === filterStatus
  );

  // Calculate aging (days since last movement or created)
  const calculateAging = (lastMovementDate?: string, createdAt?: string) => {
    const date = lastMovementDate || createdAt;
    if (!date) return 0;
    const diffTime = Math.abs(new Date().getTime() - new Date(date).getTime());
    return Math.ceil(diffTime / (1000 * 60 * 60 * 24));
  };

  // Add aging to inventory
  const inventoryWithAging = filteredInventory.map((inv: any) => ({
    ...inv,
    agingDays: calculateAging(inv.lastMovementDate, inv.createdAt),
  }));

  // Sort by aging (oldest first)
  const sortedInventory = inventoryWithAging.sort((a, b) => b.agingDays - a.agingDays);

  const totalQuantity = sortedInventory.reduce((sum, inv) => sum + inv.quantity, 0);
  const totalItems = sortedInventory.length;
  const oldItems = sortedInventory.filter((inv) => inv.agingDays > 30).length;
  const criticalItems = sortedInventory.filter((inv) => inv.agingDays > 90).length;

  const getAgingBadge = (days: number) => {
    if (days > 90) return <span className="badge badge-danger">Critical ({days}d)</span>;
    if (days > 30) return <span className="badge badge-warning">Old ({days}d)</span>;
    return <span className="badge badge-info">{days} days</span>;
  };

  const exportToExcel = () => {
    const headers = ['Item Code', 'Item Name', 'Location', 'Batch', 'MRN', 'Quantity', 'UoM', 'Status', 'Aging (days)', 'Last Movement'];
    const rows = sortedInventory.map((inv: any) => [
      inv.item?.code || '',
      inv.item?.name || '',
      inv.location?.name || '',
      inv.batchNumber || '',
      inv.mrn || '',
      inv.quantity.toFixed(2),
      inv.uoM?.code || '',
      inv.qualityStatus === 2 ? 'Blocked' : 'Quarantine',
      inv.agingDays,
      inv.lastMovementDate ? new Date(inv.lastMovementDate).toLocaleDateString() : '',
    ]);

    const csv = [headers, ...rows].map(row => row.join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `blocked_inventory_${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
  };

  return (
    <div>
      <div className="header">
        <h2>🔒 Blocked & Quarantine Inventory</h2>
        <button className="btn btn-success" onClick={exportToExcel}>
          📥 Export to Excel
        </button>
      </div>

      <div className="alert alert-warning">
        <strong>⚠️ Quality Hold Inventory</strong><br />
        This inventory is on hold and cannot be issued for production or shipment.
        Review and release when quality issues are resolved.
      </div>

      {/* Filter */}
      <div className="filters" style={{ marginBottom: '20px' }}>
        <label style={{ marginRight: '10px' }}>Status:</label>
        <select
          className="form-control"
          style={{ width: '200px', display: 'inline-block' }}
          value={filterStatus}
          onChange={(e) => setFilterStatus(parseInt(e.target.value))}
        >
          <option value="2">Blocked</option>
          <option value="3">Quarantine</option>
        </select>
      </div>

      {/* Summary Cards */}
      <div className="summary-cards" style={{ 
        display: 'grid', 
        gridTemplateColumns: 'repeat(4, 1fr)', 
        gap: '15px', 
        marginBottom: '20px' 
      }}>
        <div className="card" style={{ padding: '15px', background: '#f8d7da' }}>
          <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{totalItems}</div>
          <div style={{ fontSize: '12px', color: '#721c24' }}>Total Items on Hold</div>
        </div>
        <div className="card" style={{ padding: '15px', background: '#fff3cd' }}>
          <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{oldItems}</div>
          <div style={{ fontSize: '12px', color: '#856404' }}>Aging {'>'}30 days</div>
        </div>
        <div className="card" style={{ padding: '15px', background: '#f8d7da' }}>
          <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{criticalItems}</div>
          <div style={{ fontSize: '12px', color: '#721c24' }}>Critical {'>'}90 days</div>
        </div>
        <div className="card" style={{ padding: '15px', background: '#e7f3ff' }}>
          <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{totalQuantity.toFixed(0)}</div>
          <div style={{ fontSize: '12px', color: '#004085' }}>Total Quantity</div>
        </div>
      </div>

      {loading ? (
        <div className="loading">Loading inventory...</div>
      ) : (
        <>
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>Item Code</th>
                  <th>Item Name</th>
                  <th>Location</th>
                  <th>Batch</th>
                  <th>MRN</th>
                  <th>Quantity</th>
                  <th>Status</th>
                  <th>Aging</th>
                  <th>Last Movement</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {sortedInventory.length === 0 ? (
                  <tr>
                    <td colSpan={10} style={{ textAlign: 'center', padding: '20px' }}>
                      ✅ No {filterStatus === 2 ? 'blocked' : 'quarantine'} inventory found. Great!
                    </td>
                  </tr>
                ) : (
                  sortedInventory.map((inv: any, idx: number) => (
                    <tr key={idx} className={inv.agingDays > 90 ? 'table-danger' : inv.agingDays > 30 ? 'table-warning' : ''}>
                      <td><strong>{inv.item?.code}</strong></td>
                      <td>{inv.item?.name}</td>
                      <td>{inv.location?.name}</td>
                      <td>{inv.batchNumber || '-'}</td>
                      <td>{inv.mrn || '-'}</td>
                      <td>
                        <strong>{inv.quantity.toFixed(2)}</strong> {inv.uoM?.code}
                      </td>
                      <td>
                        <span className={`badge badge-${inv.qualityStatus === 2 ? 'danger' : 'warning'}`}>
                          {inv.qualityStatus === 2 ? 'Blocked' : 'Quarantine'}
                        </span>
                      </td>
                      <td>{getAgingBadge(inv.agingDays)}</td>
                      <td>
                        {inv.lastMovementDate 
                          ? new Date(inv.lastMovementDate).toLocaleDateString()
                          : '-'}
                      </td>
                      <td>
                        <button
                          className="btn btn-xs btn-success"
                          onClick={() => handleReleaseClick(inv.id)}
                          title="Release to OK status"
                        >
                          ✅ Release
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {sortedInventory.length > 0 && (
            <div className="alert alert-info" style={{ marginTop: '20px' }}>
              <strong>💡 Tip:</strong> Items aging {'>'}30 days should be reviewed. 
              Items aging {'>'}90 days may need to be scrapped or written off.
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default BlockedInventory;
