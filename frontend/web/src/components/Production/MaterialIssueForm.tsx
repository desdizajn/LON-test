import React, { useState, useEffect } from 'react';
import { productionApi, wmsApi, masterDataApi } from '../../services/api';
import { MaterialIssueFormData } from '../../types/production';
import { toast } from 'react-toastify';
import FormInputSimple from '../forms/FormInputSimple';
import FormSelectSimple from '../forms/FormSelectSimple';
import '../../pages/MasterData/Items/ItemForm.css';

interface MaterialIssueFormProps {
  productionOrderId?: string;
  onSuccess?: () => void;
  onCancel?: () => void;
}

const MaterialIssueForm: React.FC<MaterialIssueFormProps> = ({ productionOrderId, onSuccess, onCancel }) => {
  const [formData, setFormData] = useState<MaterialIssueFormData>({
    productionOrderId: productionOrderId || '',
    itemId: '',
    quantity: 0,
    uoMId: '',
    batchNumber: '',
    mrn: '',
    locationId: '',
    issueDate: new Date().toISOString().split('T')[0],
    notes: '',
  });

  const [orders, setOrders] = useState<any[]>([]);
  const [items, setItems] = useState<any[]>([]);
  const [uoms, setUoms] = useState<any[]>([]);
  const [inventory, setInventory] = useState<any[]>([]);
  const [locations, setLocations] = useState<any[]>([]);
  const [selectedOrder, setSelectedOrder] = useState<any>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    loadOrders();
    loadUoms();
  }, []);

  const loadOrders = async () => {
    try {
      const res = await productionApi.getOrders();
      setOrders(res.data.filter((o: any) => o.status === 2 || o.status === 3)); // Released or InProgress
      if (productionOrderId) {
        const order = res.data.find((o: any) => o.id === productionOrderId);
        if (order) setSelectedOrder(order);
      }
    } catch (err) {
      toast.error('Failed to load production orders');
    }
  };

  const loadUoms = async () => {
    try {
      const res = await masterDataApi.getUoM();
      setUoms(res.data);
    } catch (err) {
      toast.error('Failed to load UoMs');
    }
  };

  useEffect(() => {
    if (formData.productionOrderId) {
      productionApi.getOrder(formData.productionOrderId).then(res => {
        setSelectedOrder(res.data);
        // Load items from BOM
        if (res.data.bom && res.data.bom.lines) {
          setItems(res.data.bom.lines.map((l: any) => l.item));
        }
      });
    }
  }, [formData.productionOrderId]);

  useEffect(() => {
    if (formData.itemId) {
      // Load available inventory for selected item
      wmsApi.getInventory(formData.itemId).then(res => {
        // Filter only OK quality status
        const availableInventory = res.data.filter((inv: any) => inv.qualityStatus === 1 && inv.quantity > 0);
        setInventory(availableInventory);
        
        // Extract unique locations
        const uniqueLocations = Array.from(new Set(availableInventory.map((inv: any) => inv.location)))
          .filter(Boolean);
        setLocations(uniqueLocations as any[]);
      });
      
      // Auto-set UoM
      const selectedItem = items.find(i => i.id === formData.itemId);
      if (selectedItem) {
        setFormData(prev => ({ ...prev, uoMId: selectedItem.uoMId }));
      }
    }
  }, [formData.itemId, items]);

  const handleBatchSelect = (inventoryId: string) => {
    const selectedInv = inventory.find(inv => inv.id === inventoryId);
    if (selectedInv) {
      setFormData(prev => ({
        ...prev,
        batchNumber: selectedInv.batchNumber || '',
        mrn: selectedInv.mrn || '',
        locationId: selectedInv.locationId,
      }));
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!formData.productionOrderId || !formData.itemId || formData.quantity <= 0) {
      toast.error('Please fill all required fields');
      return;
    }
    
    if (!formData.batchNumber) {
      toast.error('Batch number is required for traceability');
      return;
    }

    setLoading(true);
    try {
      await productionApi.createMaterialIssue(formData);
      toast.success('Material issued successfully!');
      if (onSuccess) onSuccess();
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Failed to issue material');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="form-container">
      <div className="form-header">
        <h2>Issue Material to Production Order</h2>
        <button className="btn btn-secondary" onClick={onCancel} disabled={loading}>
          Cancel
        </button>
      </div>

      <form onSubmit={handleSubmit} className="item-form">
        <div className="form-grid">
          {!productionOrderId && (
            <FormSelectSimple
              label="Production Order"
              value={formData.productionOrderId}
              onChange={(value) => setFormData({ ...formData, productionOrderId: value })}
              options={orders.map(o => ({ value: o.id, label: `${o.orderNumber} - ${o.item?.name}` }))}
              required
            />
          )}

          {selectedOrder && (
            <div className="info-card" style={{ gridColumn: '1 / -1', padding: '15px', background: '#f0f8ff', borderRadius: '4px', marginBottom: '15px' }}>
              <strong>Order:</strong> {selectedOrder.orderNumber} <br />
              <strong>Item:</strong> {selectedOrder.item?.name} <br />
              <strong>Order Qty:</strong> {selectedOrder.orderQuantity} <br />
              <strong>Status:</strong> {selectedOrder.status === 2 ? 'Released' : 'In Progress'}
            </div>
          )}

          <FormSelectSimple
            label="Material Item"
            value={formData.itemId}
            onChange={(value) => setFormData({ ...formData, itemId: value })}
            options={items.map(item => ({ value: item.id, label: `${item.code} - ${item.name}` }))}
            required
          />

          <FormInputSimple
            label="Quantity"
            type="number"
            value={formData.quantity.toString()}
            onChange={(value) => setFormData({ ...formData, quantity: parseFloat(value) || 0 })}
            required
          />

          <FormSelectSimple
            label="UoM"
            value={formData.uoMId}
            onChange={(value) => setFormData({ ...formData, uoMId: value })}
            options={uoms.map(u => ({ value: u.id, label: u.code }))}
            required
          />

          <FormInputSimple
            label="Issue Date"
            type="date"
            value={formData.issueDate}
            onChange={(value) => setFormData({ ...formData, issueDate: value })}
            required
          />
        </div>

        {/* Batch + MRN Picker Section - CRITICAL FOR TRACEABILITY */}
        {formData.itemId && inventory.length > 0 && (
          <div className="form-section" style={{ border: '2px solid #3498db', padding: '15px', borderRadius: '4px', marginTop: '15px' }}>
            <h3 style={{ color: '#3498db' }}>⚡ Select Batch + MRN (Required for Traceability)</h3>
            <p style={{ fontSize: '14px', color: '#666', marginBottom: '15px' }}>
              Select the inventory batch to issue. Batch and MRN are tracked for full traceability.
            </p>

            <div className="inventory-picker">
              {inventory.map((inv) => (
                <div
                  key={inv.id}
                  className="inventory-item"
                  style={{
                    border: '1px solid #ddd',
                    padding: '12px',
                    marginBottom: '10px',
                    borderRadius: '4px',
                    cursor: 'pointer',
                    background: formData.batchNumber === inv.batchNumber ? '#e3f2fd' : '#fff',
                  }}
                  onClick={() => handleBatchSelect(inv.id)}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <div>
                      <strong>Batch:</strong> {inv.batchNumber || 'N/A'} <br />
                      <strong>MRN:</strong> {inv.mrn || 'N/A'} <br />
                      <strong>Location:</strong> {inv.location?.name}
                    </div>
                    <div style={{ textAlign: 'right' }}>
                      <strong style={{ fontSize: '18px', color: '#27ae60' }}>
                        {inv.quantity.toFixed(2)} {inv.uoM?.code}
                      </strong>
                      <br />
                      <span style={{ fontSize: '12px', color: '#666' }}>Available</span>
                    </div>
                  </div>
                </div>
              ))}
            </div>

            {inventory.length === 0 && (
              <div className="empty-state">No available inventory for this item</div>
            )}
          </div>
        )}

        {formData.batchNumber && (
          <div className="form-grid" style={{ marginTop: '15px' }}>
            <FormInputSimple
              label="Batch Number"
              value={formData.batchNumber}
              onChange={(value) => setFormData({ ...formData, batchNumber: value })}
              required
              disabled
            />

            <FormInputSimple
              label="MRN"
              value={formData.mrn || ''}
              onChange={(value) => setFormData({ ...formData, mrn: value })}
              disabled
            />

            <FormSelectSimple
              label="Location"
              value={formData.locationId}
              onChange={(value) => setFormData({ ...formData, locationId: value })}
              options={locations.map(loc => ({ value: loc.id, label: loc.name }))}
              required
              disabled
            />
          </div>
        )}

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
            {loading ? 'Issuing...' : 'Issue Material'}
          </button>
        </div>
      </form>
    </div>
  );
};

export default MaterialIssueForm;
