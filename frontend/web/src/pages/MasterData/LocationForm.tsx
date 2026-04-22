import React, { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { locationsApi, warehousesApi } from '../../services/masterDataApi';
import type { Location, LocationFormData, Warehouse } from '../../types/masterData';
import { LocationType } from '../../types/masterData';
import '../../styles/Form.css';

const LocationForm: React.FC = () => {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const isEditMode = !!id && id !== 'new';

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [location, setLocation] = useState<Location | null>(null);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [allLocations, setAllLocations] = useState<Location[]>([]);

  const [formData, setFormData] = useState<LocationFormData>({
    warehouseId: '',
    code: '',
    name: '',
    locationType: LocationType.Storage,
    parentLocationId: undefined,
    isActive: true,
  });

  const [errors, setErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    loadInitialData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  const loadInitialData = async () => {
    try {
      setLoading(true);

      // Load warehouses and all locations
      const [warehousesRes, locationsRes] = await Promise.all([
        warehousesApi.getAll(),
        locationsApi.getAll(),
      ]);

      setWarehouses(warehousesRes.data.filter((w) => w.isActive));
      setAllLocations(locationsRes.data);

      // If edit mode, load the specific location
      if (isEditMode) {
        const response = await locationsApi.getById(id!);
        const data = response.data;
        setLocation(data);
        setFormData({
          warehouseId: data.warehouseId,
          code: data.code,
          name: data.name,
          locationType: data.locationType,
          parentLocationId: data.parentLocationId || undefined,
          isActive: data.isActive,
        });
      } else if (warehousesRes.data.length > 0) {
        // Auto-select first warehouse for new location
        setFormData((prev) => ({
          ...prev,
          warehouseId: warehousesRes.data.find((w) => w.isActive)?.id || '',
        }));
      }
    } catch (error) {
      console.error('Error loading data:', error);
      alert('Грешка при вчитување на податоци');
      if (isEditMode) {
        navigate('/master-data/locations');
      }
    } finally {
      setLoading(false);
    }
  };

  const handleChange = (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>
  ) => {
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

  const handleLocationTypeChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const newType = Number(e.target.value) as LocationType;
    setFormData((prev) => ({
      ...prev,
      locationType: newType,
    }));

    if (errors.locationType) {
      setErrors((prev) => {
        const newErrors = { ...prev };
        delete newErrors.locationType;
        return newErrors;
      });
    }
  };

  const getLocationTypeName = (type: LocationType): string => {
    const typeNames: Record<LocationType, string> = {
      [LocationType.Receiving]: 'Приемна',
      [LocationType.Storage]: 'Складиште',
      [LocationType.Picking]: 'Пикинг',
      [LocationType.Production]: 'Производство',
      [LocationType.Shipping]: 'Испорака',
      [LocationType.Quarantine]: 'Карантин',
      [LocationType.Blocked]: 'Блокирана',
    };
    return typeNames[type] || 'Непознато';
  };

  const getLocationTypeIcon = (type: LocationType): string => {
    const icons: Record<LocationType, string> = {
      [LocationType.Receiving]: '📥',
      [LocationType.Storage]: '📦',
      [LocationType.Picking]: '🎯',
      [LocationType.Production]: '⚙️',
      [LocationType.Shipping]: '🚚',
      [LocationType.Quarantine]: '⚠️',
      [LocationType.Blocked]: '🔒',
    };
    return icons[type] || '📍';
  };

  const validate = (): boolean => {
    const newErrors: Record<string, string> = {};

    if (!formData.warehouseId) {
      newErrors.warehouseId = 'Магацинот е задолжителен';
    }

    if (!formData.code.trim()) {
      newErrors.code = 'Кодот е задолжителен';
    } else if (formData.code.length > 50) {
      newErrors.code = 'Кодот не може да биде подолг од 50 карактери';
    }

    if (!formData.name.trim()) {
      newErrors.name = 'Називот е задолжителен';
    } else if (formData.name.length > 100) {
      newErrors.name = 'Називот не може да биде подолг од 100 карактери';
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

      // Prepare data - convert empty string to undefined for parentLocationId
      const dataToSend = {
        ...formData,
        parentLocationId: formData.parentLocationId || undefined,
      };

      if (isEditMode) {
        await locationsApi.update(id!, dataToSend);
        alert('Локацијата е успешно ажурирана');
      } else {
        await locationsApi.create(dataToSend);
        alert('Локацијата е успешно креирана');
      }

      navigate('/master-data/locations');
    } catch (error: any) {
      console.error('Error saving location:', error);
      const errorMessage =
        error.response?.data?.message || error.message || 'Непозната грешка';
      alert(`Грешка при зачувување на локација:\n${errorMessage}`);
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = () => {
    if (window.confirm('Дали сте сигурни? Незачуваните промени ќе бидат изгубени.')) {
      navigate('/master-data/locations');
    }
  };

  // Generate automatic code suggestion
  const generateCode = () => {
    if (!formData.warehouseId) {
      alert('Прво изберете магацин');
      return;
    }

    const warehouse = warehouses.find((w) => w.id === formData.warehouseId);
    if (!warehouse) return;

    const whCode = warehouse.code;
    const typeCode = getLocationTypeCode(formData.locationType);
    const existingLocations = allLocations.filter(
      (l) => l.warehouseId === formData.warehouseId && l.locationType === formData.locationType
    );
    const nextNumber = (existingLocations.length + 1).toString().padStart(3, '0');

    const suggestedCode = `${whCode}-${typeCode}-${nextNumber}`;
    setFormData((prev) => ({ ...prev, code: suggestedCode }));
  };

  const getLocationTypeCode = (type: LocationType): string => {
    const codes: Record<LocationType, string> = {
      [LocationType.Receiving]: 'RCV',
      [LocationType.Storage]: 'STG',
      [LocationType.Picking]: 'PCK',
      [LocationType.Production]: 'PRD',
      [LocationType.Shipping]: 'SHP',
      [LocationType.Quarantine]: 'QTN',
      [LocationType.Blocked]: 'BLK',
    };
    return codes[type] || 'LOC';
  };

  // Get available parent locations (same warehouse, different type)
  const availableParentLocations = allLocations.filter(
    (l) =>
      l.warehouseId === formData.warehouseId &&
      l.isActive &&
      l.id !== id // Don't allow self as parent
  );

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

  if (warehouses.length === 0) {
    return (
      <div className="page-container">
        <div className="card" style={{ textAlign: 'center', padding: '40px' }}>
          <div style={{ fontSize: '64px', marginBottom: '20px' }}>⚠️</div>
          <h3>Нема Активни Магацини</h3>
          <p>Прво треба да креирате магацин пред да можете да креирате локација.</p>
          <div style={{ marginTop: '20px' }}>
            <button className="btn btn-primary" onClick={() => navigate('/master-data/warehouses/new')}>
              ➕ Креирај Магацин
            </button>
            <button
              className="btn btn-secondary"
              style={{ marginLeft: '10px' }}
              onClick={() => navigate('/master-data/locations')}
            >
              ❌ Назад
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="page-container">
      <div className="page-header">
        <div>
          <h2>{isEditMode ? '✏️ Измени Локација' : '➕ Нова Локација'}</h2>
          <p>{isEditMode ? `Локација: ${location?.code}` : 'Креирање нова локација'}</p>
        </div>
        <button className="btn btn-secondary" onClick={handleCancel}>
          ❌ Откажи
        </button>
      </div>

      <form onSubmit={handleSubmit}>
        {/* Basic Information */}
        <div className="card">
          <h4 style={{ marginBottom: '25px', color: '#2196F3' }}>📋 Основни Информации</h4>

          {/* Warehouse Selection */}
          <div className="form-group">
            <label className="required">
              Магацин <span style={{ color: 'red' }}>*</span>
            </label>
            <select
              name="warehouseId"
              value={formData.warehouseId}
              onChange={handleChange}
              className={errors.warehouseId ? 'error' : ''}
              disabled={isEditMode} // Cannot change warehouse in edit mode
              required
            >
              <option value="">-- Избери Магацин --</option>
              {warehouses.map((wh) => (
                <option key={wh.id} value={wh.id}>
                  {wh.code} - {wh.name}
                </option>
              ))}
            </select>
            {errors.warehouseId && <span className="error-message">{errors.warehouseId}</span>}
            {isEditMode && (
              <small style={{ color: '#666', fontSize: '12px' }}>
                Магацинот не може да се менува
              </small>
            )}
          </div>

          <div className="form-grid">
            {/* Code */}
            <div className="form-group">
              <label className="required">
                Код <span style={{ color: 'red' }}>*</span>
              </label>
              <div style={{ display: 'flex', gap: '10px' }}>
                <input
                  type="text"
                  name="code"
                  value={formData.code}
                  onChange={handleChange}
                  className={errors.code ? 'error' : ''}
                  placeholder="WH-STG-001"
                  maxLength={50}
                  disabled={isEditMode}
                  required
                  style={{ flex: 1 }}
                />
                {!isEditMode && (
                  <button
                    type="button"
                    className="btn btn-secondary"
                    onClick={generateCode}
                    title="Генерирај автоматски код"
                  >
                    🔄 Генерирај
                  </button>
                )}
              </div>
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
                placeholder="Складишна Локација А1"
                maxLength={100}
                required
              />
              {errors.name && <span className="error-message">{errors.name}</span>}
            </div>
          </div>
        </div>

        {/* Location Type and Hierarchy */}
        <div className="card">
          <h4 style={{ marginBottom: '25px', color: '#2196F3' }}>📍 Тип и Хиерархија</h4>

          <div className="form-grid">
            {/* Location Type */}
            <div className="form-group">
              <label className="required">
                Тип на Локација <span style={{ color: 'red' }}>*</span>
              </label>
              <select
                name="locationType"
                value={formData.locationType}
                onChange={handleLocationTypeChange}
                className={errors.locationType ? 'error' : ''}
                required
              >
                {Object.entries(LocationType)
                  .filter(([key]) => !isNaN(Number(key)))
                  .map(([key, value]) => (
                    <option key={key} value={key}>
                      {getLocationTypeIcon(Number(key) as LocationType)}{' '}
                      {getLocationTypeName(Number(key) as LocationType)}
                    </option>
                  ))}
              </select>
              {errors.locationType && <span className="error-message">{errors.locationType}</span>}
            </div>

            {/* Parent Location */}
            <div className="form-group">
              <label>Родител Локација (опционално)</label>
              <select
                name="parentLocationId"
                value={formData.parentLocationId || ''}
                onChange={handleChange}
              >
                <option value="">-- Нема Родител (Root) --</option>
                {availableParentLocations.map((loc) => (
                  <option key={loc.id} value={loc.id}>
                    {loc.code} - {loc.name} ({getLocationTypeName(loc.locationType)})
                  </option>
                ))}
              </select>
              <small style={{ color: '#666', fontSize: '12px' }}>
                Користете за креирање хиерархија (Zone → Aisle → Rack → Bin)
              </small>
            </div>
          </div>

          {/* Location Type Info Box */}
          <div
            style={{
              marginTop: '20px',
              padding: '15px',
              backgroundColor: '#e3f2fd',
              borderRadius: '8px',
              border: '2px solid #2196F3',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '15px' }}>
              <div style={{ fontSize: '48px' }}>
                {getLocationTypeIcon(formData.locationType)}
              </div>
              <div>
                <h5 style={{ margin: 0, color: '#1976d2' }}>
                  {getLocationTypeName(formData.locationType)}
                </h5>
                <p style={{ margin: '5px 0 0 0', fontSize: '14px', color: '#555' }}>
                  {formData.locationType === LocationType.Receiving &&
                    'Локација за прием на материјали од добавувачи'}
                  {formData.locationType === LocationType.Storage &&
                    'Општа локација за долгорочно складирање'}
                  {formData.locationType === LocationType.Picking &&
                    'Локација оптимизирана за picking операции'}
                  {formData.locationType === LocationType.Production &&
                    'Локација во производствен простор'}
                  {formData.locationType === LocationType.Shipping &&
                    'Локација за подготовка на испратки'}
                  {formData.locationType === LocationType.Quarantine &&
                    'Локација за материјали во карантин (контрола на квалитет)'}
                  {formData.locationType === LocationType.Blocked &&
                    'Блокирана локација (не може да се користи)'}
                </p>
              </div>
            </div>
          </div>
        </div>

        {/* Status */}
        <div className="card">
          <h4 style={{ marginBottom: '20px', color: '#2196F3' }}>⚙️ Статус</h4>

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
                {formData.isActive ? '✅ Активна' : '🔴 Неактивна'}
              </span>
            </label>
            <small style={{ color: '#666', fontSize: '12px', marginLeft: '30px' }}>
              {formData.isActive
                ? 'Локацијата е активна и може да прима инвентар'
                : 'Локацијата е неактивна и нема да биде достапна за трансакции'}
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
              {saving
                ? '💾 Зачувување...'
                : isEditMode
                ? '💾 Зачувај Промени'
                : '➕ Креирај Локација'}
            </button>
          </div>
        </div>

        {/* Metadata (Edit Mode Only) */}
        {isEditMode && location && (
          <div className="card" style={{ backgroundColor: '#e3f2fd' }}>
            <h5 style={{ marginBottom: '15px', color: '#1976d2' }}>ℹ️ Метаподатоци</h5>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '15px' }}>
              <div>
                <strong>Креирано:</strong>{' '}
                {location.createdAt ? new Date(location.createdAt).toLocaleString('mk-MK') : '-'}
              </div>
              <div>
                <strong>Креирал:</strong> {location.createdBy || '-'}
              </div>
              <div>
                <strong>Последна Измена:</strong>{' '}
                {location.updatedAt ? new Date(location.updatedAt).toLocaleString('mk-MK') : '-'}
              </div>
              <div>
                <strong>Изменил:</strong> {location.updatedBy || '-'}
              </div>
            </div>
          </div>
        )}
      </form>
    </div>
  );
};

export default LocationForm;
