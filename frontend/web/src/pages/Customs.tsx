import React, { useEffect, useState } from 'react';
import { customsApi } from '../services/api';

const Customs: React.FC = () => {
  const [declarations, setDeclarations] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadDeclarations();
  }, []);

  const loadDeclarations = async () => {
    try {
      setLoading(true);
      const response = await customsApi.getDeclarations();
      setDeclarations(response.data);
    } catch (err) {
      console.error('Failed to load declarations', err);
    } finally {
      setLoading(false);
    }
  };

  if (loading) return <div className="loading">Loading customs declarations...</div>;

  return (
    <div>
      <div className="header">
        <h2>Customs & MRN</h2>
        <button className="btn btn-success">+ New Declaration</button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>Declaration Number</th>
              <th>MRN</th>
              <th>Procedure</th>
              <th>Customs Value</th>
              <th>Total Duty</th>
              <th>Total VAT</th>
              <th>Status</th>
              <th>Date</th>
            </tr>
          </thead>
          <tbody>
            {declarations.map((decl, idx) => (
              <tr key={idx}>
                <td><strong>{decl.declarationNumber}</strong></td>
                <td>{decl.mrn}</td>
                <td>{decl.customsProcedure?.name}</td>
                <td>{decl.totalCustomsValue.toFixed(2)} {decl.currency}</td>
                <td>{decl.totalDuty.toFixed(2)}</td>
                <td>{decl.totalVAT.toFixed(2)}</td>
                <td>
                  <span className={`badge badge-${decl.isCleared ? 'success' : 'warning'}`}>
                    {decl.isCleared ? 'Cleared' : 'Pending'}
                  </span>
                </td>
                <td>{new Date(decl.declarationDate).toLocaleDateString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default Customs;
