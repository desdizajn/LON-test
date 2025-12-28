import React, { useState } from 'react';
import { traceabilityApi } from '../services/api';

const Traceability: React.FC = () => {
  const [searchType, setSearchType] = useState<'batch' | 'mrn'>('batch');
  const [searchValue, setSearchValue] = useState('');
  const [direction, setDirection] = useState<'forward' | 'backward'>('forward');
  const [results, setResults] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);

  const handleSearch = async () => {
    if (!searchValue.trim()) return;

    try {
      setLoading(true);
      const params = searchType === 'batch' 
        ? { batchNumber: searchValue } 
        : { mrn: searchValue };
      
      const response = direction === 'forward'
        ? await traceabilityApi.traceForward(params.batchNumber, params.mrn)
        : await traceabilityApi.traceBackward(params.batchNumber, params.mrn);
      
      setResults(response.data);
    } catch (err) {
      console.error('Failed to trace', err);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <div className="header">
        <h2>Traceability & Genealogy</h2>
      </div>

      <div className="card" style={{ marginBottom: '20px' }}>
        <h3 style={{ marginBottom: '15px' }}>Search</h3>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 2fr 1fr', gap: '10px' }}>
          <select 
            className="form-control"
            value={searchType}
            onChange={(e) => setSearchType(e.target.value as 'batch' | 'mrn')}
          >
            <option value="batch">Batch Number</option>
            <option value="mrn">MRN</option>
          </select>

          <select 
            className="form-control"
            value={direction}
            onChange={(e) => setDirection(e.target.value as 'forward' | 'backward')}
          >
            <option value="forward">Forward Trace</option>
            <option value="backward">Backward Trace</option>
          </select>

          <input
            type="text"
            className="form-control"
            placeholder={`Enter ${searchType === 'batch' ? 'batch number' : 'MRN'}...`}
            value={searchValue}
            onChange={(e) => setSearchValue(e.target.value)}
          />

          <button 
            className="btn btn-primary" 
            onClick={handleSearch}
            disabled={loading}
          >
            {loading ? 'Searching...' : 'Trace'}
          </button>
        </div>
      </div>

      {results.length > 0 && (
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th>Source Type</th>
                <th>Source Batch</th>
                <th>Source MRN</th>
                <th>→</th>
                <th>Target Type</th>
                <th>Target Batch</th>
                <th>Target MRN</th>
                <th>Item</th>
                <th>Quantity</th>
              </tr>
            </thead>
            <tbody>
              {results.map((link, idx) => (
                <tr key={idx}>
                  <td>{link.sourceType}</td>
                  <td>{link.sourceBatchNumber || '-'}</td>
                  <td>{link.sourceMRN || '-'}</td>
                  <td style={{ textAlign: 'center', fontWeight: 'bold' }}>→</td>
                  <td>{link.targetType}</td>
                  <td>{link.targetBatchNumber || '-'}</td>
                  <td>{link.targetMRN || '-'}</td>
                  <td>{link.item?.name}</td>
                  <td>{link.quantity.toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {results.length === 0 && !loading && searchValue && (
        <div className="card">
          <p>No traceability links found for {searchType}: {searchValue}</p>
        </div>
      )}
    </div>
  );
};

export default Traceability;
