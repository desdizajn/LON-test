import React, { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { warehousesApi } from '../../services/masterDataApi';
import type { Warehouse, WarehouseFormData } from '../../types/masterData';
import '../../styles/Form.css';

const WarehouseForm: React.FC = () => {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const isEditMode = !!id && id !== 'new';

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [warehouse, setWarehouse] = useState<Warehouse | null>(null);

  const [formData, setFormData] = useState<WarehouseFormData>({
    code: '',
    name: '',
    description: '',
    address: '',
    isActive: true,
  });

  const [errors, setErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (isEditMode) {
      loadWarehouse();
    }
  }, [id]);

  const loadWarehouse = async () => {
    try {
      setLoading(true);
      const response = await warehousesApi.getById(id!);
      const data = response.data;
      setWarehouse(data);
      setFormData({
        code: data.code,
        name: data.name,
        description: data.description || '',
        address: data.address || '',
        isActive: data.isActive,
      });
    } catch (error) {
      console.error('Error loading warehouse:', error);
      alert('Грешка при вчитување на магацин');
      navigate('/master-data/warehouses');
    } finally {
      setLoading(false);
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const { name, value, type } = e.target;
    const checked = (e.target as HTMLInputElement).checked;

    setFormData((prev) => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value,
    }));

    // Clear error for this field
    if (errors[name]) {
      setErrors((prev) => {
        const newErrors = { ...prev };
        delete newErrors[name];
        return newErrors;
      });
    }
  };

  const validate = (): boolean => {
    const newErrors: Record<string, string> = {};

    if (!formData.code.trim()) {
      newErrors.code = 'Кодот е задолжителен';
    } else if (formData.code.length > 20) {
      newErrors.code = 'Кодот не може да биде подолг од 20 карактери';
    }

    if (!formData.name.trim()) {
      newErrors.name = 'Називот е задолжителен';
    } else if (formData.name.length > 100) {
      newErrors.name = 'Називот не може да биде подолг од 100 карактери';
    }

    if (formData.address && formData.address.length > 200) {
      newErrors.address = 'Адресата не може да биде подолга од 200 карактери';
    }

    if (formData.description && formData.description.length > 500) {
      newErrors.description = 'Описот не може да биде подолг од 500 карактери';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!validate()) {
      alert('Ве молиме пополнете ги задолжителните полиња');
      return;
    }

    try {
      setSaving(true);

      if (isEditMode) {
        await warehousesApi.update(id!, formData);
        alert('Магацинот е успешно ажуриран');
      } else {
        await warehousesApi.create(formData);
        alert('Магацинот е успешно креиран');
      }

      navigate('/master-data/warehouses');
    } catch (error: any) {
      console.error('Error saving warehouse:', error);
      const errorMessage =
        error.response?.data?.message || error.message || 'Непозната грешка';
      alert(`Грешка при зачувување на магацин:\n${errorMessage}`);
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = () => {
    if (window.confirm('Дали сте сигурни? Незачуваните промени ќе бидат изгубени.')) {
      navigate('/master-data/warehouses');
    }
  };

  if (loading) {
    return (
      <div className="page-container">
        <div style={{ textAlign: 'center', padding: '60px' }}>
          <div style={{ fontSize: '64px', marginBottom: '20px' }}>⏳</div>
          <div style={{ fontSize: '20px', color: '#666' }}>Вчитување...</div>
        </div>
      </div>
    );
  }

  return (
    <div className="page-container">
      <div className="page-header">
        <div>
          <h2>{isEditMode ? '✏️ Измени Магацин' : '➕ Нов Магацин'}</h2>
          <p>{isEditMode ? `Магацин: ${warehouse?.name}` : 'Креирање нов магацин'}</p>
        </div>
        <button className="btn btn-secondary" onClick={handleCancel}>
          ❌ Откажи
        </button>
      </div>

      <form onSubmit={handleSubmit}>
        <div className="card">
          <h4 style={{ marginBottom: '25px', color: '#2196F3' }}>📋 Основни Информации</h4>

          <div className="form-grid">
            {/* Code */}
            <div className="form-group">
              <label className="required">
                Код <span style={{ color: 'red' }}>*</span>
              </label>
              <input
                type="text"
                name="code"
                value={formData.code}
                onChange={handleChange}
                className={errors.code ? 'error' : ''}
                placeholder="WH-001"
                maxLength={20}
                disabled={isEditMode} // Code cannot be changed in edit mode
                required
              />
              {errors.code && <span className="error-message">{errors.code}</span>}
              {isEditMode && (
                <small style={{ color: '#666', fontSize: '12px' }}>
                  Кодот не може да се менува
                </small>
              )}
            </div>

            {/* Name */}
            <div className="form-group">
              <label className="required">
                Назив <span style={{ color: 'red' }}>*</span>
              </label>
              <input
                type="text"
                name="name"
                value={formData.name}
                onChange={handleChange}
                className={errors.name ? 'error' : ''}
                placeholder="Главен Магацин"
                maxLength={100}
                required
              />
              {errors.name && <span className="error-message">{errors.name}</span>}
            </div>
          </div>

          {/* Address */}
          <div className="form-group">
            <label>Адреса</label>
            <input
              type="text"
              name="address"
              value={formData.address}
              onChange={handleChange}
              className={errors.address ? 'error' : ''}
              placeholder="ул. Индустриска бр. 10, Скопје"
              maxLength={200}
            />
            {errors.address && <span className="error-message">{errors.address}</span>}
          </div>

          {/* Description */}
          <div className="form-group">
            <label>Опис</label>
            <textarea
              name="description"
              value={formData.description}
              onChange={handleChange}
              className={errors.description ? 'error' : ''}
              placeholder="Додатни информации за магацинот..."
              maxLength={500}
              rows={4}
            />
            {errors.description && <span className="error-message">{errors.description}</span>}
            <small style={{ color: '#666', fontSize: '12px' }}>
              {(formData.description || '').length}/500 карактери
            </small>
          </div>

          {/* Is Active */}
          <div className="form-group">
            <label style={{ display: 'flex', alignItems: 'center', gap: '10px', cursor: 'pointer' }}>
              <input
                type="checkbox"
                name="isActive"
                checked={formData.isActive}
                onChange={handleChange}
                style={{ width: '20px', height: '20px', cursor: 'pointer' }}
              />
              <span style={{ fontWeight: 'bold' }}>
                {formData.isActive ? '✅ Активен' : '🔴 Неактивен'}
              </span>
            </label>
            <small style={{ color: '#666', fontSize: '12px', marginLeft: '30px' }}>
              {formData.isActive
                ? 'Магацинот е активен и може да се користи'
                : 'Магацинот е неактивен и нема да биде достапен за трансакции'}
            </small>
          </div>
        </div>

        {/* Action Buttons */}
        <div className="card" style={{ backgroundColor: '#f5f5f5' }}>
          <div style={{ display: 'flex', gap: '15px', justifyContent: 'flex-end' }}>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={handleCancel}
              disabled={saving}
            >
              ❌ Откажи
            </button>
            <button type="submit" className="btn btn-primary" disabled={saving}>
              {saving ? '💾 Зачувување...' : isEditMode ? '💾 Зачувај Промени' : '➕ Креирај Магацин'}
            </button>
          </div>
        </div>

        {/* Metadata (Edit Mode Only) */}
        {isEditMode && warehouse && (
          <div className="card" style={{ backgroundColor: '#e3f2fd' }}>
            <h5 style={{ marginBottom: '15px', color: '#1976d2' }}>ℹ️ Метаподатоци</h5>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '15px' }}>
              <div>
                <strong>Креирано:</strong>{' '}
                {warehouse.createdAt
                  ? new Date(warehouse.createdAt).toLocaleString('mk-MK')
                  : '-'}
              </div>
              <div>
                <strong>Креирал:</strong> {warehouse.createdBy || '-'}
              </div>
              <div>
                <strong>Последна Измена:</strong>{' '}
                {warehouse.updatedAt
                  ? new Date(warehouse.updatedAt).toLocaleString('mk-MK')
                  : '-'}
              </div>
              <div>
                <strong>Изменил:</strong> {warehouse.updatedBy || '-'}
              </div>
            </div>
          </div>
        )}
      </form>
    </div>
  );
};

export default WarehouseForm;
