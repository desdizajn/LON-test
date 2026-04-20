import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { wmsApi } from '../../services/api';

const MovementReports: React.FC = () => {
  const { t } = useTranslation();
  const [receipts, setReceipts] = useState<any[]>([]);
  const [shipments, setShipments] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<'receipts' | 'shipments'>('receipts');
  const [fromDate, setFromDate] = useState(() => {
    const date = new Date();
    date.setDate(date.getDate() - 30);
    return date.toISOString().split('T')[0];
  });
  const [toDate, setToDate] = useState(new Date().toISOString().split('T')[0]);

  const loadData = useCallback(async () => {
    try {
      setLoading(true);
      const [receiptsRes, shipmentsRes] = await Promise.all([
        wmsApi.getReceipts(),
        wmsApi.getShipments(),
      ]);
      setReceipts(receiptsRes.data);
      setShipments(shipmentsRes.data);
    } catch (err) {
      console.error('Failed to load movement data', err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadData(); }, [loadData]);

  const filterByDate = (items: any[], dateField: string) =>
    items.filter((item: any) => {
      const itemDate = new Date(item[dateField]).toISOString().split('T')[0];
      return itemDate >= fromDate && itemDate <= toDate;
    });

  const filteredReceipts = filterByDate(receipts, 'receiptDate');
  const filteredShipments = filterByDate(shipments, 'shipmentDate');

  const totalReceipts = filteredReceipts.length;
  const totalReceiptQty = filteredReceipts.reduce((sum, r) =>
    sum + (r.lines?.reduce((ls: number, l: any) => ls + l.quantity, 0) || 0), 0);
  const totalShipments = filteredShipments.length;
  const totalShipmentQty = filteredShipments.reduce((sum, s) =>
    sum + (s.lines?.reduce((ls: number, l: any) => ls + l.quantity, 0) || 0), 0);

  const shipmentStatusLabel = (status: number) => {
    const map: Record<number, string> = {
      1: t('shipmentStatus.draft'),
      2: t('shipmentStatus.planned'),
      3: t('shipmentStatus.picked'),
      4: t('shipmentStatus.packed'),
      5: t('shipmentStatus.shipped'),
      6: t('shipmentStatus.delivered'),
      7: t('shipmentStatus.cancelled'),
    };
    return map[status] || '—';
  };

  const exportToCsv = () => {
    let headers: string[] = [];
    let rows: any[][] = [];

    if (activeTab === 'receipts') {
      headers = [
        t('movements.receiptNumber'), t('reports.common.period').replace(/Период.*/, t('reports.common.from')),
        t('movements.supplier'), t('reports.common.warehouse'),
        t('movements.lines'), t('reports.common.totalQty'), t('movements.reference'),
      ];
      rows = filteredReceipts.map((r: any) => [
        r.receiptNumber,
        new Date(r.receiptDate).toLocaleDateString(),
        r.supplier?.name || '-',
        r.warehouse?.name || '-',
        r.lines?.length || 0,
        r.lines?.reduce((s: number, l: any) => s + l.quantity, 0).toFixed(2) || 0,
        r.referenceNumber || '-',
      ]);
    } else {
      headers = [
        t('movements.shipmentNumber'), t('reports.common.from'),
        t('movements.customer'), t('movements.carrier'),
        t('movements.lines'), t('reports.common.totalQty'), t('movements.tracking'), t('movements.statusHeader'),
      ];
      rows = filteredShipments.map((s: any) => [
        s.shipmentNumber,
        new Date(s.shipmentDate).toLocaleDateString(),
        s.customer?.name || '-',
        s.carrier?.name || '-',
        s.lines?.length || 0,
        s.lines?.reduce((ss: number, l: any) => ss + l.quantity, 0).toFixed(2) || 0,
        s.trackingNumber || '-',
        shipmentStatusLabel(s.status),
      ]);
    }
    const csv = '\uFEFF' + [headers, ...rows].map((row) => row.join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${activeTab}_${fromDate}_${toDate}.csv`;
    a.click();
  };

  return (
    <div>
      <div className="header">
        <h2>📈 {t('reports.movements.title')}</h2>
        <button onClick={exportToCsv} style={{ background: 'var(--success)', color: 'white', borderColor: 'var(--success)' }}>
          📥 {t('reports.common.exportCsv')}
        </button>
      </div>

      <div className="card" style={{ marginBottom: 16 }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12, alignItems: 'end' }}>
          <div>
            <label>{t('reports.common.from')}</label>
            <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
          </div>
          <div>
            <label>{t('reports.common.to')}</label>
            <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
          </div>
          <div>
            <button className="btn-primary" onClick={loadData}>{t('reports.common.apply')}</button>
          </div>
        </div>
      </div>

      <div style={{ marginBottom: 16, display: 'flex', gap: 8 }}>
        <button
          onClick={() => setActiveTab('receipts')}
          className={activeTab === 'receipts' ? 'btn-primary' : ''}
        >
          📥 {t('movements.tabs.receipts')} ({totalReceipts})
        </button>
        <button
          onClick={() => setActiveTab('shipments')}
          className={activeTab === 'shipments' ? 'btn-primary' : ''}
        >
          📤 {t('movements.tabs.shipments')} ({totalShipments})
        </button>
      </div>

      {loading ? (
        <div className="loading">{t('reports.common.loading')}</div>
      ) : activeTab === 'receipts' ? (
        <>
          <div className="card-grid">
            <div className="card success">
              <h3>{t('movements.totalReceipts')}</h3>
              <div className="value">{totalReceipts}</div>
            </div>
            <div className="card info">
              <h3>{t('movements.totalQtyReceived')}</h3>
              <div className="value">{totalReceiptQty.toFixed(0)}</div>
            </div>
            <div className="card warning">
              <h3>{t('movements.avgQtyPerReceipt')}</h3>
              <div className="value">{totalReceipts > 0 ? (totalReceiptQty / totalReceipts).toFixed(1) : 0}</div>
            </div>
          </div>

          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>{t('movements.receiptNumber')}</th>
                  <th>{t('reports.common.period').replace(/Период.*/, t('common.date'))}</th>
                  <th>{t('movements.supplier')}</th>
                  <th>{t('reports.common.warehouse')}</th>
                  <th>{t('movements.lines')}</th>
                  <th>{t('reports.common.totalQty')}</th>
                  <th>{t('movements.reference')}</th>
                </tr>
              </thead>
              <tbody>
                {filteredReceipts.length === 0 ? (
                  <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20 }}>{t('movements.noReceipts')}</td></tr>
                ) : (
                  filteredReceipts.map((r: any) => (
                    <tr key={r.id}>
                      <td><strong>{r.receiptNumber}</strong></td>
                      <td>{new Date(r.receiptDate).toLocaleDateString()}</td>
                      <td>{r.supplier?.name || '-'}</td>
                      <td>{r.warehouse?.name || '-'}</td>
                      <td>{r.lines?.length || 0}</td>
                      <td><strong>{r.lines?.reduce((s: number, l: any) => s + l.quantity, 0).toFixed(2) || 0}</strong></td>
                      <td>{r.referenceNumber || '-'}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </>
      ) : (
        <>
          <div className="card-grid">
            <div className="card info">
              <h3>{t('movements.totalShipments')}</h3>
              <div className="value">{totalShipments}</div>
            </div>
            <div className="card warning">
              <h3>{t('movements.totalQtyShipped')}</h3>
              <div className="value">{totalShipmentQty.toFixed(0)}</div>
            </div>
            <div className="card">
              <h3>{t('movements.avgQtyPerShipment')}</h3>
              <div className="value">{totalShipments > 0 ? (totalShipmentQty / totalShipments).toFixed(1) : 0}</div>
            </div>
          </div>

          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>{t('movements.shipmentNumber')}</th>
                  <th>{t('common.date')}</th>
                  <th>{t('movements.customer')}</th>
                  <th>{t('movements.carrier')}</th>
                  <th>{t('movements.lines')}</th>
                  <th>{t('reports.common.totalQty')}</th>
                  <th>{t('movements.tracking')}</th>
                  <th>{t('movements.statusHeader')}</th>
                </tr>
              </thead>
              <tbody>
                {filteredShipments.length === 0 ? (
                  <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20 }}>{t('movements.noShipments')}</td></tr>
                ) : (
                  filteredShipments.map((s: any) => (
                    <tr key={s.id}>
                      <td><strong>{s.shipmentNumber}</strong></td>
                      <td>{new Date(s.shipmentDate).toLocaleDateString()}</td>
                      <td>{s.customer?.name || '-'}</td>
                      <td>{s.carrier?.name || '-'}</td>
                      <td>{s.lines?.length || 0}</td>
                      <td><strong>{s.lines?.reduce((ss: number, l: any) => ss + l.quantity, 0).toFixed(2) || 0}</strong></td>
                      <td>{s.trackingNumber || '-'}</td>
                      <td><span className="badge badge-info">{shipmentStatusLabel(s.status)}</span></td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
};

export default MovementReports;
