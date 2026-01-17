import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { locationsApi, warehousesApi } from '../../services/masterDataApi';
import type { Location, Warehouse } from '../../types/masterData';
import { LocationType } from '../../types/masterData';
import '../../styles/List.css';

const LocationList: React.FC = () => {
  const navigate = useNavigate();
  const [locations, setLocations] = useState<Location[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [loading, setLoading] = useState(true);

  // Filters
  const [filterWarehouse, setFilterWarehouse] = useState<string>('all');
  const [filterType, setFilterType] = useState<string>('all');
  const [filterActive, setFilterActive] = useState<string>('all'); // 'all' | 'active' | 'inactive'
  const [searchTerm, setSearchTerm] = useState('');

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [locationsRes, warehousesRes] = await Promise.all([
        locationsApi.getAll(),
        warehousesApi.getAll(),
      ]);
      setLocations(locationsRes.data);
      setWarehouses(warehousesRes.data);
    } catch (error) {
      console.error('Error loading data:', error);
      alert('Грешка при вчитување на податоци');
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (id: string, code: string) => {
    if (!window.confirm(`Дали сте сигурни дека сакате да ја избришете локацијата "${code}"?`)) {
      return;
    }

    try {
      await locationsApi.delete(id);
      alert('Локацијата е успешно избришана');
      loadData();
    } catch (error) {
      console.error('Error deleting location:', error);
      alert('Грешка при бришење на локација. Можеби има поврзан инвентар.');
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

  const getWarehouseName = (warehouseId: string): string => {
    const warehouse = warehouses.find((w) => w.id === warehouseId);
    return warehouse ? warehouse.name : 'N/A';
  };

  // Apply filters
  const filteredLocations = locations.filter((location) => {
    // Warehouse filter
    if (filterWarehouse !== 'all' && location.warehouseId !== filterWarehouse) {
      return false;
    }

    // Type filter
    if (filterType !== 'all' && location.locationType.toString() !== filterType) {
      return false;
    }

    // Active filter
    if (filterActive === 'active' && !location.isActive) return false;
    if (filterActive === 'inactive' && location.isActive) return false;

    // Search filter
    if (searchTerm) {
      const search = searchTerm.toLowerCase();
      return (
        location.code.toLowerCase().includes(search) ||
        location.name.toLowerCase().includes(search)
      );
    }

    return true;
  });

  // Group by warehouse for summary
  const locationsByWarehouse = locations.reduce((acc, loc) => {
    const whName = getWarehouseName(loc.warehouseId);
    if (!acc[whName]) acc[whName] = 0;
    acc[whName]++;
    return acc;
  }, {} as Record<string, number>);

  return (
    <div className="page-container">
      <div className="page-header">
        <div>
          <h2>📍 Локации</h2>
          <p>Преглед и управување со локации во магацините</p>
        </div>
        <button className="btn btn-primary" onClick={() => navigate('/master-data/locations/new')}>
          ➕ Нова Локација
        </button>
      </div>

      {/* Summary Cards */}
      <div className="cards-grid" style={{ gridTemplateColumns: 'repeat(4, 1fr)' }}>
        <div className="card">
          <h5>📊 Вкупно Локации</h5>
          <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#2196F3' }}>
            {locations.length}
          </div>
        </div>

        <div className="card">
          <h5>📦 Магацини</h5>
          <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#9C27B0' }}>
            {warehouses.length}
          </div>
        </div>

        <div className="card">
          <h5>✅ Активни</h5>
          <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#4CAF50' }}>
            {locations.filter((l) => l.isActive).length}
          </div>
        </div>

        <div className="card">
          <h5>🔴 Неактивни</h5>
          <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#f44336' }}>
            {locations.filter((l) => !l.isActive).length}
          </div>
        </div>
      </div>

      {/* Locations by Warehouse Breakdown */}
      {warehouses.length > 0 && (
        <div className="card">
          <h5 style={{ marginBottom: '15px' }}>📦 Локации по Магацин</h5>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))', gap: '15px' }}>
            {warehouses.map((wh) => {
              const count = locationsByWarehouse[wh.name] || 0;
              return (
                <div
                  key={wh.id}
                  style={{
                    padding: '12px',
                    backgroundColor: '#f5f5f5',
                    borderRadius: '8px',
                    border: '2px solid #e0e0e0',
                  }}
                >
                  <div style={{ fontSize: '12px', color: '#666', marginBottom: '5px' }}>
                    {wh.code}
                  </div>
                  <div style={{ fontWeight: 'bold', marginBottom: '5px' }}>{wh.name}</div>
                  <div style={{ fontSize: '24px', fontWeight: 'bold', color: '#2196F3' }}>
                    {count} <span style={{ fontSize: '14px', color: '#666' }}>локации</span>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Filters */}
      <div className="card">
        <h5 style={{ marginBottom: '15px' }}>🔍 Филтри и Пребарување</h5>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '15px' }}>
          {/* Search */}
          <div>
            <input
              type="text"
              placeholder="🔍 Пребарај по код или назив..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              style={{
                width: '100%',
                padding: '10px',
                fontSize: '16px',
                border: '2px solid #ddd',
                borderRadius: '8px',
              }}
            />
          </div>

          {/* Filter Buttons Row 1: Warehouse */}
          <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap', alignItems: 'center' }}>
            <strong style={{ minWidth: '100px' }}>Магацин:</strong>
            <button
              className={`btn ${filterWarehouse === 'all' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setFilterWarehouse('all')}
            >
              Сите ({locations.length})
            </button>
            {warehouses.map((wh) => (
              <button
                key={wh.id}
                className={`btn ${filterWarehouse === wh.id ? 'btn-primary' : 'btn-secondary'}`}
                onClick={() => setFilterWarehouse(wh.id)}
              >
                {wh.code} ({locationsByWarehouse[wh.name] || 0})
              </button>
            ))}
          </div>

          {/* Filter Buttons Row 2: Type */}
          <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap', alignItems: 'center' }}>
            <strong style={{ minWidth: '100px' }}>Тип:</strong>
            <button
              className={`btn ${filterType === 'all' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setFilterType('all')}
            >
              Сите ({locations.length})
            </button>
            {Object.entries(LocationType)
              .filter(([key]) => isNaN(Number(key)))
              .map(([key, value]) => {
                const count = locations.filter((l) => l.locationType === value).length;
                return (
                  <button
                    key={value}
                    className={`btn ${filterType === value.toString() ? 'btn-primary' : 'btn-secondary'}`}
                    onClick={() => setFilterType(value.toString())}
                  >
                    {getLocationTypeName(value as LocationType)} ({count})
                  </button>
                );
              })}
          </div>

          {/* Filter Buttons Row 3: Active Status */}
          <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap', alignItems: 'center' }}>
            <strong style={{ minWidth: '100px' }}>Статус:</strong>
            <button
              className={`btn ${filterActive === 'all' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setFilterActive('all')}
            >
              Сите ({locations.length})
            </button>
            <button
              className={`btn ${filterActive === 'active' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setFilterActive('active')}
            >
              ✅ Активни ({locations.filter((l) => l.isActive).length})
            </button>
            <button
              className={`btn ${filterActive === 'inactive' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setFilterActive('inactive')}
            >
              🔴 Неактивни ({locations.filter((l) => !l.isActive).length})
            </button>
          </div>
        </div>
      </div>

      {/* Table */}
      <div className="card">
        {loading ? (
          <div style={{ textAlign: 'center', padding: '40px' }}>
            <div style={{ fontSize: '48px', marginBottom: '15px' }}>⏳</div>
            <div style={{ fontSize: '18px', color: '#666' }}>Вчитување...</div>
          </div>
        ) : filteredLocations.length === 0 ? (
          <div style={{ textAlign: 'center', padding: '40px' }}>
            <div style={{ fontSize: '64px', marginBottom: '15px' }}>📭</div>
            <div style={{ fontSize: '18px', color: '#666', marginBottom: '10px' }}>
              {searchTerm || filterWarehouse !== 'all' || filterType !== 'all' || filterActive !== 'all'
                ? 'Нема резултати за избраните филтри'
                : 'Нема локации'}
            </div>
            {searchTerm || filterWarehouse !== 'all' || filterType !== 'all' || filterActive !== 'all' ? (
              <button
                className="btn btn-secondary"
                onClick={() => {
                  setSearchTerm('');
                  setFilterWarehouse('all');
                  setFilterType('all');
                  setFilterActive('all');
                }}
              >
                🔄 Ресетирај Филтри
              </button>
            ) : (
              <button className="btn btn-primary" onClick={() => navigate('/master-data/locations/new')}>
                ➕ Креирај Прва Локација
              </button>
            )}
          </div>
        ) : (
          <div className="table-container">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Код</th>
                  <th>Назив</th>
                  <th>Магацин</th>
                  <th>Тип</th>
                  <th>Позиција (А/Р/П/Б)</th>
                  <th>Статус</th>
                  <th>Креирано</th>
                  <th style={{ width: '150px' }}>Акции</th>
                </tr>
              </thead>
              <tbody>
                {filteredLocations.map((location) => (
                  <tr
                    key={location.id}
                    style={{
                      backgroundColor: !location.isActive ? '#ffebee' : undefined,
                    }}
                  >
                    <td>
                      <strong>{location.code}</strong>
                    </td>
                    <td>{location.name}</td>
                    <td>
                      <div style={{ fontSize: '12px', color: '#666' }}>
                        {warehouses.find((w) => w.id === location.warehouseId)?.code || 'N/A'}
                      </div>
                      <div>{getWarehouseName(location.warehouseId)}</div>
                    </td>
                    <td>
                      <span
                        style={{
                          padding: '4px 10px',
                          borderRadius: '8px',
                          fontSize: '12px',
                          fontWeight: 'bold',
                          backgroundColor: '#e3f2fd',
                          color: '#1976d2',
                        }}
                      >
                        {getLocationTypeName(location.locationType)}
                      </span>
                    </td>
                    <td>
                      {location.parentLocationId ? (
                        <div style={{ fontSize: '12px' }}>
                          {locations.find((l) => l.id === location.parentLocationId)?.code || 'N/A'}
                        </div>
                      ) : (
                        '-'
                      )}
                    </td>
                    <td>
                      <span
                        style={{
                          padding: '4px 12px',
                          borderRadius: '12px',
                          fontSize: '12px',
                          fontWeight: 'bold',
                          backgroundColor: location.isActive ? '#4CAF50' : '#f44336',
                          color: 'white',
                        }}
                      >
                        {location.isActive ? '✅ Активна' : '🔴 Неактивна'}
                      </span>
                    </td>
                    <td>
                      {location.createdAt
                        ? new Date(location.createdAt).toLocaleDateString('mk-MK', {
                            year: 'numeric',
                            month: 'short',
                            day: 'numeric',
                          })
                        : '-'}
                    </td>
                    <td>
                      <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end' }}>
                        <button
                          className="btn btn-sm btn-secondary"
                          onClick={() => navigate(`/master-data/locations/${location.id}`)}
                          title="Измени"
                        >
                          ✏️
                        </button>
                        <button
                          className="btn btn-sm btn-danger"
                          onClick={() => handleDelete(location.id, location.code)}
                          title="Избриши"
                        >
                          🗑️
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Footer Info */}
      {filteredLocations.length > 0 && (
        <div className="card" style={{ backgroundColor: '#f5f5f5' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <div>
              <strong>Прикажани:</strong> {filteredLocations.length} од {locations.length} локации
            </div>
            <div style={{ color: '#666', fontSize: '14px' }}>
              Последно освежување: {new Date().toLocaleTimeString('mk-MK')}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default LocationList;
