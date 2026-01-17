import React, { useState, useEffect } from 'react';
import { wmsApi, masterDataApi } from '../../services/api';
import { TransferFormData, TransferLine, QualityStatus } from '../../types/wms';
import { toast } from 'react-toastify';
import FormInputSimple from '../forms/FormInputSimple';
import FormSelectSimple from '../forms/FormSelectSimple';
import '../../pages/MasterData/Items/ItemForm.css';

interface TransferFormProps {
  onSuccess?: () => void;
  onCancel?: () => void;
}

const TransferForm: React.FC<TransferFormProps> = ({ onSuccess, onCancel }) => {
  const [formData, setFormData] = useState<TransferFormData>({
    transferDate: new Date().toISOString().split('T')[0],
    fromWarehouseId: '',
    toWarehouseId: '',
    notes: '',
    lines: [],
  });

  const [warehouses, setWarehouses] = useState<any[]>([]);
  const [items, setItems] = useState<any[]>([]);
  const [fromLocations, setFromLocations] = useState<any[]>([]);
  const [toLocations, setToLocations] = useState<any[]>([]);
  const [uoms, setUoms] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      const [whRes, itemsRes, uomRes] = await Promise.all([
        masterDataApi.getWarehouses(),
        masterDataApi.getItems(),
        masterDataApi.getUoM(),
      ]);
      setWarehouses(whRes.data);
      setItems(itemsRes.data);
      setUoms(uomRes.data);
    } catch (err) {
      toast.error('Failed to load data');
    }
  };

  useEffect(() => {
    if (formData.fromWarehouseId) {
      masterDataApi.getLocations(formData.fromWarehouseId).then(res => setFromLocations(res.data));
    }
  }, [formData.fromWarehouseId]);

  useEffect(() => {
    if (formData.toWarehouseId) {
      masterDataApi.getLocations(formData.toWarehouseId).then(res => setToLocations(res.data));
    }
  }, [formData.toWarehouseId]);

  const handleAddLine = () => {
    const newLine: TransferLine = {
      itemId: '',
      quantity: 0,
      uoMId: '',
      batchNumber: '',
      mrn: '',
      fromLocationId: '',
      toLocationId: '',
      qualityStatus: QualityStatus.OK,
    };
    setFormData({ ...formData, lines: [...formData.lines, newLine] });
  };

  const handleRemoveLine = (index: number) => {
    setFormData({ ...formData, lines: formData.lines.filter((_, i) => i !== index) });
  };

  const handleLineChange = (index: number, field: keyof TransferLine, value: any) => {
    const updatedLines = [...formData.lines];
    updatedLines[index] = { ...updatedLines[index], [field]: value };
    
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
    
    if (!formData.fromWarehouseId || !formData.toWarehouseId) {
      toast.error('Please select warehouses');
      return;
    }
    
    if (formData.lines.length === 0) {
      toast.error('Please add at least one line');
      return;
    }

    setLoading(true);
    try {
      await wmsApi.createTransfer(formData);
      toast.success('Transfer created successfully!');
      if (onSuccess) onSuccess();
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Failed to create transfer');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="form-container">
      <div className="form-header">
        <h2>Create Transfer</h2>
        <button className="btn btn-secondary" onClick={onCancel} disabled={loading}>
          Cancel
        </button>
      </div>

      <form onSubmit={handleSubmit} className="item-form">
        <div className="form-grid">
          <FormInputSimple
            label="Transfer Date"
            type="date"
            value={formData.transferDate}
            onChange={(value) => setFormData({ ...formData, transferDate: value })}
            required
          />

          <FormSelectSimple
            label="From Warehouse"
            value={formData.fromWarehouseId}
            onChange={(value) => setFormData({ ...formData, fromWarehouseId: value })}
            options={warehouses.map(wh => ({ value: wh.id, label: wh.name }))}
            required
          />

          <FormSelectSimple
            label="To Warehouse"
            value={formData.toWarehouseId}
            onChange={(value) => setFormData({ ...formData, toWarehouseId: value })}
            options={warehouses.map(wh => ({ value: wh.id, label: wh.name }))}
            required
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
            <h3>Transfer Lines</h3>
            <button type="button" className="btn btn-primary" onClick={handleAddLine}>
              + Add Line
            </button>
          </div>

          {formData.lines.map((line, index) => (
            <div key={index} className="line-item" style={{ border: '1px solid #ddd', padding: '15px', marginBottom: '15px', borderRadius: '4px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
                <h4>Line {index + 1}</h4>
                <button type="button" className="btn btn-danger btn-sm" onClick={() => handleRemoveLine(index)}>
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
                  label="From Location"
                  value={line.fromLocationId}
                  onChange={(value) => handleLineChange(index, 'fromLocationId', value)}
                  options={fromLocations.map(loc => ({ value: loc.id, label: loc.name }))}
                  required
                />

                <FormSelectSimple
                  label="To Location"
                  value={line.toLocationId}
                  onChange={(value) => handleLineChange(index, 'toLocationId', value)}
                  options={toLocations.map(loc => ({ value: loc.id, label: loc.name }))}
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
              </div>
            </div>
          ))}
        </div>

        <div className="form-actions">
          <button type="button" className="btn btn-secondary" onClick={onCancel} disabled={loading}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={loading}>
            {loading ? 'Creating...' : 'Create Transfer'}
          </button>
        </div>
      </form>
    </div>
  );
};

export default TransferForm;
