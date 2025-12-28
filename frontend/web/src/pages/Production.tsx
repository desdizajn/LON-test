import React, { useEffect, useState } from 'react';
import { productionApi } from '../services/api';

const Production: React.FC = () => {
  const [orders, setOrders] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

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
        <button className="btn btn-success">+ New Production Order</button>
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
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default Production;
