import React, { useState, useEffect } from 'react';
import { wmsApi, masterDataApi } from '../../services/api';
import { QualityStatus } from '../../types/wms';

interface QualityStatusChangeFormProps {
  onSuccess: () => void;
  onCancel: () => void;
}

interface QualityStatusChangeFormData {
  inventoryBalanceId: string;
  newQualityStatus: QualityStatus;
  reason: string;
  qualityInspector?: string;
  testReferenceNumber?: string;
  notes?: string;
}

const QualityStatusChangeForm: React.FC<QualityStatusChangeFormProps> = ({ onSuccess, onCancel }) => {
  const [formData, setFormData] = useState<QualityStatusChangeFormData>({
    inventoryBalanceId: '',
    newQualityStatus: QualityStatus.OK,
    reason: '',
    qualityInspector: '',
    testReferenceNumber: '',
    notes: '',
  });

  const [items, setItems] = useState<any[]>([]);
  const [inventoryBalances, setInventoryBalances] = useState<any[]>([]);
  const [selectedInventory, setSelectedInventory] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    loadMasterData();
  }, []);

  const loadMasterData = async () => {
    try {
      const itemsRes = await masterDataApi.getItems();
      setItems(itemsRes.data);
    } catch (err: any) {
      setError('Failed to load items');
      console.error(err);
    }
  };

  const loadInventoryForItem = async (itemId: string) => {
    try {
      const response = await wmsApi.getInventory(itemId);
      setInventoryBalances(response.data);
    } catch (err: any) {
      console.error('Failed to load inventory:', err);
    }
  };

  const handleItemSelect = (itemId: string) => {
    if (itemId) {
      loadInventoryForItem(itemId);
    }
  };

  const handleInventorySelect = (inventory: any) => {
    setSelectedInventory(inventory);
    setFormData(prev => ({
      ...prev,
      inventoryBalanceId: inventory.id,
      // Set opposite status by default
      newQualityStatus: inventory.qualityStatus === QualityStatus.OK ? QualityStatus.Blocked : QualityStatus.OK
    }));
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    const { name, value } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: name === 'newQualityStatus' ? parseInt(value) : value
    }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    
    // Validation
    if (!formData.inventoryBalanceId) {
      setError('Please select inventory to change status');
      return;
    }
    if (!formData.reason) {
      setError('Reason is required for quality status change');
      return;
    }
    if (formData.newQualityStatus === selectedInventory?.qualityStatus) {
      setError('New status must be different from current status');
      return;
    }

    try {
      setLoading(true);
      await wmsApi.updateQualityStatus(formData);
      onSuccess();
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to update quality status');
    } finally {
      setLoading(false);
    }
  };

  const getQualityStatusLabel = (status: QualityStatus) => {
    switch (status) {
      case QualityStatus.OK: return 'OK (Available)';
      case QualityStatus.Blocked: return 'Blocked (Cannot Issue)';
      case QualityStatus.Quarantine: return 'Quarantine (Under Review)';
      default: return 'Unknown';
    }
  };

  const getQualityStatusBadge = (status: QualityStatus) => {
    const badges = {
      [QualityStatus.OK]: 'badge-success',
      [QualityStatus.Blocked]: 'badge-danger',
      [QualityStatus.Quarantine]: 'badge-warning',
    };
    return <span className={`badge ${badges[status]}`}>{getQualityStatusLabel(status)}</span>;
  };

  return (
    <div className="form-container">
      <div className="form-header">
        <h3>🔒 Quality Status Change</h3>
      </div>

      {error && (
        <div className="alert alert-danger">
          {error}
        </div>
      )}

      <div className="alert alert-info">
        <strong>ℹ️ Info:</strong> Quality status controls whether inventory can be issued:<br />
        <ul>
          <li><strong>OK:</strong> Available for all operations</li>
          <li><strong>Blocked:</strong> Cannot be issued (damaged, quality issue)</li>
          <li><strong>Quarantine:</strong> Awaiting quality inspection</li>
        </ul>
      </div>

      <form onSubmit={handleSubmit}>
        <div className="form-group">
          <label>Search by Item</label>
          <select
            className="form-control"
            onChange={(e) => handleItemSelect(e.target.value)}
          >
            <option value="">-- Select Item to See Inventory --</option>
            {items.map(item => (
              <option key={item.id} value={item.id}>
                {item.code} - {item.name}
              </option>
            ))}
          </select>
        </div>

        {inventoryBalances.length > 0 && (
          <div className="form-group">
            <label>Select Inventory to Change Status</label>
            <div style={{ maxHeight: '200px', overflowY: 'auto', border: '1px solid #ddd', borderRadius: '4px' }}>
              <table className="table table-sm table-hover">
                <thead style={{ position: 'sticky', top: 0, background: '#f8f9fa' }}>
                  <tr>
                    <th>Location</th>
                    <th>Batch</th>
                    <th>MRN</th>
                    <th>Qty</th>
                    <th>Status</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {inventoryBalances.map((inv, idx) => (
                    <tr key={idx} className={inv.id === formData.inventoryBalanceId ? 'table-active' : ''}>
                      <td>{inv.location?.name}</td>
                      <td>{inv.batchNumber || '-'}</td>
                      <td>{inv.mrn || '-'}</td>
                      <td>{inv.quantity.toFixed(2)} {inv.uoM?.code}</td>
                      <td>{getQualityStatusBadge(inv.qualityStatus)}</td>
                      <td>
                        <button
                          type="button"
                          className="btn btn-xs btn-primary"
                          onClick={() => handleInventorySelect(inv)}
                        >
                          {inv.id === formData.inventoryBalanceId ? '✓ Selected' : 'Select'}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {selectedInventory && (
          <>
            <div className="alert alert-warning">
              <h5>Selected Inventory</h5>
              <strong>Item:</strong> {selectedInventory.item?.code} - {selectedInventory.item?.name}<br />
              <strong>Location:</strong> {selectedInventory.location?.name}<br />
              <strong>Batch:</strong> {selectedInventory.batchNumber || 'N/A'}<br />
              <strong>MRN:</strong> {selectedInventory.mrn || 'N/A'}<br />
              <strong>Quantity:</strong> {selectedInventory.quantity.toFixed(2)} {selectedInventory.uoM?.code}<br />
              <strong>Current Status:</strong> {getQualityStatusBadge(selectedInventory.qualityStatus)}
            </div>

            <div className="form-group">
              <label>New Quality Status *</label>
              <select
                name="newQualityStatus"
                value={formData.newQualityStatus}
                onChange={handleInputChange}
                className="form-control"
                required
              >
                <option value={QualityStatus.OK}>OK (Available)</option>
                <option value={QualityStatus.Blocked}>Blocked (Cannot Issue)</option>
                <option value={QualityStatus.Quarantine}>Quarantine (Under Review)</option>
              </select>
            </div>

            <div className="form-group">
              <label>Reason for Change *</label>
              <textarea
                name="reason"
                value={formData.reason}
                onChange={handleInputChange}
                className="form-control"
                rows={3}
                required
                placeholder="Explain why the quality status is being changed..."
              />
            </div>

            <div className="form-row">
              <div className="form-group col-md-6">
                <label>Quality Inspector</label>
                <input
                  type="text"
                  name="qualityInspector"
                  value={formData.qualityInspector}
                  onChange={handleInputChange}
                  className="form-control"
                  placeholder="Name of inspector"
                />
              </div>

              <div className="form-group col-md-6">
                <label>Test/Report Reference #</label>
                <input
                  type="text"
                  name="testReferenceNumber"
                  value={formData.testReferenceNumber}
                  onChange={handleInputChange}
                  className="form-control"
                  placeholder="e.g. QA-2026-001"
                />
              </div>
            </div>

            <div className="form-group">
              <label>Additional Notes</label>
              <textarea
                name="notes"
                value={formData.notes}
                onChange={handleInputChange}
                className="form-control"
                rows={2}
                placeholder="Optional additional information..."
              />
            </div>
          </>
        )}

        <div className="form-actions">
          <button
            type="submit"
            className="btn btn-primary"
            disabled={loading || !selectedInventory}
          >
            {loading ? 'Processing...' : 'Update Quality Status'}
          </button>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={onCancel}
            disabled={loading}
          >
            Cancel
          </button>
        </div>
      </form>
    </div>
  );
};

export default QualityStatusChangeForm;
