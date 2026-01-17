import React, { useEffect, useState } from 'react';
import { wmsApi, masterDataApi } from '../../services/api';

const MovementReports: React.FC = () => {
  const [receipts, setReceipts] = useState<any[]>([]);
  const [shipments, setShipments] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<'receipts' | 'shipments' | 'transfers'>('receipts');
  const [fromDate, setFromDate] = useState(() => {
    const date = new Date();
    date.setDate(date.getDate() - 30); // Last 30 days
    return date.toISOString().split('T')[0];
  });
  const [toDate, setToDate] = useState(new Date().toISOString().split('T')[0]);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [receiptsRes, shipmentsRes] = await Promise.all([
        wmsApi.getReceipts(),
        wmsApi.getShipments()
      ]);
      setReceipts(receiptsRes.data);
      setShipments(shipmentsRes.data);
    } catch (err) {
      console.error('Failed to load movement data', err);
    } finally {
      setLoading(false);
    }
  };

  // Filter by date range
  const filterByDate = (items: any[], dateField: string) => {
    return items.filter((item: any) => {
      const itemDate = new Date(item[dateField]).toISOString().split('T')[0];
      return itemDate >= fromDate && itemDate <= toDate;
    });
  };

  const filteredReceipts = filterByDate(receipts, 'receiptDate');
  const filteredShipments = filterByDate(shipments, 'shipmentDate');

  // Calculate metrics
  const totalReceipts = filteredReceipts.length;
  const totalReceiptQty = filteredReceipts.reduce((sum, r) => 
    sum + (r.lines?.reduce((lineSum: number, l: any) => lineSum + l.quantity, 0) || 0), 0
  );

  const totalShipments = filteredShipments.length;
  const totalShipmentQty = filteredShipments.reduce((sum, s) => 
    sum + (s.lines?.reduce((lineSum: number, l: any) => lineSum + l.quantity, 0) || 0), 0
  );

  const exportToExcel = () => {
    let headers: string[] = [];
    let rows: any[][] = [];

    if (activeTab === 'receipts') {
      headers = ['Receipt #', 'Date', 'Supplier', 'Warehouse', 'Items', 'Total Lines', 'Reference'];
      rows = filteredReceipts.map((r: any) => [
        r.receiptNumber,
        new Date(r.receiptDate).toLocaleDateString(),
        r.supplier?.name || '-',
        r.warehouse?.name || '-',
        r.lines?.length || 0,
        r.lines?.reduce((sum: number, l: any) => sum + l.quantity, 0).toFixed(2) || 0,
        r.referenceNumber || '-',
      ]);
    } else if (activeTab === 'shipments') {
      headers = ['Shipment #', 'Date', 'Customer', 'Carrier', 'Items', 'Total Lines', 'Tracking #'];
      rows = filteredShipments.map((s: any) => [
        s.shipmentNumber,
        new Date(s.shipmentDate).toLocaleDateString(),
        s.customer?.name || '-',
        s.carrier?.name || '-',
        s.lines?.length || 0,
        s.lines?.reduce((sum: number, l: any) => sum + l.quantity, 0).toFixed(2) || 0,
        s.trackingNumber || '-',
      ]);
    }

    const csv = [headers, ...rows].map(row => row.join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${activeTab}_report_${fromDate}_to_${toDate}.csv`;
    a.click();
  };

  return (
    <div>
      <div className="header">
        <h2>📈 Movement Reports</h2>
        <button className="btn btn-success" onClick={exportToExcel}>
          📥 Export to Excel
        </button>
      </div>

      {/* Date Range Filter */}
      <div className="filters" style={{ 
        background: '#f8f9fa', 
        padding: '15px', 
        borderRadius: '8px', 
        marginBottom: '20px' 
      }}>
        <div className="form-row">
          <div className="form-group col-md-4">
            <label>From Date</label>
            <input
              type="date"
              className="form-control"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
            />
          </div>
          <div className="form-group col-md-4">
            <label>To Date</label>
            <input
              type="date"
              className="form-control"
              value={toDate}
              onChange={(e) => setToDate(e.target.value)}
            />
          </div>
          <div className="form-group col-md-4" style={{ display: 'flex', alignItems: 'flex-end' }}>
            <button className="btn btn-primary" onClick={loadData}>
              Apply Filter
            </button>
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="tabs" style={{ marginBottom: '20px' }}>
        <button
          className={`btn ${activeTab === 'receipts' ? 'btn-primary' : 'btn-outline-primary'}`}
          onClick={() => setActiveTab('receipts')}
          style={{ marginRight: '10px' }}
        >
          📥 Receipts ({totalReceipts})
        </button>
        <button
          className={`btn ${activeTab === 'shipments' ? 'btn-primary' : 'btn-outline-primary'}`}
          onClick={() => setActiveTab('shipments')}
        >
          📤 Shipments ({totalShipments})
        </button>
      </div>

      {loading ? (
        <div className="loading">Loading reports...</div>
      ) : (
        <>
          {/* Receipts Tab */}
          {activeTab === 'receipts' && (
            <>
              <div className="summary-cards" style={{ 
                display: 'grid', 
                gridTemplateColumns: 'repeat(3, 1fr)', 
                gap: '15px', 
                marginBottom: '20px' 
              }}>
                <div className="card" style={{ padding: '15px', background: '#e8f5e9' }}>
                  <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{totalReceipts}</div>
                  <div style={{ fontSize: '12px', color: '#155724' }}>Total Receipts</div>
                </div>
                <div className="card" style={{ padding: '15px', background: '#e3f2fd' }}>
                  <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{totalReceiptQty.toFixed(0)}</div>
                  <div style={{ fontSize: '12px', color: '#0d47a1' }}>Total Quantity Received</div>
                </div>
                <div className="card" style={{ padding: '15px', background: '#fff3e0' }}>
                  <div style={{ fontSize: '24px', fontWeight: 'bold' }}>
                    {totalReceipts > 0 ? (totalReceiptQty / totalReceipts).toFixed(1) : 0}
                  </div>
                  <div style={{ fontSize: '12px', color: '#856404' }}>Avg Qty per Receipt</div>
                </div>
              </div>

              <div className="table-container">
                <table>
                  <thead>
                    <tr>
                      <th>Receipt #</th>
                      <th>Date</th>
                      <th>Supplier</th>
                      <th>Warehouse</th>
                      <th>Lines</th>
                      <th>Total Qty</th>
                      <th>Reference</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredReceipts.length === 0 ? (
                      <tr>
                        <td colSpan={7} style={{ textAlign: 'center', padding: '20px' }}>
                          No receipts found in selected date range.
                        </td>
                      </tr>
                    ) : (
                      filteredReceipts.map((receipt: any) => (
                        <tr key={receipt.id}>
                          <td><strong>{receipt.receiptNumber}</strong></td>
                          <td>{new Date(receipt.receiptDate).toLocaleDateString()}</td>
                          <td>{receipt.supplier?.name || '-'}</td>
                          <td>{receipt.warehouse?.name || '-'}</td>
                          <td>{receipt.lines?.length || 0}</td>
                          <td>
                            <strong>
                              {receipt.lines?.reduce((sum: number, l: any) => sum + l.quantity, 0).toFixed(2) || 0}
                            </strong>
                          </td>
                          <td>{receipt.referenceNumber || '-'}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </>
          )}

          {/* Shipments Tab */}
          {activeTab === 'shipments' && (
            <>
              <div className="summary-cards" style={{ 
                display: 'grid', 
                gridTemplateColumns: 'repeat(3, 1fr)', 
                gap: '15px', 
                marginBottom: '20px' 
              }}>
                <div className="card" style={{ padding: '15px', background: '#e3f2fd' }}>
                  <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{totalShipments}</div>
                  <div style={{ fontSize: '12px', color: '#0d47a1' }}>Total Shipments</div>
                </div>
                <div className="card" style={{ padding: '15px', background: '#fff3e0' }}>
                  <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{totalShipmentQty.toFixed(0)}</div>
                  <div style={{ fontSize: '12px', color: '#856404' }}>Total Quantity Shipped</div>
                </div>
                <div className="card" style={{ padding: '15px', background: '#f3e5f5' }}>
                  <div style={{ fontSize: '24px', fontWeight: 'bold' }}>
                    {totalShipments > 0 ? (totalShipmentQty / totalShipments).toFixed(1) : 0}
                  </div>
                  <div style={{ fontSize: '12px', color: '#4a148c' }}>Avg Qty per Shipment</div>
                </div>
              </div>

              <div className="table-container">
                <table>
                  <thead>
                    <tr>
                      <th>Shipment #</th>
                      <th>Date</th>
                      <th>Customer</th>
                      <th>Carrier</th>
                      <th>Lines</th>
                      <th>Total Qty</th>
                      <th>Tracking #</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredShipments.length === 0 ? (
                      <tr>
                        <td colSpan={8} style={{ textAlign: 'center', padding: '20px' }}>
                          No shipments found in selected date range.
                        </td>
                      </tr>
                    ) : (
                      filteredShipments.map((shipment: any) => (
                        <tr key={shipment.id}>
                          <td><strong>{shipment.shipmentNumber}</strong></td>
                          <td>{new Date(shipment.shipmentDate).toLocaleDateString()}</td>
                          <td>{shipment.customer?.name || '-'}</td>
                          <td>{shipment.carrier?.name || '-'}</td>
                          <td>{shipment.lines?.length || 0}</td>
                          <td>
                            <strong>
                              {shipment.lines?.reduce((sum: number, l: any) => sum + l.quantity, 0).toFixed(2) || 0}
                            </strong>
                          </td>
                          <td>{shipment.trackingNumber || '-'}</td>
                          <td>
                            <span className="badge badge-success">
                              {shipment.status === 1 ? 'Planned' : 
                               shipment.status === 2 ? 'Picked' :
                               shipment.status === 3 ? 'Packed' : 'Shipped'}
                            </span>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </>
      )}
    </div>
  );
};

export default MovementReports;
