import React, { useEffect, useState } from 'react';
import { wmsApi } from '../../services/api';

const InventoryByMRN: React.FC = () => {
  const [inventory, setInventory] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [filterStatus, setFilterStatus] = useState<'all' | 'active' | 'depleted'>('active');

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

  // Group by MRN
  const inventoryByMRN = inventory
    .filter((inv: any) => inv.mrn) // Only items with MRN
    .reduce((acc: any, inv: any) => {
      const mrn = inv.mrn;
      if (!acc[mrn]) {
        acc[mrn] = {
          mrn: mrn,
          items: [],
          totalQuantity: 0,
          locations: new Set(),
          batches: new Set(),
        };
      }
      acc[mrn].items.push(inv);
      acc[mrn].totalQuantity += inv.quantity;
      acc[mrn].locations.add(inv.location?.name);
      acc[mrn].batches.add(inv.batchNumber);
      return acc;
    }, {});

  // Convert to array and sort
  const mrnList = Object.values(inventoryByMRN).sort((a: any, b: any) => 
    b.totalQuantity - a.totalQuantity
  );

  // Apply filter
  const filteredMRNList = mrnList.filter((mrn: any) => {
    if (filterStatus === 'active') return mrn.totalQuantity > 0;
    if (filterStatus === 'depleted') return mrn.totalQuantity === 0;
    return true;
  });

  const totalMRNs = filteredMRNList.length;
  const totalQuantity = filteredMRNList.reduce((sum: number, mrn: any) => sum + mrn.totalQuantity, 0);
  const activeMRNs = mrnList.filter((mrn: any) => mrn.totalQuantity > 0).length;
  const depletedMRNs = mrnList.filter((mrn: any) => mrn.totalQuantity === 0).length;

  const exportToExcel = () => {
    const headers = ['MRN', 'Total Quantity', 'Locations', 'Batches', 'Item Count', 'Status'];
    const rows = filteredMRNList.map((mrn: any) => [
      mrn.mrn,
      mrn.totalQuantity.toFixed(2),
      Array.from(mrn.locations).join('; '),
      Array.from(mrn.batches).join('; '),
      mrn.items.length,
      mrn.totalQuantity > 0 ? 'Active' : 'Depleted',
    ]);

    const csv = [headers, ...rows].map(row => row.join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `inventory_by_mrn_${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
  };

  return (
    <div>
      <div className="header">
        <h2>🛃 Inventory by MRN (Customs)</h2>
        <button className="btn btn-success" onClick={exportToExcel}>
          📥 Export to Excel
        </button>
      </div>

      <div className="alert alert-info">
        <strong>ℹ️ Critical for Customs Compliance!</strong><br />
        This report shows inventory grouped by MRN (Master Reference Number). 
        Use this to track imported materials usage for customs declarations.
      </div>

      {/* Filter */}
      <div className="filters" style={{ marginBottom: '20px' }}>
        <label style={{ marginRight: '10px' }}>Filter:</label>
        <select
          className="form-control"
          style={{ width: '200px', display: 'inline-block' }}
          value={filterStatus}
          onChange={(e) => setFilterStatus(e.target.value as any)}
        >
          <option value="all">All MRNs</option>
          <option value="active">Active (Qty {'>'} 0)</option>
          <option value="depleted">Depleted (Qty = 0)</option>
        </select>
      </div>

      {/* Summary Cards */}
      <div className="summary-cards" style={{ 
        display: 'grid', 
        gridTemplateColumns: 'repeat(4, 1fr)', 
        gap: '15px', 
        marginBottom: '20px' 
      }}>
        <div className="card" style={{ padding: '15px', background: '#e3f2fd' }}>
          <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{totalMRNs}</div>
          <div style={{ fontSize: '12px', color: '#666' }}>Total MRNs</div>
        </div>
        <div className="card" style={{ padding: '15px', background: '#e8f5e9' }}>
          <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{activeMRNs}</div>
          <div style={{ fontSize: '12px', color: '#155724' }}>Active MRNs</div>
        </div>
        <div className="card" style={{ padding: '15px', background: '#f8d7da' }}>
          <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{depletedMRNs}</div>
          <div style={{ fontSize: '12px', color: '#721c24' }}>Depleted MRNs</div>
        </div>
        <div className="card" style={{ padding: '15px', background: '#fff3e0' }}>
          <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{totalQuantity.toFixed(0)}</div>
          <div style={{ fontSize: '12px', color: '#856404' }}>Total Quantity</div>
        </div>
      </div>

      {loading ? (
        <div className="loading">Loading inventory...</div>
      ) : (
        <>
          {filteredMRNList.map((mrnData: any) => (
            <div key={mrnData.mrn} style={{ marginBottom: '30px' }}>
              <div style={{ 
                background: mrnData.totalQuantity > 0 ? '#e8f5e9' : '#f8d7da',
                padding: '15px', 
                borderRadius: '4px',
                marginBottom: '10px',
              }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <div>
                    <strong style={{ fontSize: '18px' }}>🛃 MRN: {mrnData.mrn}</strong>
                    <span className={`badge ${mrnData.totalQuantity > 0 ? 'badge-success' : 'badge-danger'}`}
                      style={{ marginLeft: '10px' }}>
                      {mrnData.totalQuantity > 0 ? 'Active' : 'Depleted'}
                    </span>
                  </div>
                  <div style={{ fontSize: '14px' }}>
                    <strong>Total Qty:</strong> {mrnData.totalQuantity.toFixed(2)} | 
                    <strong style={{ marginLeft: '10px' }}>Locations:</strong> {mrnData.locations.size} | 
                    <strong style={{ marginLeft: '10px' }}>Batches:</strong> {mrnData.batches.size} |
                    <strong style={{ marginLeft: '10px' }}>Items:</strong> {mrnData.items.length}
                  </div>
                </div>
              </div>

              <div className="table-container">
                <table>
                  <thead>
                    <tr>
                      <th>Item Code</th>
                      <th>Item Name</th>
                      <th>Location</th>
                      <th>Batch</th>
                      <th>Quantity</th>
                      <th>Quality Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {mrnData.items.map((inv: any, idx: number) => (
                      <tr key={idx}>
                        <td><strong>{inv.item?.code}</strong></td>
                        <td>{inv.item?.name}</td>
                        <td>{inv.location?.name}</td>
                        <td>{inv.batchNumber || '-'}</td>
                        <td>
                          <strong>{inv.quantity.toFixed(2)}</strong> {inv.uoM?.code}
                        </td>
                        <td>
                          <span className={`badge badge-${
                            inv.qualityStatus === 1 ? 'success' : 
                            inv.qualityStatus === 2 ? 'danger' : 'warning'
                          }`}>
                            {inv.qualityStatus === 1 ? 'OK' : 
                             inv.qualityStatus === 2 ? 'Blocked' : 'Quarantine'}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ))}

          {filteredMRNList.length === 0 && (
            <div className="alert alert-warning">
              No MRN inventory found. Import materials with MRN tracking to see data here.
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default InventoryByMRN;
