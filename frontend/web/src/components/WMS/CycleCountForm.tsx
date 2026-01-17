import React, { useState, useEffect } from 'react';
import { wmsApi, masterDataApi } from '../../services/api';

interface CycleCountFormProps {
  onSuccess: () => void;
  onCancel: () => void;
}

interface CycleCountLine {
  itemId: string;
  item?: any;
  batchNumber?: string;
  mrn?: string;
  systemQuantity: number;
  countedQuantity: number;
  variance: number;
  uoMId: string;
  uoM?: any;
}

interface CycleCountFormData {
  countDate: string;
  locationId: string;
  countedByEmployeeId: string;
  notes?: string;
  lines: CycleCountLine[];
}

const CycleCountForm: React.FC<CycleCountFormProps> = ({ onSuccess, onCancel }) => {
  const [formData, setFormData] = useState<CycleCountFormData>({
    countDate: new Date().toISOString().split('T')[0],
    locationId: '',
    countedByEmployeeId: '',
    notes: '',
    lines: [],
  });

  const [locations, setLocations] = useState<any[]>([]);
  const [employees, setEmployees] = useState<any[]>([]);
  const [inventoryBalances, setInventoryBalances] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadingInventory, setLoadingInventory] = useState(false);
  const [error, setError] = useState('');
  const [step, setStep] = useState<'setup' | 'count' | 'review'>('setup');

  useEffect(() => {
    loadMasterData();
  }, []);

  const loadMasterData = async () => {
    try {
      const [locationsRes, employeesRes] = await Promise.all([
        masterDataApi.getLocations(),
        masterDataApi.getEmployees()
      ]);
      setLocations(locationsRes.data);
      setEmployees(employeesRes.data);
    } catch (err: any) {
      setError('Failed to load master data');
      console.error(err);
    }
  };

  const loadInventoryForLocation = async (locationId: string) => {
    if (!locationId) return;
    
    try {
      setLoadingInventory(true);
      const response = await wmsApi.getInventory(undefined, locationId);
      setInventoryBalances(response.data);
      
      // Initialize count lines from system inventory
      const lines: CycleCountLine[] = response.data.map((inv: any) => ({
        itemId: inv.itemId,
        item: inv.item,
        batchNumber: inv.batchNumber || '',
        mrn: inv.mrn || '',
        systemQuantity: inv.quantity,
        countedQuantity: 0, // User will enter this
        variance: -inv.quantity, // Will recalculate when user enters counted qty
        uoMId: inv.uoMId,
        uoM: inv.uoM,
      }));
      
      setFormData(prev => ({ ...prev, lines }));
      setStep('count');
    } catch (err: any) {
      setError('Failed to load inventory for location');
      console.error(err);
    } finally {
      setLoadingInventory(false);
    }
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
  };

  const handleLocationChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const locationId = e.target.value;
    setFormData(prev => ({ ...prev, locationId }));
  };

  const handleStartCount = () => {
    // Validation
    if (!formData.locationId) {
      setError('Location is required');
      return;
    }
    if (!formData.countedByEmployeeId) {
      setError('Counter employee is required');
      return;
    }
    
    loadInventoryForLocation(formData.locationId);
  };

  const handleCountedQuantityChange = (index: number, value: string) => {
    const countedQty = parseFloat(value) || 0;
    const updatedLines = [...formData.lines];
    updatedLines[index].countedQuantity = countedQty;
    updatedLines[index].variance = countedQty - updatedLines[index].systemQuantity;
    setFormData(prev => ({ ...prev, lines: updatedLines }));
  };

  const handleReview = () => {
    setStep('review');
  };

  const handleBackToCount = () => {
    setStep('count');
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    
    // Validation
    if (formData.lines.length === 0) {
      setError('No items to count. Please go back and load inventory.');
      return;
    }

    const allCounted = formData.lines.every(line => line.countedQuantity >= 0);
    if (!allCounted) {
      setError('All items must have a counted quantity (use 0 if not found)');
      return;
    }

    try {
      setLoading(true);
      await wmsApi.createCycleCount(formData);
      onSuccess();
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to create cycle count');
    } finally {
      setLoading(false);
    }
  };

  const renderSetupStep = () => (
    <>
      <div className="form-group">
        <label>Count Date *</label>
        <input
          type="date"
          name="countDate"
          value={formData.countDate}
          onChange={handleInputChange}
          className="form-control"
          required
        />
      </div>

      <div className="form-group">
        <label>Location to Count *</label>
        <select
          name="locationId"
          value={formData.locationId}
          onChange={handleLocationChange}
          className="form-control"
          required
        >
          <option value="">-- Select Location --</option>
          {locations.map(loc => (
            <option key={loc.id} value={loc.id}>
              {loc.warehouse?.name} - {loc.name}
            </option>
          ))}
        </select>
        <small className="form-text text-muted">
          All inventory in this location will be counted
        </small>
      </div>

      <div className="form-group">
        <label>Counted By (Employee) *</label>
        <select
          name="countedByEmployeeId"
          value={formData.countedByEmployeeId}
          onChange={handleInputChange}
          className="form-control"
          required
        >
          <option value="">-- Select Employee --</option>
          {employees.map(emp => (
            <option key={emp.id} value={emp.id}>
              {emp.firstName} {emp.lastName}
            </option>
          ))}
        </select>
      </div>

      <div className="form-group">
        <label>Notes</label>
        <textarea
          name="notes"
          value={formData.notes}
          onChange={handleInputChange}
          className="form-control"
          rows={3}
          placeholder="Additional notes about this count..."
        />
      </div>

      <div className="form-actions">
        <button
          type="button"
          className="btn btn-primary"
          onClick={handleStartCount}
          disabled={loadingInventory}
        >
          {loadingInventory ? 'Loading Inventory...' : 'Start Counting →'}
        </button>
        <button
          type="button"
          className="btn btn-secondary"
          onClick={onCancel}
        >
          Cancel
        </button>
      </div>
    </>
  );

  const renderCountStep = () => (
    <>
      <div className="alert alert-info">
        <strong>Location:</strong> {locations.find(l => l.id === formData.locationId)?.name}<br />
        <strong>Items to Count:</strong> {formData.lines.length}
      </div>

      <div style={{ marginBottom: '20px' }}>
        <strong>Instructions:</strong> Enter the actual counted quantity for each item below. 
        Use 0 if item is not found in the location.
      </div>

      <div className="table-container" style={{ maxHeight: '400px', overflowY: 'auto' }}>
        <table>
          <thead style={{ position: 'sticky', top: 0, background: '#f8f9fa' }}>
            <tr>
              <th style={{ width: '25%' }}>Item</th>
              <th style={{ width: '15%' }}>Batch</th>
              <th style={{ width: '15%' }}>MRN</th>
              <th style={{ width: '15%' }}>System Qty</th>
              <th style={{ width: '15%' }}>Counted Qty</th>
              <th style={{ width: '15%' }}>Variance</th>
            </tr>
          </thead>
          <tbody>
            {formData.lines.map((line, index) => (
              <tr key={index}>
                <td>
                  <strong>{line.item?.code}</strong><br />
                  <small>{line.item?.name}</small>
                </td>
                <td>{line.batchNumber || '-'}</td>
                <td>{line.mrn || '-'}</td>
                <td>
                  {line.systemQuantity.toFixed(2)} {line.uoM?.code}
                </td>
                <td>
                  <input
                    type="number"
                    className="form-control form-control-sm"
                    value={line.countedQuantity}
                    onChange={(e) => handleCountedQuantityChange(index, e.target.value)}
                    step="0.01"
                    min="0"
                    style={{ width: '120px' }}
                  />
                </td>
                <td>
                  <span className={
                    line.variance === 0 ? 'text-success' : 
                    line.variance > 0 ? 'text-warning' : 'text-danger'
                  }>
                    {line.variance > 0 && '+'}{line.variance.toFixed(2)} {line.uoM?.code}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="form-actions" style={{ marginTop: '20px' }}>
        <button
          type="button"
          className="btn btn-primary"
          onClick={handleReview}
        >
          Review & Submit →
        </button>
        <button
          type="button"
          className="btn btn-secondary"
          onClick={() => setStep('setup')}
        >
          ← Back
        </button>
      </div>
    </>
  );

  const renderReviewStep = () => {
    const totalVariances = formData.lines.filter(l => l.variance !== 0).length;
    const positiveVariances = formData.lines.filter(l => l.variance > 0).length;
    const negativeVariances = formData.lines.filter(l => l.variance < 0).length;
    const accurate = formData.lines.filter(l => l.variance === 0).length;
    const accuracyPercentage = ((accurate / formData.lines.length) * 100).toFixed(1);

    return (
      <>
        <div className="alert alert-warning">
          <h5>⚠️ Review Cycle Count</h5>
          <p>Please review the variances below before submitting. Inventory adjustments will be created for all variances.</p>
        </div>

        <div className="summary-cards" style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '15px', marginBottom: '20px' }}>
          <div className="card" style={{ padding: '15px', background: '#f8f9fa' }}>
            <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{formData.lines.length}</div>
            <div style={{ fontSize: '12px', color: '#666' }}>Total Items</div>
          </div>
          <div className="card" style={{ padding: '15px', background: '#d4edda' }}>
            <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{accurate}</div>
            <div style={{ fontSize: '12px', color: '#155724' }}>Accurate ({accuracyPercentage}%)</div>
          </div>
          <div className="card" style={{ padding: '15px', background: '#fff3cd' }}>
            <div style={{ fontSize: '24px', fontWeight: 'bold' }}>+{positiveVariances}</div>
            <div style={{ fontSize: '12px', color: '#856404' }}>Overage</div>
          </div>
          <div className="card" style={{ padding: '15px', background: '#f8d7da' }}>
            <div style={{ fontSize: '24px', fontWeight: 'bold' }}>-{negativeVariances}</div>
            <div style={{ fontSize: '12px', color: '#721c24' }}>Shortage</div>
          </div>
        </div>

        {totalVariances > 0 && (
          <div className="variances-table">
            <h5>Variances ({totalVariances} items)</h5>
            <div className="table-container" style={{ maxHeight: '300px', overflowY: 'auto' }}>
              <table>
                <thead>
                  <tr>
                    <th>Item</th>
                    <th>Batch</th>
                    <th>MRN</th>
                    <th>System</th>
                    <th>Counted</th>
                    <th>Variance</th>
                  </tr>
                </thead>
                <tbody>
                  {formData.lines.filter(l => l.variance !== 0).map((line, index) => (
                    <tr key={index}>
                      <td>
                        <strong>{line.item?.code}</strong><br />
                        <small>{line.item?.name}</small>
                      </td>
                      <td>{line.batchNumber || '-'}</td>
                      <td>{line.mrn || '-'}</td>
                      <td>{line.systemQuantity.toFixed(2)}</td>
                      <td>{line.countedQuantity.toFixed(2)}</td>
                      <td>
                        <span className={line.variance > 0 ? 'text-warning' : 'text-danger'}>
                          <strong>{line.variance > 0 && '+'}{line.variance.toFixed(2)} {line.uoM?.code}</strong>
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {totalVariances === 0 && (
          <div className="alert alert-success">
            ✅ Perfect count! All quantities match the system.
          </div>
        )}

        <div className="form-actions" style={{ marginTop: '20px' }}>
          <button
            type="submit"
            className="btn btn-success"
            disabled={loading}
          >
            {loading ? 'Submitting...' : '✅ Submit Cycle Count'}
          </button>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={handleBackToCount}
            disabled={loading}
          >
            ← Back to Count
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
      </>
    );
  };

  return (
    <div className="form-container">
      <div className="form-header">
        <h3>📊 Cycle Count</h3>
        <div className="steps-indicator" style={{ fontSize: '14px', color: '#666' }}>
          Step {step === 'setup' ? '1' : step === 'count' ? '2' : '3'} of 3: {
            step === 'setup' ? 'Setup' : 
            step === 'count' ? 'Count Inventory' : 
            'Review & Submit'
          }
        </div>
      </div>

      {error && (
        <div className="alert alert-danger">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit}>
        {step === 'setup' && renderSetupStep()}
        {step === 'count' && renderCountStep()}
        {step === 'review' && renderReviewStep()}
      </form>
    </div>
  );
};

export default CycleCountForm;
