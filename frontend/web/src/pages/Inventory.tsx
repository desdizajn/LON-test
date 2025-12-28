import React, { useEffect, useState } from 'react';
import { wmsApi } from '../services/api';

const Inventory: React.FC = () => {
  const [inventory, setInventory] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

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

  if (loading) return <div className="loading">Loading inventory...</div>;

  return (
    <div>
      <div className="header">
        <h2>WMS & Inventory</h2>
        <button className="btn btn-success">+ New Receipt</button>
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
