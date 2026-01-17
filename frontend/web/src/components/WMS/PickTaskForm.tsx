import React, { useState, useEffect } from 'react';
import { wmsApi, masterDataApi } from '../../services/api';
import { PickTaskStatus } from '../../types/wms';

interface PickTaskFormProps {
  onSuccess: () => void;
  onCancel: () => void;
  mode?: 'create' | 'assign' | 'complete';
  existingTask?: any;
}

interface PickTaskFormData {
  itemId: string;
  locationId: string;
  batchNumber?: string;
  mrn?: string;
  quantityToPick: number;
  uoMId: string;
  priority: number;
  orderType?: string; // ProductionOrder or Shipment
  orderReference?: string;
  notes?: string;
  assignedToEmployeeId?: string;
}

const PickTaskForm: React.FC<PickTaskFormProps> = ({ 
  onSuccess, 
  onCancel, 
  mode = 'create',
  existingTask 
}) => {
  const [formData, setFormData] = useState<PickTaskFormData>({
    itemId: existingTask?.itemId || '',
    locationId: existingTask?.locationId || '',
    batchNumber: existingTask?.batchNumber || '',
    mrn: existingTask?.mrn || '',
    quantityToPick: existingTask?.quantityToPick || 0,
    uoMId: existingTask?.uoMId || '',
    priority: existingTask?.priority || 3,
    orderType: existingTask?.orderType || 'ProductionOrder',
    orderReference: existingTask?.orderReference || '',
    notes: existingTask?.notes || '',
    assignedToEmployeeId: existingTask?.assignedToEmployeeId || '',
  });

  const [items, setItems] = useState<any[]>([]);
  const [locations, setLocations] = useState<any[]>([]);
  const [employees, setEmployees] = useState<any[]>([]);
  const [uoms, setUoms] = useState<any[]>([]);
  const [inventoryBalances, setInventoryBalances] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [showInventoryPicker, setShowInventoryPicker] = useState(false);

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
      const [itemsRes, locationsRes, employeesRes, uomsRes] = await Promise.all([
        masterDataApi.getItems(),
        masterDataApi.getLocations(),
        masterDataApi.getEmployees(),
        masterDataApi.getUoM()
      ]);
      setItems(itemsRes.data);
      setLocations(locationsRes.data);
      setEmployees(employeesRes.data);
      setUoms(uomsRes.data);
    } catch (err: any) {
      setError('Failed to load master data');
      console.error(err);
    }
  };

  const loadInventoryForItem = async (itemId: string) => {
    try {
      const response = await wmsApi.getInventory(itemId);
      // Filter only OK quality status for picking
      const availableInventory = response.data.filter((inv: any) => inv.qualityStatus === 1);
      setInventoryBalances(availableInventory);
    } catch (err: any) {
      console.error('Failed to load inventory:', err);
    }
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    const { name, value } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: name === 'quantityToPick' || name === 'priority' ? parseFloat(value) || 0 : value
    }));
  };

  const handleInventorySelect = (inventory: any) => {
    setFormData(prev => ({
      ...prev,
      locationId: inventory.locationId,
      batchNumber: inventory.batchNumber || '',
      mrn: inventory.mrn || '',
      uoMId: inventory.uoMId
    }));
    setShowInventoryPicker(false);
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
    if (formData.quantityToPick <= 0) {
      setError('Quantity must be greater than 0');
      return;
    }
    if (!formData.uoMId) {
      setError('Unit of Measure is required');
      return;
    }

    try {
      setLoading(true);

      if (mode === 'create') {
        await wmsApi.createPickTask(formData);
      } else if (mode === 'assign' && existingTask) {
        if (!formData.assignedToEmployeeId) {
          setError('Employee must be selected for assignment');
          return;
        }
        await wmsApi.assignPickTask(existingTask.id, formData.assignedToEmployeeId);
      } else if (mode === 'complete' && existingTask) {
        const quantityPicked = formData.quantityToPick; // In real scenario, user enters actual picked qty
        await wmsApi.completePickTask(existingTask.id, quantityPicked);
      }

      onSuccess();
    } catch (err: any) {
      setError(err.response?.data?.error || `Failed to ${mode} pick task`);
    } finally {
      setLoading(false);
    }
  };

  const renderCreateMode = () => (
    <>
      <div className="form-group">
        <label>Order Type</label>
        <select
          name="orderType"
          value={formData.orderType}
          onChange={handleInputChange}
          className="form-control"
        >
          <option value="ProductionOrder">Production Order</option>
          <option value="Shipment">Shipment</option>
        </select>
      </div>

      <div className="form-group">
        <label>Order Reference</label>
        <input
          type="text"
          name="orderReference"
          value={formData.orderReference}
          onChange={handleInputChange}
          className="form-control"
          placeholder="e.g. WO-001 or SHIP-001"
        />
      </div>

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
          <label>Available Inventory (OK Quality Only)</label>
          <button
            type="button"
            className="btn btn-sm btn-info"
            onClick={() => setShowInventoryPicker(!showInventoryPicker)}
            style={{ marginLeft: '10px' }}
          >
            {showInventoryPicker ? 'Hide Picker' : 'Show Picker'}
          </button>
          
          {showInventoryPicker && (
            <div className="inventory-picker" style={{ 
              maxHeight: '200px', 
              overflowY: 'auto', 
              border: '1px solid #ddd', 
              marginTop: '10px',
              borderRadius: '4px'
            }}>
              <table className="table table-sm table-hover">
                <thead>
                  <tr style={{ position: 'sticky', top: 0, background: '#f8f9fa' }}>
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
                      <td>{inv.location?.name || inv.locationId}</td>
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
          )}
        </div>
      )}

      <div className="form-group">
        <label>Source Location *</label>
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

      <div className="form-row">
        <div className="form-group col-md-6">
          <label>Batch Number</label>
          <input
            type="text"
            name="batchNumber"
            value={formData.batchNumber}
            onChange={handleInputChange}
            className="form-control"
            placeholder="Optional"
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
            placeholder="Optional"
          />
        </div>
      </div>

      <div className="form-row">
        <div className="form-group col-md-6">
          <label>Quantity to Pick *</label>
          <input
            type="number"
            name="quantityToPick"
            value={formData.quantityToPick}
            onChange={handleInputChange}
            className="form-control"
            step="0.01"
            min="0"
            required
          />
        </div>

        <div className="form-group col-md-6">
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
        <label>Priority (1=Highest, 5=Lowest)</label>
        <select
          name="priority"
          value={formData.priority}
          onChange={handleInputChange}
          className="form-control"
        >
          <option value="1">1 - Critical</option>
          <option value="2">2 - High</option>
          <option value="3">3 - Normal</option>
          <option value="4">4 - Low</option>
          <option value="5">5 - Very Low</option>
        </select>
      </div>

      <div className="form-group">
        <label>Assign To (Optional)</label>
        <select
          name="assignedToEmployeeId"
          value={formData.assignedToEmployeeId}
          onChange={handleInputChange}
          className="form-control"
        >
          <option value="">-- Unassigned --</option>
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
          placeholder="Additional instructions..."
        />
      </div>
    </>
  );

  const renderAssignMode = () => (
    <>
      <div className="alert alert-info">
        <strong>Pick Task:</strong> {existingTask?.taskNumber}<br />
        <strong>Item:</strong> {existingTask?.item?.code} - {existingTask?.item?.name}<br />
        <strong>Location:</strong> {existingTask?.location?.name}<br />
        <strong>Quantity:</strong> {existingTask?.quantityToPick} {existingTask?.uoM?.code}
      </div>

      <div className="form-group">
        <label>Assign To Employee *</label>
        <select
          name="assignedToEmployeeId"
          value={formData.assignedToEmployeeId}
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
    </>
  );

  const renderCompleteMode = () => (
    <>
      <div className="alert alert-info">
        <strong>Pick Task:</strong> {existingTask?.taskNumber}<br />
        <strong>Item:</strong> {existingTask?.item?.code} - {existingTask?.item?.name}<br />
        <strong>Location:</strong> {existingTask?.location?.name}<br />
        <strong>Quantity to Pick:</strong> {existingTask?.quantityToPick} {existingTask?.uoM?.code}
      </div>

      <div className="form-group">
        <label>Quantity Actually Picked *</label>
        <input
          type="number"
          name="quantityToPick"
          value={formData.quantityToPick}
          onChange={handleInputChange}
          className="form-control"
          step="0.01"
          min="0"
          required
        />
        <small className="form-text text-muted">
          Enter the actual quantity picked. If different from planned, a variance will be recorded.
        </small>
      </div>

      <div className="form-group">
        <label>Notes (if variance or issues)</label>
        <textarea
          name="notes"
          value={formData.notes}
          onChange={handleInputChange}
          className="form-control"
          rows={3}
          placeholder="Explain any differences or issues..."
        />
      </div>
    </>
  );

  return (
    <div className="form-container">
      <div className="form-header">
        <h3>
          {mode === 'create' && '🎯 Create Pick Task'}
          {mode === 'assign' && '👤 Assign Pick Task'}
          {mode === 'complete' && '✅ Complete Pick Task'}
        </h3>
      </div>

      {error && (
        <div className="alert alert-danger">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit}>
        {mode === 'create' && renderCreateMode()}
        {mode === 'assign' && renderAssignMode()}
        {mode === 'complete' && renderCompleteMode()}

        <div className="form-actions">
          <button
            type="submit"
            className="btn btn-primary"
            disabled={loading}
          >
            {loading ? 'Processing...' : mode === 'create' ? 'Create Pick Task' : mode === 'assign' ? 'Assign' : 'Complete'}
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

export default PickTaskForm;
