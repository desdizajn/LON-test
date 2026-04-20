import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { wmsApi } from '../services/api';
import ReceiptForm from '../components/WMS/ReceiptForm';
import TransferForm from '../components/WMS/TransferForm';
import ShipmentForm from '../components/WMS/ShipmentForm';
import CycleCountForm from '../components/WMS/CycleCountForm';
import AdjustmentForm from '../components/WMS/AdjustmentForm';
import QualityStatusChangeForm from '../components/WMS/QualityStatusChangeForm';
import MoveBatchModal from '../components/WMS/MoveBatchModal';

const Inventory: React.FC = () => {
  const { t } = useTranslation();
  const [inventory, setInventory] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeForm, setActiveForm] = useState<string | null>(null);
  const [moveBatchRow, setMoveBatchRow] = useState<any | null>(null);

  useEffect(() => {
    loadInventory();
  }, []);

  const loadInventory = async () => {
    try {
      setLoading(true);
      const response = await wmsApi.getInventory();
      setInventory(response.data);
    } catch (err) {
      console.error('Failed to load inventory', err);
    } finally {
      setLoading(false);
    }
  };

  const handleFormSuccess = () => {
    setActiveForm(null);
    loadInventory();
  };

  const handleFormCancel = () => {
    setActiveForm(null);
  };

  // Render active form
  if (activeForm === 'receipt') {
    return <ReceiptForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }
  if (activeForm === 'transfer') {
    return <TransferForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }
  if (activeForm === 'shipment') {
    return <ShipmentForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }
  if (activeForm === 'cyclecount') {
    return <CycleCountForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }
  if (activeForm === 'adjustment') {
    return <AdjustmentForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }
  if (activeForm === 'qualitychange') {
    return <QualityStatusChangeForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  }

  if (loading) return <div className="loading">{t('inventory.loading')}</div>;

  return (
    <div>
      <div className="header">
        <h2>📦 {t('inventory.title')}</h2>
        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
          <button onClick={() => setActiveForm('receipt')} style={{ background: 'var(--success)', color: 'white', borderColor: 'var(--success)' }}>
            ➕ {t('inventory.actions.receipt')}
          </button>
          <button className="btn-primary" onClick={() => setActiveForm('transfer')}>
            🔄 {t('inventory.actions.transfer')}
          </button>
          <button onClick={() => setActiveForm('shipment')} style={{ background: 'var(--info)', color: 'white', borderColor: 'var(--info)' }}>
            📤 {t('inventory.actions.shipment')}
          </button>
          <button onClick={() => setActiveForm('cyclecount')} style={{ background: 'var(--warning)', color: 'white', borderColor: 'var(--warning)' }}>
            📊 {t('inventory.actions.cycleCount')}
          </button>
          <button onClick={() => setActiveForm('adjustment')}>
            ⚙️ {t('inventory.actions.adjustment')}
          </button>
          <button onClick={() => setActiveForm('qualitychange')} style={{ background: 'var(--danger)', color: 'white', borderColor: 'var(--danger)' }}>
            🔒 {t('inventory.actions.qualityStatus')}
          </button>
        </div>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('inventory.columns.itemCode')}</th>
              <th>{t('inventory.columns.itemName')}</th>
              <th>{t('inventory.columns.location')}</th>
              <th>{t('inventory.columns.batch')}</th>
              <th>{t('inventory.columns.mrn')}</th>
              <th>{t('inventory.columns.quantity')}</th>
              <th>{t('inventory.columns.qualityStatus')}</th>
              <th>{t('common.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {inventory.map((inv, idx) => (
              <tr key={idx}>
                <td>{inv.item?.code}</td>
                <td>{inv.item?.name}</td>
                <td>{inv.location?.name}</td>
                <td>{inv.batchNumber || '-'}</td>
                <td>{inv.mrn || '-'}</td>
                <td>{inv.quantity.toFixed(2)} {inv.uoM?.code}</td>
                <td>
                  <span className={`badge badge-${
                    inv.qualityStatus === 1 ? 'success' :
                    inv.qualityStatus === 2 ? 'danger' : 'warning'
                  }`}>
                    {inv.qualityStatus === 1 ? t('qualityStatus.ok') :
                     inv.qualityStatus === 2 ? t('qualityStatus.blocked') : t('qualityStatus.quarantine')}
                  </span>
                </td>
                <td>
                  {inv.batchNumber && inv.quantity > 0 && (
                    <button
                      onClick={() => setMoveBatchRow(inv)}
                      style={{ fontSize: 12, padding: '2px 8px' }}
                    >
                      🔀 {t('moveBatch.button')}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {moveBatchRow && (
        <MoveBatchModal
          defaultBatchNumber={moveBatchRow.batchNumber}
          defaultWarehouseId={moveBatchRow.location?.warehouseId}
          onCancel={() => setMoveBatchRow(null)}
          onSuccess={(summary) => {
            setMoveBatchRow(null);
            toast.success(
              t('moveBatch.success', {
                count: summary.balancesMoved,
                qty: summary.totalQty,
              })
            );
            loadInventory();
          }}
        />
      )}
    </div>
  );
};

export default Inventory;
