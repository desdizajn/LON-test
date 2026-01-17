import React, { useState, useEffect } from 'react';
import { wmsApi, masterDataApi } from '../../services/api';
import { ReceiptFormData, ReceiptLine, QualityStatus } from '../../types/wms';
import { toast } from 'react-toastify';
import FormInputSimple from '../forms/FormInputSimple';
import FormSelectSimple from '../forms/FormSelectSimple';
import FormAutocomplete from '../forms/FormAutocomplete';
import '../../pages/MasterData/Items/ItemForm.css';

interface ReceiptFormProps {
  onSuccess?: () => void;
  onCancel?: () => void;
}

const ReceiptForm: React.FC<ReceiptFormProps> = ({ onSuccess, onCancel }) => {
  const [formData, setFormData] = useState<ReceiptFormData>({
    receiptDate: new Date().toISOString().split('T')[0],
    warehouseId: '',
    supplierId: '',
    referenceNumber: '',
    notes: '',
    lines: [],
  });

  const [warehouses, setWarehouses] = useState<any[]>([]);
  const [suppliers, setSuppliers] = useState<any[]>([]);
  const [items, setItems] = useState<any[]>([]);
  const [locations, setLocations] = useState<any[]>([]);
  const [uoms, setUoms] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      const [whRes, suppRes, itemsRes, uomRes] = await Promise.all([
        masterDataApi.getWarehouses(),
        masterDataApi.getPartners('1'), // Supplier type
        masterDataApi.getItems(),
        masterDataApi.getUoM(),
      ]);
      setWarehouses(whRes.data);
      setSuppliers(suppRes.data);
      setItems(itemsRes.data);
      setUoms(uomRes.data);
    } catch (err) {
      toast.error('Failed to load data');
    }
  };

  const loadLocations = async (warehouseId: string) => {
    try {
      const res = await masterDataApi.getLocations(warehouseId);
      setLocations(res.data);
    } catch (err) {
      toast.error('Failed to load locations');
    }
  };

  useEffect(() => {
    if (formData.warehouseId) {
      loadLocations(formData.warehouseId);
    }
  }, [formData.warehouseId]);

  const handleAddLine = () => {
    const newLine: ReceiptLine = {
      itemId: '',
      quantity: 0,
      uoMId: '',
      batchNumber: '',
      mrn: '',
      locationId: '',
      qualityStatus: QualityStatus.OK,
      expiryDate: '',
      notes: '',
    };
    setFormData({
      ...formData,
      lines: [...formData.lines, newLine],
    });
  };

  const handleRemoveLine = (index: number) => {
    setFormData({
      ...formData,
      lines: formData.lines.filter((_, i) => i !== index),
    });
  };

  const handleLineChange = (index: number, field: keyof ReceiptLine, value: any) => {
    const updatedLines = [...formData.lines];
    updatedLines[index] = { ...updatedLines[index], [field]: value };
    
    // Auto-set UoM when item is selected
    if (field === 'itemId' && value) {
      const selectedItem = items.find(item => item.id === value);
      if (selectedItem) {
        updatedLines[index].uoMId = selectedItem.uoMId;
      }
    }
    
    setFormData({ ...formData, lines: updatedLines });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!formData.warehouseId) {
      toast.error('Please select a warehouse');
      return;
    }
    
    if (formData.lines.length === 0) {
      toast.error('Please add at least one line');
      return;
    }
    
    // Validate lines
    for (let i = 0; i < formData.lines.length; i++) {
      const line = formData.lines[i];
      if (!line.itemId || !line.locationId || line.quantity <= 0) {
        toast.error(`Line ${i + 1}: Please fill all required fields`);
        return;
      }
    }

    setLoading(true);
    try {
      await wmsApi.createReceipt(formData);
      toast.success('Receipt created successfully!');
      if (onSuccess) onSuccess();
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Failed to create receipt');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="form-container">
      <div className="form-header">
        <h2>Create Receipt</h2>
        <button className="btn btn-secondary" onClick={onCancel} disabled={loading}>
          Cancel
        </button>
      </div>

      <form onSubmit={handleSubmit} className="item-form">
        <div className="form-grid">
          <FormInputSimple
            label="Receipt Date"
            type="date"
            value={formData.receiptDate}
            onChange={(value) => setFormData({ ...formData, receiptDate: value })}
            required
          />

          <FormSelectSimple
            label="Warehouse"
            value={formData.warehouseId}
            onChange={(value) => setFormData({ ...formData, warehouseId: value })}
            options={warehouses.map(wh => ({ value: wh.id, label: wh.name }))}
            required
          />

          <FormSelectSimple
            label="Supplier"
            value={formData.supplierId || ''}
            onChange={(value) => setFormData({ ...formData, supplierId: value })}
            options={suppliers.map(s => ({ value: s.id, label: s.name }))}
          />

          <FormInputSimple
            label="Reference Number"
            value={formData.referenceNumber || ''}
            onChange={(value) => setFormData({ ...formData, referenceNumber: value })}
          />
        </div>

        <FormInputSimple
          label="Notes"
          value={formData.notes || ''}
          onChange={(value) => setFormData({ ...formData, notes: value })}
          multiline
        />

        <div className="form-section">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '15px' }}>
            <h3>Receipt Lines</h3>
            <button type="button" className="btn btn-primary" onClick={handleAddLine}>
              + Add Line
            </button>
          </div>

          {formData.lines.length === 0 && (
            <div className="empty-state">No lines added. Click "Add Line" to start.</div>
          )}

          {formData.lines.map((line, index) => (
            <div key={index} className="line-item" style={{ border: '1px solid #ddd', padding: '15px', marginBottom: '15px', borderRadius: '4px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
                <h4>Line {index + 1}</h4>
                <button
                  type="button"
                  className="btn btn-danger btn-sm"
                  onClick={() => handleRemoveLine(index)}
                >
                  Remove
                </button>
              </div>

              <div className="form-grid">
                <FormSelectSimple
                  label="Item"
                  value={line.itemId}
                  onChange={(value) => handleLineChange(index, 'itemId', value)}
                  options={items.map(item => ({ value: item.id, label: `${item.code} - ${item.name}` }))}
                  required
                />

                <FormInputSimple
                  label="Quantity"
                  type="number"
                  value={line.quantity.toString()}
                  onChange={(value) => handleLineChange(index, 'quantity', parseFloat(value) || 0)}
                  required
                />

                <FormSelectSimple
                  label="UoM"
                  value={line.uoMId}
                  onChange={(value) => handleLineChange(index, 'uoMId', value)}
                  options={uoms.map(u => ({ value: u.id, label: u.code }))}
                  required
                />

                <FormSelectSimple
                  label="Location"
                  value={line.locationId}
                  onChange={(value) => handleLineChange(index, 'locationId', value)}
                  options={locations.map(loc => ({ value: loc.id, label: loc.name }))}
                  required
                />

                <FormInputSimple
                  label="Batch Number"
                  value={line.batchNumber || ''}
                  onChange={(value) => handleLineChange(index, 'batchNumber', value)}
                />

                <FormInputSimple
                  label="MRN"
                  value={line.mrn || ''}
                  onChange={(value) => handleLineChange(index, 'mrn', value)}
                />

                <FormSelectSimple
                  label="Quality Status"
                  value={line.qualityStatus.toString()}
                  onChange={(value) => handleLineChange(index, 'qualityStatus', parseInt(value))}
                  options={[
                    { value: '1', label: 'OK' },
                    { value: '2', label: 'Blocked' },
                    { value: '3', label: 'Quarantine' },
                  ]}
                  required
                />

                <FormInputSimple
                  label="Expiry Date"
                  type="date"
                  value={line.expiryDate || ''}
                  onChange={(value) => handleLineChange(index, 'expiryDate', value)}
                />
              </div>

              <FormInputSimple
                label="Line Notes"
                value={line.notes || ''}
                onChange={(value) => handleLineChange(index, 'notes', value)}
                multiline
              />
            </div>
          ))}
        </div>

        <div className="form-actions">
          <button type="button" className="btn btn-secondary" onClick={onCancel} disabled={loading}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={loading}>
            {loading ? 'Creating...' : 'Create Receipt'}
          </button>
        </div>
      </form>
    </div>
  );
};

export default ReceiptForm;
