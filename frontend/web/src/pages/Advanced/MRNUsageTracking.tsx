import React, { useEffect, useState } from 'react';
import { wmsApi, customsApi } from '../../services/api';

const MRNUsageTracking: React.FC = () => {
  const [mrns, setMRNs] = useState<any[]>([]);
  const [inventory, setInventory] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedMRN, setSelectedMRN] = useState<string>('');
  const [mrnDetails, setMRNDetails] = useState<any>(null);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const inventoryRes = await wmsApi.getInventory();
      setInventory(inventoryRes.data);

      // Group by MRN
      const mrnGroups = inventoryRes.data.reduce((acc: any, inv: any) => {
        if (!inv.mrn) return acc;
        
        if (!acc[inv.mrn]) {
          acc[inv.mrn] = {
            mrn: inv.mrn,
            items: [],
            batches: new Set(),
            locations: new Set(),
            totalQuantity: 0,
            totalValue: 0,
            status: 'Active',
            importDate: inv.createdAt, // Assuming first receipt date
          };
        }

        acc[inv.mrn].items.push(inv);
        acc[inv.mrn].batches.add(inv.batchNumber);
        acc[inv.mrn].locations.add(inv.location?.name || 'Unknown');
        acc[inv.mrn].totalQuantity += inv.quantity;
        acc[inv.mrn].totalValue += inv.quantity * (inv.item?.unitCost || 0);

        return acc;
      }, {});

      const mrnList = Object.values(mrnGroups).map((mrn: any) => ({
        ...mrn,
        batches: Array.from(mrn.batches),
        locations: Array.from(mrn.locations),
        status: mrn.totalQuantity > 0 ? 'Active' : 'Depleted'
      }));

      setMRNs(mrnList);
    } catch (err) {
      console.error('Failed to load MRN usage data', err);
    } finally {
      setLoading(false);
    }
  };

  const viewMRNDetails = (mrn: string) => {
    const mrnData = mrns.find(m => m.mrn === mrn);
    if (!mrnData) return;

    // Calculate detailed consumption
    const originalImportQuantity = 1000; // Mock - would come from customs import document
    const originalImportValue = 15000; // Mock - total customs value
    const dutyRate = 0.10; // Mock - 10% duty rate
    const totalDuty = originalImportValue * dutyRate;

    // Calculate consumption
    const consumed = originalImportQuantity - mrnData.totalQuantity;
    const consumedPercentage = (consumed / originalImportQuantity * 100).toFixed(1);

    // Mock production orders that consumed this MRN
    const productionOrders = [
      {
        orderNumber: 'PROD-001',
        item: { code: 'FG-001', name: 'Finished Good A' },
        consumedQuantity: 300,
        consumedValue: 4500,
        proportionalDuty: (4500 / originalImportValue) * totalDuty,
        productionDate: new Date(Date.now() - 60 * 24 * 60 * 60 * 1000).toISOString(),
        outputBatch: 'FG-BATCH-2024-001',
        outputQuantity: 500
      },
      {
        orderNumber: 'PROD-002',
        item: { code: 'FG-002', name: 'Finished Good B' },
        consumedQuantity: 250,
        consumedValue: 3750,
        proportionalDuty: (3750 / originalImportValue) * totalDuty,
        productionDate: new Date(Date.now() - 45 * 24 * 60 * 60 * 1000).toISOString(),
        outputBatch: 'FG-BATCH-2024-002',
        outputQuantity: 400
      },
      {
        orderNumber: 'PROD-005',
        item: { code: 'FG-001', name: 'Finished Good A' },
        consumedQuantity: 200,
        consumedValue: 3000,
        proportionalDuty: (3000 / originalImportValue) * totalDuty,
        productionDate: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
        outputBatch: 'FG-BATCH-2024-005',
        outputQuantity: 350
      }
    ];

    const totalConsumed = productionOrders.reduce((sum, po) => sum + po.consumedQuantity, 0);
    const totalConsumedValue = productionOrders.reduce((sum, po) => sum + po.consumedValue, 0);
    const totalAllocatedDuty = productionOrders.reduce((sum, po) => sum + po.proportionalDuty, 0);
    const remainingDuty = totalDuty - totalAllocatedDuty;

    setMRNDetails({
      ...mrnData,
      originalImportQuantity,
      originalImportValue,
      dutyRate,
      totalDuty,
      consumed,
      consumedPercentage,
      productionOrders,
      totalConsumed,
      totalConsumedValue,
      totalAllocatedDuty,
      remainingDuty
    });

    setSelectedMRN(mrn);
  };

  const exportForCustomsAudit = () => {
    if (!mrnDetails) return;

    const headers = [
      'MRN',
      'Original Import Qty',
      'Original Import Value',
      'Duty Rate %',
      'Total Duty',
      'Current Balance',
      'Consumed Qty',
      'Consumed Value',
      'Allocated Duty',
      'Remaining Duty',
      'Status'
    ];

    const summaryRow = [
      mrnDetails.mrn,
      mrnDetails.originalImportQuantity,
      mrnDetails.originalImportValue.toFixed(2),
      (mrnDetails.dutyRate * 100).toFixed(1),
      mrnDetails.totalDuty.toFixed(2),
      mrnDetails.totalQuantity.toFixed(2),
      mrnDetails.consumed.toFixed(2),
      mrnDetails.totalConsumedValue.toFixed(2),
      mrnDetails.totalAllocatedDuty.toFixed(2),
      mrnDetails.remainingDuty.toFixed(2),
      mrnDetails.status
    ];

    const detailHeaders = [
      '',
      'Production Order',
      'Finished Good',
      'Output Batch',
      'Output Qty',
      'Consumed Qty',
      'Consumed Value',
      'Proportional Duty',
      'Production Date'
    ];

    const detailRows = mrnDetails.productionOrders.map((po: any) => [
      '',
      po.orderNumber,
      `${po.item.code} - ${po.item.name}`,
      po.outputBatch,
      po.outputQuantity,
      po.consumedQuantity,
      po.consumedValue.toFixed(2),
      po.proportionalDuty.toFixed(2),
      new Date(po.productionDate).toLocaleDateString()
    ]);

    const csv = [
      headers,
      summaryRow,
      [],
      ['CONSUMPTION DETAILS:'],
      detailHeaders,
      ...detailRows
    ].map(row => row.join(',')).join('\n');

    const blob = new Blob([csv], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `MRN_${mrnDetails.mrn}_Customs_Audit_${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
  };

  if (loading) return <div className="loading">Loading MRN usage data...</div>;

  return (
    <div>
      <div className="header">
        <h2>🛃 MRN Usage Tracking</h2>
      </div>

      {/* Alert */}
      <div className="alert alert-warning" style={{ marginBottom: '20px' }}>
        <strong>⚠️ Customs Compliance - Critical Report</strong><br />
        Tracks imported materials consumption for customs duty calculations, LON/REK declarations, and customs audits.
        <br />
        <strong>Required for:</strong> Import duty allocation, Finished goods costing, Customs compliance, Audit documentation
      </div>

      {/* Summary Cards */}
      <div className="summary-cards" style={{ 
        display: 'grid', 
        gridTemplateColumns: 'repeat(4, 1fr)', 
        gap: '15px', 
        marginBottom: '30px' 
      }}>
        <div className="card" style={{ padding: '20px', textAlign: 'center' }}>
          <div style={{ fontSize: '12px', color: '#666', marginBottom: '5px' }}>Total MRNs</div>
          <div style={{ fontSize: '32px', fontWeight: 'bold' }}>{mrns.length}</div>
        </div>
        <div className="card" style={{ padding: '20px', textAlign: 'center' }}>
          <div style={{ fontSize: '12px', color: '#666', marginBottom: '5px' }}>Active MRNs</div>
          <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#28a745' }}>
            {mrns.filter(m => m.status === 'Active').length}
          </div>
        </div>
        <div className="card" style={{ padding: '20px', textAlign: 'center' }}>
          <div style={{ fontSize: '12px', color: '#666', marginBottom: '5px' }}>Depleted MRNs</div>
          <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#dc3545' }}>
            {mrns.filter(m => m.status === 'Depleted').length}
          </div>
        </div>
        <div className="card" style={{ padding: '20px', textAlign: 'center' }}>
          <div style={{ fontSize: '12px', color: '#666', marginBottom: '5px' }}>Total Value</div>
          <div style={{ fontSize: '32px', fontWeight: 'bold' }}>
            ${mrns.reduce((sum, m) => sum + m.totalValue, 0).toFixed(0)}
          </div>
        </div>
      </div>

      {/* MRN List */}
      <div className="card" style={{ padding: '15px', marginBottom: '30px' }}>
        <h5>📋 All MRNs Overview</h5>
        <div className="table-container" style={{ marginTop: '15px' }}>
          <table>
            <thead>
              <tr>
                <th>MRN</th>
                <th>Import Date</th>
                <th>Batches</th>
                <th>Locations</th>
                <th>Current Qty</th>
                <th>Current Value</th>
                <th>Status</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {mrns
                .sort((a, b) => new Date(b.importDate).getTime() - new Date(a.importDate).getTime())
                .map((mrn, idx) => (
                  <tr key={idx} style={{
                    background: mrn.status === 'Active' ? '#d4edda' : '#f8d7da'
                  }}>
                    <td><strong>{mrn.mrn}</strong></td>
                    <td>{new Date(mrn.importDate).toLocaleDateString()}</td>
                    <td>{mrn.batches.length}</td>
                    <td>{mrn.locations.length}</td>
                    <td>{mrn.totalQuantity.toFixed(2)}</td>
                    <td>${mrn.totalValue.toFixed(2)}</td>
                    <td>
                      <span className={`badge badge-${mrn.status === 'Active' ? 'success' : 'danger'}`}>
                        {mrn.status}
                      </span>
                    </td>
                    <td>
                      <button 
                        className="btn btn-sm btn-primary"
                        onClick={() => viewMRNDetails(mrn.mrn)}
                      >
                        📊 View Details
                      </button>
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* MRN Details Modal */}
      {mrnDetails && (
        <div className="modal-overlay" onClick={() => setMRNDetails(null)}>
          <div className="modal-content" style={{ maxWidth: '1200px', maxHeight: '90vh', overflowY: 'auto' }} onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>🛃 MRN Detailed Usage: {mrnDetails.mrn}</h3>
              <button className="btn btn-secondary" onClick={() => setMRNDetails(null)}>✕</button>
            </div>
            <div className="modal-body">
              {/* Import Summary */}
              <div className="card" style={{ padding: '20px', marginBottom: '20px', background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)', color: 'white' }}>
                <h5 style={{ marginBottom: '15px' }}>📦 Import Summary</h5>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '20px' }}>
                  <div>
                    <div style={{ fontSize: '12px', opacity: 0.9 }}>Original Import Quantity</div>
                    <div style={{ fontSize: '28px', fontWeight: 'bold' }}>{mrnDetails.originalImportQuantity}</div>
                  </div>
                  <div>
                    <div style={{ fontSize: '12px', opacity: 0.9 }}>Original Import Value</div>
                    <div style={{ fontSize: '28px', fontWeight: 'bold' }}>${mrnDetails.originalImportValue.toFixed(2)}</div>
                  </div>
                  <div>
                    <div style={{ fontSize: '12px', opacity: 0.9 }}>Total Duty ({(mrnDetails.dutyRate * 100).toFixed(1)}%)</div>
                    <div style={{ fontSize: '28px', fontWeight: 'bold' }}>${mrnDetails.totalDuty.toFixed(2)}</div>
                  </div>
                </div>
              </div>

              {/* Consumption Summary */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px', marginBottom: '20px' }}>
                <div className="card" style={{ padding: '15px' }}>
                  <h5>📊 Consumption Status</h5>
                  <div style={{ marginTop: '15px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
                      <span>Current Balance:</span>
                      <strong className="text-success">{mrnDetails.totalQuantity.toFixed(2)}</strong>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
                      <span>Consumed:</span>
                      <strong className="text-danger">{mrnDetails.consumed.toFixed(2)}</strong>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
                      <span>Consumption %:</span>
                      <strong>{mrnDetails.consumedPercentage}%</strong>
                    </div>
                    <div style={{ 
                      width: '100%', 
                      height: '30px', 
                      background: '#e9ecef', 
                      borderRadius: '4px',
                      overflow: 'hidden',
                      marginTop: '10px'
                    }}>
                      <div style={{ 
                        width: `${mrnDetails.consumedPercentage}%`, 
                        height: '100%', 
                        background: parseFloat(mrnDetails.consumedPercentage) > 80 ? '#dc3545' : '#ffc107',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        color: 'white',
                        fontWeight: 'bold',
                        fontSize: '12px'
                      }}>
                        {mrnDetails.consumedPercentage}%
                      </div>
                    </div>
                  </div>
                </div>

                <div className="card" style={{ padding: '15px' }}>
                  <h5>💰 Duty Allocation</h5>
                  <div style={{ marginTop: '15px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
                      <span>Total Duty Paid:</span>
                      <strong>${mrnDetails.totalDuty.toFixed(2)}</strong>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
                      <span>Allocated to Production:</span>
                      <strong className="text-danger">${mrnDetails.totalAllocatedDuty.toFixed(2)}</strong>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
                      <span>Remaining Duty:</span>
                      <strong className="text-success">${mrnDetails.remainingDuty.toFixed(2)}</strong>
                    </div>
                    <div style={{ marginTop: '10px', padding: '10px', background: '#fff3cd', borderRadius: '4px' }}>
                      <small>
                        <strong>Note:</strong> Remaining duty will be allocated proportionally as material is consumed
                      </small>
                    </div>
                  </div>
                </div>
              </div>

              {/* Production Orders Consumption */}
              <div className="card" style={{ padding: '15px', marginBottom: '20px' }}>
                <h5>🏭 Production Consumption Details</h5>
                <div className="table-container" style={{ marginTop: '15px' }}>
                  <table>
                    <thead>
                      <tr>
                        <th>Production Order</th>
                        <th>Finished Good</th>
                        <th>Output Batch</th>
                        <th>Output Qty</th>
                        <th>Consumed Qty</th>
                        <th>Consumed Value</th>
                        <th>Proportional Duty</th>
                        <th>Date</th>
                      </tr>
                    </thead>
                    <tbody>
                      {mrnDetails.productionOrders.map((po: any, idx: number) => (
                        <tr key={idx}>
                          <td><strong>{po.orderNumber}</strong></td>
                          <td>{po.item.code} - {po.item.name}</td>
                          <td>
                            <span style={{ 
                              padding: '4px 8px', 
                              background: '#e7f3ff', 
                              borderRadius: '4px',
                              fontWeight: 'bold'
                            }}>
                              {po.outputBatch}
                            </span>
                          </td>
                          <td>{po.outputQuantity}</td>
                          <td><strong style={{ color: '#dc3545' }}>{po.consumedQuantity}</strong></td>
                          <td>${po.consumedValue.toFixed(2)}</td>
                          <td><strong>${po.proportionalDuty.toFixed(2)}</strong></td>
                          <td>{new Date(po.productionDate).toLocaleDateString()}</td>
                        </tr>
                      ))}
                      <tr style={{ background: '#f8f9fa', fontWeight: 'bold' }}>
                        <td colSpan={4}>TOTAL:</td>
                        <td>{mrnDetails.totalConsumed}</td>
                        <td>${mrnDetails.totalConsumedValue.toFixed(2)}</td>
                        <td>${mrnDetails.totalAllocatedDuty.toFixed(2)}</td>
                        <td></td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Current Inventory */}
              <div className="card" style={{ padding: '15px', marginBottom: '20px' }}>
                <h5>📍 Current Inventory by Location</h5>
                <div className="table-container" style={{ marginTop: '15px' }}>
                  <table>
                    <thead>
                      <tr>
                        <th>Location</th>
                        <th>Batch</th>
                        <th>Item</th>
                        <th>Quantity</th>
                        <th>Value</th>
                        <th>Quality Status</th>
                      </tr>
                    </thead>
                    <tbody>
                      {mrnDetails.items.map((inv: any, idx: number) => (
                        <tr key={idx}>
                          <td><strong>{inv.location?.name || 'Unknown'}</strong></td>
                          <td>{inv.batchNumber}</td>
                          <td>{inv.item?.code} - {inv.item?.name}</td>
                          <td>{inv.quantity.toFixed(2)}</td>
                          <td>${(inv.quantity * (inv.item?.unitCost || 0)).toFixed(2)}</td>
                          <td>
                            <span className={`badge badge-${inv.qualityStatus === 1 ? 'success' : inv.qualityStatus === 2 ? 'danger' : 'warning'}`}>
                              {inv.qualityStatus === 1 ? 'OK' : inv.qualityStatus === 2 ? 'Blocked' : 'Quarantine'}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Export Button */}
              <div style={{ textAlign: 'center' }}>
                <button 
                  className="btn btn-primary btn-lg"
                  onClick={exportForCustomsAudit}
                >
                  📊 Export for Customs Audit
                </button>
                <div style={{ marginTop: '10px', fontSize: '12px', color: '#666' }}>
                  Exports complete MRN usage report with duty allocation for customs compliance
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default MRNUsageTracking;
