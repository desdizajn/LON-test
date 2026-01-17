import React, { useState, useEffect } from 'react';
import { productionApi, masterDataApi } from '../../services/api';
import { ProductionOrderFormData } from '../../types/production';
import { toast } from 'react-toastify';
import FormInputSimple from '../forms/FormInputSimple';
import FormSelectSimple from '../forms/FormSelectSimple';
import '../../pages/MasterData/Items/ItemForm.css';

interface ProductionOrderFormProps {
  onSuccess?: () => void;
  onCancel?: () => void;
}

const ProductionOrderForm: React.FC<ProductionOrderFormProps> = ({ onSuccess, onCancel }) => {
  const [formData, setFormData] = useState<ProductionOrderFormData>({
    itemId: '',
    bomId: '',
    routingId: '',
    orderQuantity: 0,
    uoMId: '',
    plannedStartDate: new Date().toISOString().split('T')[0],
    plannedEndDate: new Date().toISOString().split('T')[0],
    priority: 5,
    notes: '',
  });

  const [items, setItems] = useState<any[]>([]);
  const [boms, setBoms] = useState<any[]>([]);
  const [routings, setRoutings] = useState<any[]>([]);
  const [uoms, setUoms] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      const [itemsRes, uomRes] = await Promise.all([
        masterDataApi.getItems(),
        masterDataApi.getUoM(),
      ]);
      setItems(itemsRes.data.filter((i: any) => i.itemType === 3)); // Finished Goods only
      setUoms(uomRes.data);
    } catch (err) {
      toast.error('Failed to load data');
    }
  };

  useEffect(() => {
    if (formData.itemId) {
      // Load BOMs for selected item
      productionApi.getBOMs(formData.itemId).then(res => {
        setBoms(res.data);
        if (res.data.length > 0) {
          setFormData(prev => ({ ...prev, bomId: res.data[0].id }));
        }
      });
      
      // Auto-set UoM
      const selectedItem = items.find(i => i.id === formData.itemId);
      if (selectedItem) {
        setFormData(prev => ({ ...prev, uoMId: selectedItem.uoMId }));
      }
    }
  }, [formData.itemId, items]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!formData.itemId || formData.orderQuantity <= 0) {
      toast.error('Please fill all required fields');
      return;
    }

    setLoading(true);
    try {
      await productionApi.createOrder(formData);
      toast.success('Production Order created successfully!');
      if (onSuccess) onSuccess();
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Failed to create production order');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="form-container">
      <div className="form-header">
        <h2>Create Production Order</h2>
        <button className="btn btn-secondary" onClick={onCancel} disabled={loading}>
          Cancel
        </button>
      </div>

      <form onSubmit={handleSubmit} className="item-form">
        <div className="form-grid">
          <FormSelectSimple
            label="Finished Good Item"
            value={formData.itemId}
            onChange={(value) => setFormData({ ...formData, itemId: value })}
            options={items.map(item => ({ value: item.id, label: `${item.code} - ${item.name}` }))}
            required
          />

          <FormInputSimple
            label="Order Quantity"
            type="number"
            value={formData.orderQuantity.toString()}
            onChange={(value) => setFormData({ ...formData, orderQuantity: parseFloat(value) || 0 })}
            required
          />

          <FormSelectSimple
            label="UoM"
            value={formData.uoMId}
            onChange={(value) => setFormData({ ...formData, uoMId: value })}
            options={uoms.map(u => ({ value: u.id, label: u.code }))}
            required
          />

          <FormSelectSimple
            label="BOM"
            value={formData.bomId || ''}
            onChange={(value) => setFormData({ ...formData, bomId: value })}
            options={boms.map(bom => ({ value: bom.id, label: `${bom.bomNumber} (v${bom.version})` }))}
          />

          <FormInputSimple
            label="Planned Start Date"
            type="date"
            value={formData.plannedStartDate}
            onChange={(value) => setFormData({ ...formData, plannedStartDate: value })}
            required
          />

          <FormInputSimple
            label="Planned End Date"
            type="date"
            value={formData.plannedEndDate}
            onChange={(value) => setFormData({ ...formData, plannedEndDate: value })}
            required
          />

          <FormInputSimple
            label="Priority"
            type="number"
            value={formData.priority.toString()}
            onChange={(value) => setFormData({ ...formData, priority: parseInt(value) || 5 })}
            min={1}
            max={10}
          />
        </div>

        <FormInputSimple
          label="Notes"
          value={formData.notes || ''}
          onChange={(value) => setFormData({ ...formData, notes: value })}
          multiline
        />

        <div className="form-actions">
          <button type="button" className="btn btn-secondary" onClick={onCancel} disabled={loading}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={loading}>
            {loading ? 'Creating...' : 'Create Production Order'}
          </button>
        </div>
      </form>
    </div>
  );
};

export default ProductionOrderForm;
