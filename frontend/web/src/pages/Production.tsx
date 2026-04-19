import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { productionApi } from '../services/api';
import ProductionOrderForm from '../components/Production/ProductionOrderForm';
import MaterialIssueForm from '../components/Production/MaterialIssueForm';
import ProductionReceiptForm from '../components/Production/ProductionReceiptForm';

const Production: React.FC = () => {
  const { t } = useTranslation();
  const [orders, setOrders] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [showOrderForm, setShowOrderForm] = useState(false);
  const [showIssueForm, setShowIssueForm] = useState(false);
  const [showReceiptForm, setShowReceiptForm] = useState(false);
  const [selectedOrderId, setSelectedOrderId] = useState<string | undefined>();
  const [busyOrderId, setBusyOrderId] = useState<string | null>(null);

  useEffect(() => {
    loadOrders();
  }, []);

  const loadOrders = async () => {
    try {
      setLoading(true);
      const response = await productionApi.getOrders();
      setOrders(response.data);
    } catch (err) {
      console.error('Failed to load production orders', err);
    } finally {
      setLoading(false);
    }
  };

  const handleFormSuccess = () => {
    setShowOrderForm(false);
    setShowIssueForm(false);
    setShowReceiptForm(false);
    setSelectedOrderId(undefined);
    loadOrders();
  };

  const handleFormCancel = () => {
    setShowOrderForm(false);
    setShowIssueForm(false);
    setShowReceiptForm(false);
    setSelectedOrderId(undefined);
  };

  const handleIssue = (orderId: string) => {
    setSelectedOrderId(orderId);
    setShowIssueForm(true);
  };

  const handleReceipt = (orderId: string) => {
    setSelectedOrderId(orderId);
    setShowReceiptForm(true);
  };

  // P5.2.6 — one-click Release (Draft → Released; expands BOM + Routing).
  const handleRelease = async (orderId: string) => {
    if (!window.confirm(t('production.releaseConfirm'))) return;
    setBusyOrderId(orderId);
    try {
      await productionApi.releaseOrder(orderId);
      await loadOrders();
    } catch (e: any) {
      alert(e?.response?.data?.errorMessage || t('production.releaseFailed'));
    } finally {
      setBusyOrderId(null);
    }
  };

  // P5.2.1 — one-click bulk issue of all remaining materials (FEFO auto-pick).
  const handleBulkIssue = async (orderId: string) => {
    if (!window.confirm(t('production.bulkIssueConfirm'))) return;
    setBusyOrderId(orderId);
    try {
      await productionApi.issueAllMaterials(orderId, new Date().toISOString());
      await loadOrders();
    } catch (e: any) {
      alert(e?.response?.data?.errorMessage || t('production.bulkIssueFailed'));
    } finally {
      setBusyOrderId(null);
    }
  };

  if (showOrderForm) {
    return <ProductionOrderForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }

  if (showIssueForm) {
    return <MaterialIssueForm productionOrderId={selectedOrderId} onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }

  if (showReceiptForm) {
    return <ProductionReceiptForm productionOrderId={selectedOrderId} onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }

  const getStatusBadge = (status: number) => {
    const statusMap: any = {
      1: { label: 'Draft', class: 'info' },
      2: { label: 'Released', class: 'warning' },
      3: { label: 'In Progress', class: 'warning' },
      4: { label: 'Completed', class: 'success' },
      5: { label: 'Closed', class: 'info' },
      6: { label: 'Cancelled', class: 'danger' },
    };
    const s = statusMap[status] || { label: 'Unknown', class: 'info' };
    return <span className={`badge badge-${s.class}`}>{s.label}</span>;
  };

  if (loading) return <div className="loading">Loading production orders...</div>;

  return (
    <div>
      <div className="header">
        <h2>Production Orders (LON)</h2>
        <button className="btn btn-success" onClick={() => setShowOrderForm(true)}>
          + New Production Order
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>Order Number</th>
              <th>Item</th>
              <th>Order Qty</th>
              <th>Produced Qty</th>
              <th>Scrap Qty</th>
              <th>Status</th>
              <th>Planned Start</th>
              <th>Planned End</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {orders.map((order, idx) => (
              <tr key={idx}>
                <td><strong>{order.orderNumber}</strong></td>
                <td>{order.item?.name}</td>
                <td>{order.orderQuantity.toFixed(2)}</td>
                <td>{order.producedQuantity.toFixed(2)}</td>
                <td>{order.scrapQuantity.toFixed(2)}</td>
                <td>{getStatusBadge(order.status)}</td>
                <td>{new Date(order.plannedStartDate).toLocaleDateString()}</td>
                <td>{new Date(order.plannedEndDate).toLocaleDateString()}</td>
                <td>
                  <div style={{ display: 'flex', gap: '5px', flexWrap: 'wrap' }}>
                    {order.status === 0 && (
                      <button
                        className="btn btn-sm"
                        onClick={() => handleRelease(order.id)}
                        disabled={busyOrderId === order.id}
                        title={t('production.release')}
                      >
                        {t('production.release')}
                      </button>
                    )}
                    {(order.status === 2 || order.status === 3) && (
                      <>
                        <button
                          className="btn btn-sm btn-primary"
                          onClick={() => handleIssue(order.id)}
                          title="Issue Materials"
                        >
                          Issue
                        </button>
                        <button
                          className="btn btn-sm"
                          onClick={() => handleBulkIssue(order.id)}
                          disabled={busyOrderId === order.id}
                          title={t('production.bulkIssue')}
                        >
                          {t('production.bulkIssue')}
                        </button>
                        <button
                          className="btn btn-sm btn-success"
                          onClick={() => handleReceipt(order.id)}
                          title="Receive Finished Goods"
                        >
                          Receive
                        </button>
                      </>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default Production;
