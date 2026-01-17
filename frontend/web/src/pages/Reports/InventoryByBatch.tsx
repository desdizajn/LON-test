import React, { useEffect, useState } from 'react';
import { wmsApi } from '../../services/api';

const InventoryByBatch: React.FC = () => {
  const [inventory, setInventory] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchBatch, setSearchBatch] = useState('');

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

  // Group by batch
  const inventoryByBatch = inventory
    .filter((inv: any) => inv.batchNumber) // Only items with batch
    .reduce((acc: any, inv: any) => {
      const batch = inv.batchNumber;
      if (!acc[batch]) {
        acc[batch] = {
          batchNumber: batch,
          items: [],
          totalQuantity: 0,
          locations: new Set(),
          mrns: new Set(),
          itemCodes: new Set(),
        };
      }
      acc[batch].items.push(inv);
      acc[batch].totalQuantity += inv.quantity;
      acc[batch].locations.add(inv.location?.name);
      if (inv.mrn) acc[batch].mrns.add(inv.mrn);
      acc[batch].itemCodes.add(inv.item?.code);
      return acc;
    }, {});

  // Convert to array and sort by batch number
  const batchList = Object.values(inventoryByBatch).sort((a: any, b: any) => 
    b.batchNumber.localeCompare(a.batchNumber)
  );

  // Apply search filter
  const filteredBatchList = searchBatch
    ? batchList.filter((batch: any) => 
        batch.batchNumber.toLowerCase().includes(searchBatch.toLowerCase())
      )
    : batchList;

  const totalBatches = filteredBatchList.length;
  const totalQuantity = filteredBatchList.reduce((sum: number, batch: any) => sum + batch.totalQuantity, 0);
  const activeBatches = filteredBatchList.filter((batch: any) => batch.totalQuantity > 0).length;

  const exportToExcel = () => {
    const headers = ['Batch Number', 'Total Quantity', 'Locations', 'MRNs', 'Items', 'Item Codes'];
    const rows = filteredBatchList.map((batch: any) => [
      batch.batchNumber,
      batch.totalQuantity.toFixed(2),
      Array.from(batch.locations).join('; '),
      Array.from(batch.mrns).join('; '),
      batch.items.length,
      Array.from(batch.itemCodes).join('; '),
    ]);

    const csv = [headers, ...rows].map(row => row.join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `inventory_by_batch_${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
  };

  return (
    <div>
      <div className="header">
        <h2>🏷️ Inventory by Batch</h2>
        <button className="btn btn-success" onClick={exportToExcel}>
          📥 Export to Excel
        </button>
      </div>

      <div className="alert alert-info">
        <strong>ℹ️ Batch Traceability</strong><br />
        This report shows inventory grouped by batch number. 
        Use this for batch recalls, expiry management, and quality tracking.
      </div>

      {/* Search */}
      <div className="filters" style={{ marginBottom: '20px' }}>
        <label style={{ marginRight: '10px' }}>Search Batch:</label>
        <input
          type="text"
          className="form-control"
          style={{ width: '300px', display: 'inline-block' }}
          placeholder="Enter batch number..."
          value={searchBatch}
          onChange={(e) => setSearchBatch(e.target.value)}
        />
      </div>

      {/* Summary Cards */}
      <div className="summary-cards" style={{ 
        display: 'grid', 
        gridTemplateColumns: 'repeat(3, 1fr)', 
        gap: '15px', 
        marginBottom: '20px' 
      }}>
        <div className="card" style={{ padding: '15px', background: '#e3f2fd' }}>
          <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{totalBatches}</div>
          <div style={{ fontSize: '12px', color: '#0d47a1' }}>Total Batches</div>
        </div>
        <div className="card" style={{ padding: '15px', background: '#e8f5e9' }}>
          <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{activeBatches}</div>
          <div style={{ fontSize: '12px', color: '#155724' }}>Active Batches</div>
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
          {filteredBatchList.map((batchData: any) => (
            <div key={batchData.batchNumber} style={{ marginBottom: '30px' }}>
              <div style={{ 
                background: '#e8f5e9',
                padding: '15px', 
                borderRadius: '4px',
                marginBottom: '10px',
              }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <div>
                    <strong style={{ fontSize: '18px' }}>🏷️ Batch: {batchData.batchNumber}</strong>
                    {batchData.mrns.size > 0 && (
                      <span style={{ marginLeft: '10px', color: '#666' }}>
                        MRN: {Array.from(batchData.mrns).join(', ')}
                      </span>
                    )}
                  </div>
                  <div style={{ fontSize: '14px' }}>
                    <strong>Total Qty:</strong> {batchData.totalQuantity.toFixed(2)} | 
                    <strong style={{ marginLeft: '10px' }}>Locations:</strong> {batchData.locations.size} | 
                    <strong style={{ marginLeft: '10px' }}>Lines:</strong> {batchData.items.length}
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
                      <th>MRN</th>
                      <th>Quantity</th>
                      <th>Quality Status</th>
                      <th>Last Movement</th>
                    </tr>
                  </thead>
                  <tbody>
                    {batchData.items.map((inv: any, idx: number) => (
                      <tr key={idx}>
                        <td><strong>{inv.item?.code}</strong></td>
                        <td>{inv.item?.name}</td>
                        <td>{inv.location?.name}</td>
                        <td>{inv.mrn || '-'}</td>
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

          {filteredBatchList.length === 0 && (
            <div className="alert alert-warning">
              No batches found. {searchBatch && 'Try a different search term.'}
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default InventoryByBatch;
