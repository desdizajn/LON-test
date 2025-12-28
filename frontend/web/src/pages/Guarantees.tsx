import React, { useEffect, useState } from 'react';
import { guaranteeApi } from '../services/api';

const Guarantees: React.FC = () => {
  const [accounts, setAccounts] = useState<any[]>([]);
  const [activeGuarantees, setActiveGuarantees] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [accountsRes, guaranteesRes] = await Promise.all([
        guaranteeApi.getAccounts(),
        guaranteeApi.getActiveGuarantees()
      ]);
      setAccounts(accountsRes.data);
      setActiveGuarantees(guaranteesRes.data);
    } catch (err) {
      console.error('Failed to load guarantees', err);
    } finally {
      setLoading(false);
    }
  };

  if (loading) return <div className="loading">Loading guarantees...</div>;

  return (
    <div>
      <div className="header">
        <h2>Guarantee Management</h2>
      </div>

      <h3 style={{ marginBottom: '15px' }}>Guarantee Accounts</h3>
      <div className="card-grid" style={{ marginBottom: '30px' }}>
        {accounts.map((acc, idx) => (
          <div key={idx} className="card">
            <h3>{acc.accountName}</h3>
            <div style={{ fontSize: '14px', marginTop: '10px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '5px' }}>
                <span>Total Limit:</span>
                <strong>{acc.totalLimit.toFixed(2)} {acc.currency}</strong>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '5px' }}>
                <span>Current Balance:</span>
                <strong style={{ color: '#e74c3c' }}>{acc.currentBalance.toFixed(2)}</strong>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span>Available:</span>
                <strong style={{ color: '#27ae60' }}>{acc.availableLimit.toFixed(2)}</strong>
              </div>
            </div>
          </div>
        ))}
      </div>

      <h3 style={{ marginBottom: '15px' }}>Active Guarantees</h3>
      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>Date</th>
              <th>Amount</th>
              <th>Currency</th>
              <th>MRN</th>
              <th>Description</th>
              <th>Expected Release</th>
            </tr>
          </thead>
          <tbody>
            {activeGuarantees.map((g, idx) => (
              <tr key={idx}>
                <td>{new Date(g.entryDate).toLocaleDateString()}</td>
                <td><strong>{g.amount.toFixed(2)}</strong></td>
                <td>{g.currency}</td>
                <td>{g.mrn || '-'}</td>
                <td>{g.description}</td>
                <td>{g.expectedReleaseDate ? new Date(g.expectedReleaseDate).toLocaleDateString() : '-'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default Guarantees;
