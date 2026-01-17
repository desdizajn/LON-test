import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { warehousesApi } from '../../services/masterDataApi';
import type { Warehouse } from '../../types/masterData';
import '../../styles/List.css';

const WarehouseList: React.FC = () => {
  const navigate = useNavigate();
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [loading, setLoading] = useState(true);
  const [filterActive, setFilterActive] = useState<string>('all'); // 'all' | 'active' | 'inactive'

  useEffect(() => {
    loadWarehouses();
  }, []);

  const loadWarehouses = async () => {
    try {
      setLoading(true);
      const response = await warehousesApi.getAll();
      setWarehouses(response.data);
    } catch (error) {
      console.error('Error loading warehouses:', error);
      alert('Грешка при вчитување на магацини');
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (id: string, name: string) => {
    if (!window.confirm(`Дали сте сигурни дека сакате да го избришете магацинот "${name}"?`)) {
      return;
    }

    try {
      await warehousesApi.delete(id);
      alert('Магацинот е успешно избришан');
      loadWarehouses();
    } catch (error) {
      console.error('Error deleting warehouse:', error);
      alert('Грешка при бришење на магацин. Можеби има поврзани локации.');
    }
  };

  const filteredWarehouses = warehouses.filter((w) => {
    if (filterActive === 'active') return w.isActive;
    if (filterActive === 'inactive') return !w.isActive;
    return true; // 'all'
  });

  return (
    <div className="page-container">
      <div className="page-header">
        <div>
          <h2>📦 Магацини</h2>
          <p>Преглед и управување со магацини</p>
        </div>
        <button className="btn btn-primary" onClick={() => navigate('/master-data/warehouses/new')}>
          ➕ Нов Магацин
        </button>
      </div>

      {/* Summary Cards */}
      <div className="cards-grid" style={{ gridTemplateColumns: 'repeat(3, 1fr)' }}>
        <div className="card">
          <h5>📊 Вкупно Магацини</h5>
          <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#2196F3' }}>
            {warehouses.length}
          </div>
        </div>

        <div className="card">
          <h5>✅ Активни</h5>
          <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#4CAF50' }}>
            {warehouses.filter((w) => w.isActive).length}
          </div>
        </div>

        <div className="card">
          <h5>🔴 Неактивни</h5>
          <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#f44336' }}>
            {warehouses.filter((w) => !w.isActive).length}
          </div>
        </div>
      </div>

      {/* Filters */}
      <div className="card">
        <div style={{ display: 'flex', gap: '15px', alignItems: 'center', flexWrap: 'wrap' }}>
          <h5 style={{ margin: 0 }}>🔍 Филтри:</h5>

          <div style={{ display: 'flex', gap: '10px' }}>
            <button
              className={`btn ${filterActive === 'all' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setFilterActive('all')}
            >
              Сите ({warehouses.length})
            </button>
            <button
              className={`btn ${filterActive === 'active' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setFilterActive('active')}
            >
              ✅ Активни ({warehouses.filter((w) => w.isActive).length})
            </button>
            <button
              className={`btn ${filterActive === 'inactive' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setFilterActive('inactive')}
            >
              🔴 Неактивни ({warehouses.filter((w) => !w.isActive).length})
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
        ) : filteredWarehouses.length === 0 ? (
          <div style={{ textAlign: 'center', padding: '40px' }}>
            <div style={{ fontSize: '64px', marginBottom: '15px' }}>📭</div>
            <div style={{ fontSize: '18px', color: '#666', marginBottom: '10px' }}>
              Нема магацини
            </div>
            <button className="btn btn-primary" onClick={() => navigate('/master-data/warehouses/new')}>
              ➕ Креирај Прв Магацин
            </button>
          </div>
        ) : (
          <div className="table-container">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Код</th>
                  <th>Назив</th>
                  <th>Адреса</th>
                  <th>Статус</th>
                  <th>Креирано</th>
                  <th>Креирал</th>
                  <th style={{ width: '150px' }}>Акции</th>
                </tr>
              </thead>
              <tbody>
                {filteredWarehouses.map((warehouse) => (
                  <tr
                    key={warehouse.id}
                    style={{
                      backgroundColor: !warehouse.isActive ? '#ffebee' : undefined,
                    }}
                  >
                    <td>
                      <strong>{warehouse.code}</strong>
                    </td>
                    <td>{warehouse.name}</td>
                    <td>{warehouse.address || '-'}</td>
                    <td>
                      <span
                        style={{
                          padding: '4px 12px',
                          borderRadius: '12px',
                          fontSize: '12px',
                          fontWeight: 'bold',
                          backgroundColor: warehouse.isActive ? '#4CAF50' : '#f44336',
                          color: 'white',
                        }}
                      >
                        {warehouse.isActive ? '✅ Активен' : '🔴 Неактивен'}
                      </span>
                    </td>
                    <td>
                      {warehouse.createdAt
                        ? new Date(warehouse.createdAt).toLocaleDateString('mk-MK', {
                            year: 'numeric',
                            month: 'short',
                            day: 'numeric',
                          })
                        : '-'}
                    </td>
                    <td>{warehouse.createdBy || '-'}</td>
                    <td>
                      <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end' }}>
                        <button
                          className="btn btn-sm btn-secondary"
                          onClick={() => navigate(`/master-data/warehouses/${warehouse.id}`)}
                          title="Измени"
                        >
                          ✏️
                        </button>
                        <button
                          className="btn btn-sm btn-danger"
                          onClick={() => handleDelete(warehouse.id, warehouse.name)}
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
      {filteredWarehouses.length > 0 && (
        <div className="card" style={{ backgroundColor: '#f5f5f5' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <div>
              <strong>Прикажани:</strong> {filteredWarehouses.length} магацин
              {filteredWarehouses.length !== 1 ? 'и' : ''}
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

export default WarehouseList;
