import React, { useState, useEffect } from 'react';
import { wmsApi, masterDataApi } from '../../services/api';
import { ShipmentFormData, ShipmentLine } from '../../types/wms';
import { toast } from 'react-toastify';
import FormInputSimple from '../forms/FormInputSimple';
import FormSelectSimple from '../forms/FormSelectSimple';
import '../../pages/MasterData/Items/ItemForm.css';

interface ShipmentFormProps {
  onSuccess?: () => void;
  onCancel?: () => void;
}

const ShipmentForm: React.FC<ShipmentFormProps> = ({ onSuccess, onCancel }) => {
  const [formData, setFormData] = useState<ShipmentFormData>({
    shipmentDate: new Date().toISOString().split('T')[0],
    warehouseId: '',
    customerId: '',
    carrierId: '',
    trackingNumber: '',
    notes: '',
    lines: [],
  });

  const [warehouses, setWarehouses] = useState<any[]>([]);
  const [customers, setCustomers] = useState<any[]>([]);
  const [carriers, setCarriers] = useState<any[]>([]);
  const [items, setItems] = useState<any[]>([]);
  const [locations, setLocations] = useState<any[]>([]);
  const [uoms, setUoms] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      const [whRes, custRes, carrRes, itemsRes, uomRes] = await Promise.all([
        masterDataApi.getWarehouses(),
        masterDataApi.getPartners('2'), // Customer type
        masterDataApi.getPartners('3'), // Carrier type
        masterDataApi.getItems(),
        masterDataApi.getUoM(),
      ]);
      setWarehouses(whRes.data);
      setCustomers(custRes.data);
      setCarriers(carrRes.data);
      setItems(itemsRes.data);
      setUoms(uomRes.data);
    } catch (err) {
      toast.error('Failed to load data');
    }
  };

  useEffect(() => {
    if (formData.warehouseId) {
      masterDataApi.getLocations(formData.warehouseId).then(res => setLocations(res.data));
    }
  }, [formData.warehouseId]);

  const handleAddLine = () => {
    const newLine: ShipmentLine = {
      itemId: '',
      quantity: 0,
      uoMId: '',
      batchNumber: '',
      mrn: '',
      locationId: '',
    };
    setFormData({ ...formData, lines: [...formData.lines, newLine] });
  };

  const handleRemoveLine = (index: number) => {
    setFormData({ ...formData, lines: formData.lines.filter((_, i) => i !== index) });
  };

  const handleLineChange = (index: number, field: keyof ShipmentLine, value: any) => {
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
    
    if (!formData.warehouseId) {
      toast.error('Please select a warehouse');
      return;
    }
    
    if (formData.lines.length === 0) {
      toast.error('Please add at least one line');
      return;
    }

    setLoading(true);
    try {
      await wmsApi.createShipment(formData);
      toast.success('Shipment created successfully!');
      if (onSuccess) onSuccess();
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Failed to create shipment');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="form-container">
      <div className="form-header">
        <h2>Create Shipment</h2>
        <button className="btn btn-secondary" onClick={onCancel} disabled={loading}>
          Cancel
        </button>
      </div>

      <form onSubmit={handleSubmit} className="item-form">
        <div className="form-grid">
          <FormInputSimple
            label="Shipment Date"
            type="date"
            value={formData.shipmentDate}
            onChange={(value) => setFormData({ ...formData, shipmentDate: value })}
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
            label="Customer"
            value={formData.customerId || ''}
            onChange={(value) => setFormData({ ...formData, customerId: value })}
            options={customers.map(c => ({ value: c.id, label: c.name }))}
          />

          <FormSelectSimple
            label="Carrier"
            value={formData.carrierId || ''}
            onChange={(value) => setFormData({ ...formData, carrierId: value })}
            options={carriers.map(c => ({ value: c.id, label: c.name }))}
          />

          <FormInputSimple
            label="Tracking Number"
            value={formData.trackingNumber || ''}
            onChange={(value) => setFormData({ ...formData, trackingNumber: value })}
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
            <h3>Shipment Lines</h3>
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
              </div>
            </div>
          ))}
        </div>

        <div className="form-actions">
          <button type="button" className="btn btn-secondary" onClick={onCancel} disabled={loading}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={loading}>
            {loading ? 'Creating...' : 'Create Shipment'}
          </button>
        </div>
      </form>
    </div>
  );
};

export default ShipmentForm;
