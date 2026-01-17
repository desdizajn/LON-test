import React, { useEffect, useState } from 'react';
import { wmsApi } from '../services/api';
import ReceiptForm from '../components/WMS/ReceiptForm';
import TransferForm from '../components/WMS/TransferForm';
import ShipmentForm from '../components/WMS/ShipmentForm';
import CycleCountForm from '../components/WMS/CycleCountForm';
import AdjustmentForm from '../components/WMS/AdjustmentForm';
import QualityStatusChangeForm from '../components/WMS/QualityStatusChangeForm';

const Inventory: React.FC = () => {
  const [inventory, setInventory] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeForm, setActiveForm] = useState<string | null>(null);

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

  const handleFormSuccess = () => {
    setActiveForm(null);
    loadInventory();
  };

  const handleFormCancel = () => {
    setActiveForm(null);
  };

  // Render active form
  if (activeForm === 'receipt') {
    return <ReceiptForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }
  if (activeForm === 'transfer') {
    return <TransferForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }
  if (activeForm === 'shipment') {
    return <ShipmentForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }
  if (activeForm === 'cyclecount') {
    return <CycleCountForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }
  if (activeForm === 'adjustment') {
    return <AdjustmentForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }
  if (activeForm === 'qualitychange') {
    return <QualityStatusChangeForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }

  if (loading) return <div className="loading">Loading inventory...</div>;

  return (
    <div>
      <div className="header">
        <h2>📦 WMS & Inventory</h2>
        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
          <button className="btn btn-success" onClick={() => setActiveForm('receipt')}>
            ➕ Receipt
          </button>
          <button className="btn btn-primary" onClick={() => setActiveForm('transfer')}>
            🔄 Transfer
          </button>
          <button className="btn btn-info" onClick={() => setActiveForm('shipment')}>
            📤 Shipment
          </button>
          <button className="btn btn-warning" onClick={() => setActiveForm('cyclecount')}>
            📊 Cycle Count
          </button>
          <button className="btn btn-secondary" onClick={() => setActiveForm('adjustment')}>
            ⚙️ Adjustment
          </button>
          <button className="btn btn-danger" onClick={() => setActiveForm('qualitychange')}>
            🔒 Quality Status
          </button>
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
              <th>MRN</th>
              <th>Quantity</th>
              <th>Quality Status</th>
            </tr>
          </thead>
          <tbody>
            {inventory.map((inv, idx) => (
              <tr key={idx}>
                <td>{inv.item?.code}</td>
                <td>{inv.item?.name}</td>
                <td>{inv.location?.name}</td>
                <td>{inv.batchNumber || '-'}</td>
                <td>{inv.mrn || '-'}</td>
                <td>{inv.quantity.toFixed(2)} {inv.uoM?.code}</td>
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
  );
};

export default Inventory;
