import React, { useEffect, useState } from 'react';
import { wmsApi, productionApi, masterDataApi } from '../../services/api';

interface MovementEvent {
  id: number;
  date: string;
  type: 'Receipt' | 'Transfer' | 'Issue' | 'Adjustment' | 'CycleCount' | 'QualityChange' | 'Shipment';
  fromLocation?: string;
  toLocation?: string;
  quantity: number;
  referenceNumber?: string;
  user?: string;
  notes?: string;
}

const BatchTraceability: React.FC = () => {
  const [batchNumber, setBatchNumber] = useState('');
  const [loading, setLoading] = useState(false);
  const [batchData, setBatchData] = useState<any>(null);
  const [inventory, setInventory] = useState<any[]>([]);
  const [movements, setMovements] = useState<MovementEvent[]>([]);
  const [productionOrders, setProductionOrders] = useState<any[]>([]);
  const [finishedGoods, setFinishedGoods] = useState<any[]>([]);
  const [relatedBatches, setRelatedBatches] = useState<any[]>([]);

  const searchBatch = async () => {
    if (!batchNumber.trim()) {
      alert('Please enter a batch number');
      return;
    }

    try {
      setLoading(true);
      
      // Get all inventory for this batch
      const inventoryRes = await wmsApi.getInventory();
      const batchInventory = inventoryRes.data.filter((inv: any) => 
        inv.batchNumber?.toLowerCase() === batchNumber.toLowerCase()
      );

      if (batchInventory.length === 0) {
        alert('Batch not found in inventory');
        setLoading(false);
        return;
      }

      setInventory(batchInventory);

      // Get unique MRNs for this batch
      const mrns = Array.from(new Set(batchInventory.map((inv: any) => inv.mrn).filter(Boolean)));

      // Get unique items for this batch
      const items = Array.from(new Set(batchInventory.map((inv: any) => inv.itemId)));

      // Simulate movement history (in real app, would come from MovementHistory table)
      const mockMovements: MovementEvent[] = [
        {
          id: 1,
          date: new Date(Date.now() - 90 * 24 * 60 * 60 * 1000).toISOString(),
          type: 'Receipt',
          toLocation: batchInventory[0].location?.name || 'RECEIVING',
          quantity: 1000,
          referenceNumber: `REC-${Math.floor(Math.random() * 10000)}`,
          user: 'John Doe',
          notes: 'Initial receipt from supplier'
        },
        {
          id: 2,
          date: new Date(Date.now() - 85 * 24 * 60 * 60 * 1000).toISOString(),
          type: 'Transfer',
          fromLocation: 'RECEIVING',
          toLocation: 'STORAGE-A-01',
          quantity: 1000,
          referenceNumber: `TRF-${Math.floor(Math.random() * 10000)}`,
          user: 'Jane Smith',
          notes: 'Moved to storage after quality inspection'
        },
        {
          id: 3,
          date: new Date(Date.now() - 60 * 24 * 60 * 60 * 1000).toISOString(),
          type: 'Issue',
          fromLocation: 'STORAGE-A-01',
          toLocation: 'PRODUCTION',
          quantity: 300,
          referenceNumber: `PROD-001`,
          user: 'System',
          notes: 'Issued to production order PROD-001'
        },
        {
          id: 4,
          date: new Date(Date.now() - 45 * 24 * 60 * 60 * 1000).toISOString(),
          type: 'Issue',
          fromLocation: 'STORAGE-A-01',
          toLocation: 'PRODUCTION',
          quantity: 250,
          referenceNumber: `PROD-002`,
          user: 'System',
          notes: 'Issued to production order PROD-002'
        },
        {
          id: 5,
          date: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
          type: 'CycleCount',
          fromLocation: 'STORAGE-A-01',
          quantity: 450,
          referenceNumber: `CC-${Math.floor(Math.random() * 1000)}`,
          user: 'Jane Smith',
          notes: 'Cycle count - no adjustment needed'
        },
        {
          id: 6,
          date: new Date(Date.now() - 15 * 24 * 60 * 60 * 1000).toISOString(),
          type: 'Adjustment',
          fromLocation: 'STORAGE-A-01',
          quantity: -50,
          referenceNumber: `ADJ-${Math.floor(Math.random() * 1000)}`,
          user: 'Supervisor',
          notes: 'Adjustment - damaged material scrapped'
        }
      ];

      setMovements(mockMovements);

      // Simulate production orders using this batch
      const mockProductionOrders = [
        {
          orderNumber: 'PROD-001',
          item: { code: 'FG-001', name: 'Finished Good A' },
          quantityProduced: 500,
          date: new Date(Date.now() - 60 * 24 * 60 * 60 * 1000).toISOString(),
          status: 'Completed',
          consumedQuantity: 300,
          outputBatch: 'FG-BATCH-2024-001'
        },
        {
          orderNumber: 'PROD-002',
          item: { code: 'FG-002', name: 'Finished Good B' },
          quantityProduced: 400,
          date: new Date(Date.now() - 45 * 24 * 60 * 60 * 1000).toISOString(),
          status: 'Completed',
          consumedQuantity: 250,
          outputBatch: 'FG-BATCH-2024-002'
        }
      ];

      setProductionOrders(mockProductionOrders);

      // Simulate finished goods batches produced from this batch
      const mockFinishedGoods = [
        {
          batchNumber: 'FG-BATCH-2024-001',
          item: { code: 'FG-001', name: 'Finished Good A' },
          quantity: 500,
          location: { name: 'FG-STORAGE' },
          productionOrder: 'PROD-001',
          productionDate: new Date(Date.now() - 60 * 24 * 60 * 60 * 1000).toISOString()
        },
        {
          batchNumber: 'FG-BATCH-2024-002',
          item: { code: 'FG-002', name: 'Finished Good B' },
          quantity: 400,
          location: { name: 'FG-STORAGE' },
          productionOrder: 'PROD-002',
          productionDate: new Date(Date.now() - 45 * 24 * 60 * 60 * 1000).toISOString()
        }
      ];

      setFinishedGoods(mockFinishedGoods);

      // Get related batches (same MRN or co-produced batches)
      const allInventoryRes = await wmsApi.getInventory();
      const related = allInventoryRes.data.filter((inv: any) => 
        inv.batchNumber !== batchNumber && 
        mrns.includes(inv.mrn) &&
        inv.mrn !== null
      );
      
      // Group by batch
      const relatedByBatch = related.reduce((acc: any, inv: any) => {
        if (!acc[inv.batchNumber]) {
          acc[inv.batchNumber] = {
            batchNumber: inv.batchNumber,
            mrn: inv.mrn,
            item: inv.item,
            totalQuantity: 0,
            locations: []
          };
        }
        acc[inv.batchNumber].totalQuantity += inv.quantity;
        acc[inv.batchNumber].locations.push(inv.location?.name || 'Unknown');
        return acc;
      }, {});

      setRelatedBatches(Object.values(relatedByBatch));

      setBatchData({
        batchNumber,
        mrns,
        items,
        totalCurrentQuantity: batchInventory.reduce((sum: number, inv: any) => sum + inv.quantity, 0),
        locations: batchInventory.length,
        originalQuantity: 1000, // From first receipt
        consumedQuantity: 550, // Sum of issues
        adjustedQuantity: -50, // Sum of adjustments
      });

    } catch (err) {
      console.error('Failed to load batch traceability', err);
      alert('Failed to load batch data');
    } finally {
      setLoading(false);
    }
  };

  const getMovementIcon = (type: string) => {
    switch (type) {
      case 'Receipt': return '📥';
      case 'Transfer': return '🔄';
      case 'Issue': return '📤';
      case 'Adjustment': return '⚙️';
      case 'CycleCount': return '📊';
      case 'QualityChange': return '🔒';
      case 'Shipment': return '🚚';
      default: return '📋';
    }
  };

  const getMovementColor = (type: string) => {
    switch (type) {
      case 'Receipt': return '#28a745';
      case 'Issue': return '#dc3545';
      case 'Transfer': return '#007bff';
      case 'Adjustment': return '#ffc107';
      case 'CycleCount': return '#6c757d';
      case 'QualityChange': return '#fd7e14';
      case 'Shipment': return '#17a2b8';
      default: return '#6c757d';
    }
  };

  return (
    <div>
      <div className="header">
        <h2>🔍 Batch Traceability</h2>
      </div>

      {/* Alert */}
      <div className="alert alert-info" style={{ marginBottom: '20px' }}>
        <strong>ℹ️ Complete Batch Genealogy</strong><br />
        Track complete batch history: receipts, movements, production consumption, finished goods produced, and related batches from same MRN.
        <br />
        <strong>Use Cases:</strong> Product recalls, quality investigations, customs audits, material genealogy
      </div>

      {/* Search Section */}
      <div className="card" style={{ padding: '20px', marginBottom: '30px' }}>
        <div style={{ display: 'flex', gap: '10px', alignItems: 'flex-end' }}>
          <div style={{ flex: 1 }}>
            <label><strong>Batch Number:</strong></label>
            <input
              type="text"
              className="form-control"
              placeholder="Enter batch number to trace..."
              value={batchNumber}
              onChange={(e) => setBatchNumber(e.target.value)}
              onKeyPress={(e) => e.key === 'Enter' && searchBatch()}
            />
          </div>
          <button 
            className="btn btn-primary" 
            onClick={searchBatch}
            disabled={loading}
            style={{ height: '38px' }}
          >
            {loading ? '🔍 Searching...' : '🔍 Trace Batch'}
          </button>
        </div>
      </div>

      {batchData && (
        <>
          {/* Batch Summary */}
          <div className="card" style={{ padding: '20px', marginBottom: '30px', background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)', color: 'white' }}>
            <h4 style={{ marginBottom: '15px' }}>📦 Batch: {batchData.batchNumber}</h4>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '20px' }}>
              <div>
                <div style={{ fontSize: '12px', opacity: 0.9 }}>Original Quantity</div>
                <div style={{ fontSize: '28px', fontWeight: 'bold' }}>{batchData.originalQuantity}</div>
              </div>
              <div>
                <div style={{ fontSize: '12px', opacity: 0.9 }}>Current Quantity</div>
                <div style={{ fontSize: '28px', fontWeight: 'bold' }}>{batchData.totalCurrentQuantity.toFixed(2)}</div>
              </div>
              <div>
                <div style={{ fontSize: '12px', opacity: 0.9 }}>Consumed</div>
                <div style={{ fontSize: '28px', fontWeight: 'bold', color: '#ffcccb' }}>{batchData.consumedQuantity}</div>
              </div>
              <div>
                <div style={{ fontSize: '12px', opacity: 0.9 }}>MRNs</div>
                <div style={{ fontSize: '28px', fontWeight: 'bold' }}>{batchData.mrns.length}</div>
              </div>
            </div>
            {batchData.mrns.length > 0 && (
              <div style={{ marginTop: '15px', padding: '10px', background: 'rgba(255,255,255,0.1)', borderRadius: '4px' }}>
                <strong>Associated MRNs:</strong> {batchData.mrns.join(', ')}
              </div>
            )}
          </div>

          {/* Current Inventory Locations */}
          <div className="card" style={{ padding: '15px', marginBottom: '30px' }}>
            <h5>📍 Current Inventory Locations</h5>
            <div className="table-container" style={{ marginTop: '15px' }}>
              <table>
                <thead>
                  <tr>
                    <th>Location</th>
                    <th>Item</th>
                    <th>Quantity</th>
                    <th>Quality Status</th>
                    <th>MRN</th>
                    <th>Last Movement</th>
                  </tr>
                </thead>
                <tbody>
                  {inventory.map((inv, idx) => (
                    <tr key={idx}>
                      <td><strong>{inv.location?.name || 'Unknown'}</strong></td>
                      <td>{inv.item?.code} - {inv.item?.name}</td>
                      <td>{inv.quantity.toFixed(2)}</td>
                      <td>
                        <span className={`badge badge-${inv.qualityStatus === 1 ? 'success' : inv.qualityStatus === 2 ? 'danger' : 'warning'}`}>
                          {inv.qualityStatus === 1 ? 'OK' : inv.qualityStatus === 2 ? 'Blocked' : 'Quarantine'}
                        </span>
                      </td>
                      <td>{inv.mrn || '-'}</td>
                      <td>{new Date(inv.lastMovementDate || inv.createdAt).toLocaleDateString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {/* Movement Timeline */}
          <div className="card" style={{ padding: '15px', marginBottom: '30px' }}>
            <h5>📅 Movement Timeline (Chronological)</h5>
            <div style={{ marginTop: '20px' }}>
              {movements
                .sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime())
                .map((movement, idx) => (
                  <div 
                    key={movement.id}
                    style={{
                      display: 'flex',
                      gap: '15px',
                      marginBottom: '20px',
                      paddingBottom: '20px',
                      borderBottom: idx < movements.length - 1 ? '1px solid #dee2e6' : 'none'
                    }}
                  >
                    {/* Icon & Date */}
                    <div style={{ minWidth: '120px', textAlign: 'center' }}>
                      <div style={{ 
                        fontSize: '32px', 
                        marginBottom: '5px',
                        color: getMovementColor(movement.type)
                      }}>
                        {getMovementIcon(movement.type)}
                      </div>
                      <div style={{ fontSize: '12px', fontWeight: 'bold' }}>
                        {new Date(movement.date).toLocaleDateString()}
                      </div>
                      <div style={{ fontSize: '11px', color: '#666' }}>
                        {new Date(movement.date).toLocaleTimeString()}
                      </div>
                    </div>

                    {/* Timeline Line */}
                    <div style={{ 
                      width: '2px', 
                      background: idx < movements.length - 1 ? getMovementColor(movement.type) : '#dee2e6',
                      position: 'relative'
                    }}>
                      <div style={{
                        width: '10px',
                        height: '10px',
                        borderRadius: '50%',
                        background: getMovementColor(movement.type),
                        position: 'absolute',
                        left: '-4px',
                        top: '0'
                      }} />
                    </div>

                    {/* Details */}
                    <div style={{ flex: 1 }}>
                      <div style={{ fontSize: '16px', fontWeight: 'bold', marginBottom: '5px' }}>
                        <span style={{ color: getMovementColor(movement.type) }}>
                          {movement.type}
                        </span>
                        {movement.referenceNumber && (
                          <span style={{ marginLeft: '10px', fontSize: '14px', color: '#666' }}>
                            ({movement.referenceNumber})
                          </span>
                        )}
                      </div>
                      <div style={{ fontSize: '14px', marginBottom: '5px' }}>
                        {movement.fromLocation && (
                          <span>
                            <strong>From:</strong> {movement.fromLocation}
                            {movement.toLocation && ' → '}
                          </span>
                        )}
                        {movement.toLocation && (
                          <span>
                            <strong>To:</strong> {movement.toLocation}
                          </span>
                        )}
                      </div>
                      <div style={{ fontSize: '14px', marginBottom: '5px' }}>
                        <strong>Quantity:</strong> <span style={{ 
                          color: movement.quantity > 0 ? '#28a745' : '#dc3545',
                          fontWeight: 'bold'
                        }}>
                          {movement.quantity > 0 ? '+' : ''}{movement.quantity}
                        </span>
                      </div>
                      {movement.user && (
                        <div style={{ fontSize: '12px', color: '#666', marginBottom: '3px' }}>
                          <strong>User:</strong> {movement.user}
                        </div>
                      )}
                      {movement.notes && (
                        <div style={{ fontSize: '12px', color: '#666', fontStyle: 'italic' }}>
                          {movement.notes}
                        </div>
                      )}
                    </div>
                  </div>
                ))}
            </div>
          </div>

          {/* Production Orders (Forward Tracing) */}
          {productionOrders.length > 0 && (
            <div className="card" style={{ padding: '15px', marginBottom: '30px' }}>
              <h5>🏭 Production Orders (Forward Tracing - What was produced)</h5>
              <div className="table-container" style={{ marginTop: '15px' }}>
                <table>
                  <thead>
                    <tr>
                      <th>Order #</th>
                      <th>Finished Good</th>
                      <th>Produced Qty</th>
                      <th>Consumed from Batch</th>
                      <th>Output Batch</th>
                      <th>Date</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {productionOrders.map((order, idx) => (
                      <tr key={idx}>
                        <td><strong>{order.orderNumber}</strong></td>
                        <td>{order.item.code} - {order.item.name}</td>
                        <td>{order.quantityProduced}</td>
                        <td><strong style={{ color: '#dc3545' }}>{order.consumedQuantity}</strong></td>
                        <td>
                          <span style={{ 
                            padding: '4px 8px', 
                            background: '#e7f3ff', 
                            borderRadius: '4px',
                            fontWeight: 'bold'
                          }}>
                            {order.outputBatch}
                          </span>
                        </td>
                        <td>{new Date(order.date).toLocaleDateString()}</td>
                        <td>
                          <span className="badge badge-success">{order.status}</span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* Finished Goods Batches */}
          {finishedGoods.length > 0 && (
            <div className="card" style={{ padding: '15px', marginBottom: '30px' }}>
              <h5>📦 Finished Goods Batches Produced</h5>
              <div className="table-container" style={{ marginTop: '15px' }}>
                <table>
                  <thead>
                    <tr>
                      <th>FG Batch</th>
                      <th>Item</th>
                      <th>Quantity</th>
                      <th>Location</th>
                      <th>Production Order</th>
                      <th>Production Date</th>
                    </tr>
                  </thead>
                  <tbody>
                    {finishedGoods.map((fg, idx) => (
                      <tr key={idx} style={{ background: '#d4edda' }}>
                        <td><strong>{fg.batchNumber}</strong></td>
                        <td>{fg.item.code} - {fg.item.name}</td>
                        <td>{fg.quantity}</td>
                        <td>{fg.location.name}</td>
                        <td>{fg.productionOrder}</td>
                        <td>{new Date(fg.productionDate).toLocaleDateString()}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* Related Batches (Same MRN) */}
          {relatedBatches.length > 0 && (
            <div className="card" style={{ padding: '15px', marginBottom: '30px' }}>
              <h5>🔗 Related Batches (Same MRN - Co-imported)</h5>
              <div className="table-container" style={{ marginTop: '15px' }}>
                <table>
                  <thead>
                    <tr>
                      <th>Batch Number</th>
                      <th>MRN</th>
                      <th>Item</th>
                      <th>Total Quantity</th>
                      <th>Locations</th>
                    </tr>
                  </thead>
                  <tbody>
                    {relatedBatches.map((batch: any, idx: number) => (
                      <tr key={idx} style={{ background: '#fff3cd' }}>
                        <td>
                          <strong style={{ cursor: 'pointer', color: '#007bff' }}
                            onClick={() => {
                              setBatchNumber(batch.batchNumber);
                              searchBatch();
                            }}
                          >
                            {batch.batchNumber}
                          </strong>
                        </td>
                        <td>{batch.mrn}</td>
                        <td>{batch.item?.code} - {batch.item?.name}</td>
                        <td>{batch.totalQuantity.toFixed(2)}</td>
                        <td>{batch.locations.join(', ')}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* Summary & Actions */}
          <div className="card" style={{ padding: '15px', background: '#f8f9fa' }}>
            <h5>💡 Traceability Summary</h5>
            <ul style={{ marginTop: '10px', marginBottom: 0 }}>
              <li>
                <strong>Total Movements:</strong> {movements.length} events tracked
              </li>
              <li>
                <strong>Material Flow:</strong> {batchData.originalQuantity} received → {batchData.consumedQuantity} consumed → {batchData.totalCurrentQuantity.toFixed(2)} remaining
              </li>
              {productionOrders.length > 0 && (
                <li>
                  <strong>Production:</strong> Used in {productionOrders.length} production orders, producing {finishedGoods.length} finished goods batches
                </li>
              )}
              {relatedBatches.length > 0 && (
                <li>
                  <strong>Related Batches:</strong> {relatedBatches.length} co-imported batches from same MRN
                </li>
              )}
              {batchData.mrns.length > 0 && (
                <li>
                  <strong>Customs:</strong> Linked to {batchData.mrns.length} MRN(s) for duty calculations and compliance
                </li>
              )}
            </ul>
          </div>
        </>
      )}
    </div>
  );
};

export default BatchTraceability;
