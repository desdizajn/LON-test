import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { traceabilityApi } from '../services/api';

const Traceability: React.FC = () => {
  const { t } = useTranslation();
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
        <h2>{t('traceability.title')}</h2>
      </div>

      <div className="card" style={{ marginBottom: '20px' }}>
        <h3 style={{ marginBottom: '15px' }}>{t('traceability.search')}</h3>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 2fr 1fr', gap: '10px' }}>
          <select
            className="form-control"
            value={searchType}
            onChange={(e) => setSearchType(e.target.value as 'batch' | 'mrn')}
          >
            <option value="batch">{t('traceability.batchNumber')}</option>
            <option value="mrn">{t('traceability.mrn')}</option>
          </select>

          <select
            className="form-control"
            value={direction}
            onChange={(e) => setDirection(e.target.value as 'forward' | 'backward')}
          >
            <option value="forward">{t('traceability.forwardTrace')}</option>
            <option value="backward">{t('traceability.backwardTrace')}</option>
          </select>

          <input
            type="text"
            className="form-control"
            placeholder={
              searchType === 'batch'
                ? t('traceability.enterBatch')
                : t('traceability.enterMrn')
            }
            value={searchValue}
            onChange={(e) => setSearchValue(e.target.value)}
          />

          <button
            className="btn btn-primary"
            onClick={handleSearch}
            disabled={loading}
          >
            {loading ? t('traceability.searching') : t('traceability.trace')}
          </button>
        </div>
      </div>

      {results.length > 0 && (
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th>{t('traceability.sourceType')}</th>
                <th>{t('traceability.sourceBatch')}</th>
                <th>{t('traceability.sourceMrn')}</th>
                <th>→</th>
                <th>{t('traceability.targetType')}</th>
                <th>{t('traceability.targetBatch')}</th>
                <th>{t('traceability.targetMrn')}</th>
                <th>{t('traceability.item')}</th>
                <th>{t('traceability.quantity')}</th>
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
          <p>{t('traceability.noResults', { type: searchType, value: searchValue })}</p>
        </div>
      )}
    </div>
  );
};

export default Traceability;
