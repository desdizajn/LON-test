import React, { useState, useEffect } from 'react';
import { productionApi, masterDataApi } from '../../services/api';
import { ProductionReceiptFormData } from '../../types/production';
import { toast } from 'react-toastify';
import FormInputSimple from '../forms/FormInputSimple';
import FormSelectSimple from '../forms/FormSelectSimple';
import '../../pages/MasterData/Items/ItemForm.css';

interface ProductionReceiptFormProps {
  productionOrderId?: string;
  onSuccess?: () => void;
  onCancel?: () => void;
}

const ProductionReceiptForm: React.FC<ProductionReceiptFormProps> = ({ productionOrderId, onSuccess, onCancel }) => {
  const [formData, setFormData] = useState<ProductionReceiptFormData>({
    productionOrderId: productionOrderId || '',
    itemId: '',
    quantity: 0,
    uoMId: '',
    locationId: '',
    receiptDate: new Date().toISOString().split('T')[0],
    qualityStatus: 1, // OK
    notes: '',
  });

  const [orders, setOrders] = useState<any[]>([]);
  const [locations, setLocations] = useState<any[]>([]);
  const [selectedOrder, setSelectedOrder] = useState<any>(null);
  const [autoBatchNumber, setAutoBatchNumber] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    loadOrders();
    loadLocations();
  }, []);

  const loadOrders = async () => {
    try {
      const res = await productionApi.getOrders();
      setOrders(res.data.filter((o: any) => o.status === 3)); // InProgress only
      if (productionOrderId) {
        const order = res.data.find((o: any) => o.id === productionOrderId);
        if (order) {
          setSelectedOrder(order);
          setFormData(prev => ({
            ...prev,
            itemId: order.itemId,
            uoMId: order.uoMId,
          }));
        }
      }
    } catch (err) {
      toast.error('Failed to load production orders');
    }
  };

  const loadLocations = async () => {
    try {
      const wh = await masterDataApi.getWarehouses();
      if (wh.data.length > 0) {
        const loc = await masterDataApi.getLocations(wh.data[0].id);
        setLocations(loc.data);
      }
    } catch (err) {
      toast.error('Failed to load locations');
    }
  };

  useEffect(() => {
    if (formData.productionOrderId) {
      productionApi.getOrder(formData.productionOrderId).then(res => {
        setSelectedOrder(res.data);
        setFormData(prev => ({
          ...prev,
          itemId: res.data.itemId,
          uoMId: res.data.uoMId,
        }));
        generateBatchNumber(res.data);
      });
    }
  }, [formData.productionOrderId]);

  const generateBatchNumber = (order: any) => {
    // Auto-generate batch: FG-{ItemCode}-{YYYYMMDD}-{Seq}
    const today = new Date().toISOString().split('T')[0].replace(/-/g, '');
    const itemCode = order.item?.code || 'ITEM';
    const seq = '001'; // In real app, fetch from API to get next sequence
    const batch = `FG-${itemCode}-${today}-${seq}`;
    setAutoBatchNumber(batch);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!formData.productionOrderId || formData.quantity <= 0 || !formData.locationId) {
      toast.error('Please fill all required fields');
      return;
    }

    setLoading(true);
    try {
      // Include auto-generated batch number in request
      const dataWithBatch = {
        ...formData,
        batchNumber: autoBatchNumber,
      };
      await productionApi.createProductionReceipt(dataWithBatch);
      toast.success('Production receipt created successfully!');
      if (onSuccess) onSuccess();
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Failed to create production receipt');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="form-container">
      <div className="form-header">
        <h2>Production Receipt (Finished Goods)</h2>
        <button className="btn btn-secondary" onClick={onCancel} disabled={loading}>
          Cancel
        </button>
      </div>

      <form onSubmit={handleSubmit} className="item-form">
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
          <div className="info-card" style={{ padding: '15px', background: '#e8f5e9', borderRadius: '4px', marginBottom: '15px' }}>
            <h4>Production Order Details</h4>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px', marginTop: '10px' }}>
              <div>
                <strong>Order Number:</strong> {selectedOrder.orderNumber} <br />
                <strong>Item:</strong> {selectedOrder.item?.name} <br />
                <strong>Item Code:</strong> {selectedOrder.item?.code}
              </div>
              <div>
                <strong>Order Qty:</strong> {selectedOrder.orderQuantity} {selectedOrder.uoM?.code} <br />
                <strong>Produced Qty:</strong> {selectedOrder.producedQuantity} <br />
                <strong>Remaining:</strong> {(selectedOrder.orderQuantity - selectedOrder.producedQuantity).toFixed(2)}
              </div>
            </div>
          </div>
        )}

        <div className="form-grid">
          <FormInputSimple
            label="Quantity Received"
            type="number"
            value={formData.quantity.toString()}
            onChange={(value) => setFormData({ ...formData, quantity: parseFloat(value) || 0 })}
            required
          />

          <FormSelectSimple
            label="Location"
            value={formData.locationId}
            onChange={(value) => setFormData({ ...formData, locationId: value })}
            options={locations.map(loc => ({ value: loc.id, label: loc.name }))}
            required
          />

          <FormSelectSimple
            label="Quality Status"
            value={formData.qualityStatus.toString()}
            onChange={(value) => setFormData({ ...formData, qualityStatus: parseInt(value) })}
            options={[
              { value: '1', label: 'OK' },
              { value: '2', label: 'Blocked' },
              { value: '3', label: 'Quarantine' },
            ]}
            required
          />

          <FormInputSimple
            label="Receipt Date"
            type="date"
            value={formData.receiptDate}
            onChange={(value) => setFormData({ ...formData, receiptDate: value })}
            required
          />
        </div>

        {/* Auto-Generated Batch Number Display */}
        {autoBatchNumber && (
          <div className="form-section" style={{ border: '2px solid #4caf50', padding: '15px', borderRadius: '4px', background: '#f1f8e9' }}>
            <h4 style={{ color: '#4caf50', marginBottom: '10px' }}>✓ Auto-Generated Batch Number</h4>
            <div style={{ fontSize: '18px', fontFamily: 'monospace', fontWeight: 'bold', color: '#2e7d32' }}>
              {autoBatchNumber}
            </div>
            <p style={{ fontSize: '12px', color: '#666', marginTop: '5px' }}>
              Format: FG-{selectedOrder?.item?.code || 'ItemCode'}-{new Date().toISOString().split('T')[0].replace(/-/g, '')}-Sequence
            </p>
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
            {loading ? 'Receiving...' : 'Receive Finished Goods'}
          </button>
        </div>
      </form>
    </div>
  );
};

export default ProductionReceiptForm;
