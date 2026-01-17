import React, { useState, useEffect } from 'react';
import { wmsApi, masterDataApi } from '../../services/api';

interface AdjustmentFormProps {
  onSuccess: () => void;
  onCancel: () => void;
}

interface AdjustmentFormData {
  itemId: string;
  locationId: string;
  batchNumber?: string;
  mrn?: string;
  uoMId: string;
  adjustmentType: 'Increase' | 'Decrease' | 'Set';
  quantityChange: number;
  newQuantity: number;
  reasonCode: string;
  notes: string;
  documentReference?: string;
}

const AdjustmentForm: React.FC<AdjustmentFormProps> = ({ onSuccess, onCancel }) => {
  const [formData, setFormData] = useState<AdjustmentFormData>({
    itemId: '',
    locationId: '',
    batchNumber: '',
    mrn: '',
    uoMId: '',
    adjustmentType: 'Increase',
    quantityChange: 0,
    newQuantity: 0,
    reasonCode: '',
    notes: '',
    documentReference: '',
  });

  const [items, setItems] = useState<any[]>([]);
  const [locations, setLocations] = useState<any[]>([]);
  const [uoms, setUoms] = useState<any[]>([]);
  const [inventoryBalances, setInventoryBalances] = useState<any[]>([]);
  const [selectedInventory, setSelectedInventory] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const reasonCodes = [
    { value: 'Damaged', label: 'Damaged' },
    { value: 'Lost', label: 'Lost' },
    { value: 'Found', label: 'Found' },
    { value: 'Recount', label: 'Recount / Cycle Count Adjustment' },
    { value: 'Expired', label: 'Expired' },
    { value: 'QualityIssue', label: 'Quality Issue' },
    { value: 'SystemError', label: 'System Error' },
    { value: 'Other', label: 'Other (explain in notes)' },
  ];

  useEffect(() => {
    loadMasterData();
  }, []);

  useEffect(() => {
    if (formData.itemId) {
      loadInventoryForItem(formData.itemId);
    }
  }, [formData.itemId]);

  const loadMasterData = async () => {
    try {
      const [itemsRes, locationsRes, uomsRes] = await Promise.all([
        masterDataApi.getItems(),
        masterDataApi.getLocations(),
        masterDataApi.getUoM()
      ]);
      setItems(itemsRes.data);
      setLocations(locationsRes.data);
      setUoms(uomsRes.data);
    } catch (err: any) {
      setError('Failed to load master data');
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

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    const { name, value } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: name === 'quantityChange' || name === 'newQuantity' ? parseFloat(value) || 0 : value
    }));
  };

  const handleInventorySelect = (inventory: any) => {
    setSelectedInventory(inventory);
    setFormData(prev => ({
      ...prev,
      locationId: inventory.locationId,
      batchNumber: inventory.batchNumber || '',
      mrn: inventory.mrn || '',
      uoMId: inventory.uoMId
    }));
  };

  const handleAdjustmentTypeChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const adjustmentType = e.target.value as 'Increase' | 'Decrease' | 'Set';
    setFormData(prev => ({ ...prev, adjustmentType }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    
    // Validation
    if (!formData.itemId) {
      setError('Item is required');
      return;
    }
    if (!formData.locationId) {
      setError('Location is required');
      return;
    }
    if (!formData.uoMId) {
      setError('Unit of Measure is required');
      return;
    }
    if (formData.adjustmentType !== 'Set' && formData.quantityChange === 0) {
      setError('Quantity change cannot be 0');
      return;
    }
    if (!formData.reasonCode) {
      setError('Reason code is required');
      return;
    }
    if (!formData.notes) {
      setError('Notes are required for all adjustments');
      return;
    }

    // Check if large adjustment (>10% of current stock)
    if (selectedInventory) {
      const currentQty = selectedInventory.quantity;
      const changePercent = Math.abs(formData.quantityChange / currentQty) * 100;
      if (changePercent > 10) {
        const confirm = window.confirm(
          `This adjustment is ${changePercent.toFixed(1)}% of current stock. Are you sure?`
        );
        if (!confirm) return;
      }
    }

    try {
      setLoading(true);
      await wmsApi.createAdjustment(formData);
      onSuccess();
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to create adjustment');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="form-container">
      <div className="form-header">
        <h3>⚙️ Inventory Adjustment</h3>
      </div>

      {error && (
        <div className="alert alert-danger">
          {error}
        </div>
      )}

      <div className="alert alert-warning">
        <strong>⚠️ Important:</strong> All inventory adjustments must have a valid reason and notes for audit purposes.
      </div>

      <form onSubmit={handleSubmit}>
        <div className="form-group">
          <label>Item *</label>
          <select
            name="itemId"
            value={formData.itemId}
            onChange={handleInputChange}
            className="form-control"
            required
          >
            <option value="">-- Select Item --</option>
            {items.map(item => (
              <option key={item.id} value={item.id}>
                {item.code} - {item.name}
              </option>
            ))}
          </select>
        </div>

        {formData.itemId && inventoryBalances.length > 0 && (
          <div className="form-group">
            <label>Select from Available Inventory</label>
            <div style={{ maxHeight: '150px', overflowY: 'auto', border: '1px solid #ddd', borderRadius: '4px' }}>
              <table className="table table-sm table-hover">
                <thead style={{ position: 'sticky', top: 0, background: '#f8f9fa' }}>
                  <tr>
                    <th>Location</th>
                    <th>Batch</th>
                    <th>MRN</th>
                    <th>Qty</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {inventoryBalances.map((inv, idx) => (
                    <tr key={idx}>
                      <td>{inv.location?.name}</td>
                      <td>{inv.batchNumber || '-'}</td>
                      <td>{inv.mrn || '-'}</td>
                      <td>{inv.quantity.toFixed(2)} {inv.uoM?.code}</td>
                      <td>
                        <button
                          type="button"
                          className="btn btn-xs btn-primary"
                          onClick={() => handleInventorySelect(inv)}
                        >
                          Select
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        <div className="form-group">
          <label>Location *</label>
          <select
            name="locationId"
            value={formData.locationId}
            onChange={handleInputChange}
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
        </div>

        {selectedInventory && (
          <div className="alert alert-info">
            <strong>Current Stock:</strong> {selectedInventory.quantity.toFixed(2)} {selectedInventory.uoM?.code}<br />
            <strong>Batch:</strong> {selectedInventory.batchNumber || 'N/A'}<br />
            <strong>MRN:</strong> {selectedInventory.mrn || 'N/A'}
          </div>
        )}

        <div className="form-row">
          <div className="form-group col-md-6">
            <label>Batch Number</label>
            <input
              type="text"
              name="batchNumber"
              value={formData.batchNumber}
              onChange={handleInputChange}
              className="form-control"
            />
          </div>

          <div className="form-group col-md-6">
            <label>MRN</label>
            <input
              type="text"
              name="mrn"
              value={formData.mrn}
              onChange={handleInputChange}
              className="form-control"
            />
          </div>
        </div>

        <div className="form-row">
          <div className="form-group col-md-4">
            <label>Adjustment Type *</label>
            <select
              name="adjustmentType"
              value={formData.adjustmentType}
              onChange={handleAdjustmentTypeChange}
              className="form-control"
              required
            >
              <option value="Increase">Increase (+)</option>
              <option value="Decrease">Decrease (-)</option>
              <option value="Set">Set to Value (=)</option>
            </select>
          </div>

          <div className="form-group col-md-4">
            <label>
              {formData.adjustmentType === 'Set' ? 'New Quantity *' : 'Quantity Change *'}
            </label>
            <input
              type="number"
              name={formData.adjustmentType === 'Set' ? 'newQuantity' : 'quantityChange'}
              value={formData.adjustmentType === 'Set' ? formData.newQuantity : formData.quantityChange}
              onChange={handleInputChange}
              className="form-control"
              step="0.01"
              required
            />
          </div>

          <div className="form-group col-md-4">
            <label>Unit of Measure *</label>
            <select
              name="uoMId"
              value={formData.uoMId}
              onChange={handleInputChange}
              className="form-control"
              required
            >
              <option value="">-- Select UoM --</option>
              {uoms.map(uom => (
                <option key={uom.id} value={uom.id}>
                  {uom.code} - {uom.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="form-group">
          <label>Reason Code *</label>
          <select
            name="reasonCode"
            value={formData.reasonCode}
            onChange={handleInputChange}
            className="form-control"
            required
          >
            <option value="">-- Select Reason --</option>
            {reasonCodes.map(reason => (
              <option key={reason.value} value={reason.value}>
                {reason.label}
              </option>
            ))}
          </select>
        </div>

        <div className="form-group">
          <label>Notes / Explanation *</label>
          <textarea
            name="notes"
            value={formData.notes}
            onChange={handleInputChange}
            className="form-control"
            rows={4}
            required
            placeholder="Required: Explain the reason for this adjustment..."
          />
        </div>

        <div className="form-group">
          <label>Supporting Document Reference</label>
          <input
            type="text"
            name="documentReference"
            value={formData.documentReference}
            onChange={handleInputChange}
            className="form-control"
            placeholder="e.g. Photo #123, Report #456"
          />
        </div>

        <div className="form-actions">
          <button
            type="submit"
            className="btn btn-primary"
            disabled={loading}
          >
            {loading ? 'Processing...' : 'Create Adjustment'}
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

export default AdjustmentForm;
