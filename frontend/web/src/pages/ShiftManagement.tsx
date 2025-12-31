import React, { useState, useEffect } from 'react';
import { employeeService, Shift, CreateShiftRequest, UpdateShiftRequest } from '../services/employeeService';
import './ShiftManagement.css';

interface ShiftFormData {
  name: string;
  startTime: string;
  endTime: string;
  description: string;
  isActive: boolean;
}

const ShiftManagement: React.FC = () => {
  const [shifts, setShifts] = useState<Shift[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingShift, setEditingShift] = useState<Shift | null>(null);
  const [formData, setFormData] = useState<ShiftFormData>({
    name: '',
    startTime: '08:00',
    endTime: '16:00',
    description: '',
    isActive: true
  });
  const [error, setError] = useState('');

  useEffect(() => {
    loadShifts();
  }, []);

  const loadShifts = async () => {
    try {
      setLoading(true);
      const data = await employeeService.getShifts();
      setShifts(data);
    } catch (err) {
      setError('Грешка при вчитување на смени');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleAdd = () => {
    setEditingShift(null);
    setFormData({
      name: '',
      startTime: '08:00',
      endTime: '16:00',
      description: '',
      isActive: true
    });
    setShowModal(true);
  };

  const handleEdit = (shift: Shift) => {
    setEditingShift(shift);
    setFormData({
      name: shift.name,
      startTime: shift.startTime,
      endTime: shift.endTime,
      description: shift.description || '',
      isActive: shift.isActive
    });
    setShowModal(true);
  };

  const handleSave = async () => {
    try {
      setError('');
      if (editingShift) {
        const updateData: UpdateShiftRequest = {
          name: formData.name,
          startTime: formData.startTime,
          endTime: formData.endTime,
          description: formData.description || undefined,
          isActive: formData.isActive
        };
        await employeeService.updateShift(editingShift.id, updateData);
      } else {
        const createData: CreateShiftRequest = {
          name: formData.name,
          startTime: formData.startTime,
          endTime: formData.endTime,
          description: formData.description || undefined
        };
        await employeeService.createShift(createData);
      }
      setShowModal(false);
      loadShifts();
    } catch (err: any) {
      setError(err.response?.data?.message || 'Грешка при зачувување');
    }
  };

  const handleDelete = async (shiftId: string) => {
    if (!window.confirm('Дали сте сигурни дека сакате да ја избришете смената?')) {
      return;
    }
    try {
      await employeeService.deleteShift(shiftId);
      loadShifts();
    } catch (err) {
      setError('Грешка при бришење');
    }
  };

  const calculateDuration = (start: string, end: string): string => {
    const [startHour, startMin] = start.split(':').map(Number);
    const [endHour, endMin] = end.split(':').map(Number);
    
    let hours = endHour - startHour;
    let minutes = endMin - startMin;
    
    if (minutes < 0) {
      hours--;
      minutes += 60;
    }
    
    if (hours < 0) {
      hours += 24;
    }
    
    return `${hours}ч ${minutes}м`;
  };

  if (loading) {
    return <div className="loading">Се вчитува...</div>;
  }

  return (
    <div className="shift-management">
      <div className="page-header">
        <h1>Управување со смени</h1>
        <button className="btn btn-primary" onClick={handleAdd}>
          + Додади смена
        </button>
      </div>

      {error && <div className="error-banner">{error}</div>}

      <div className="shifts-grid">
        {shifts.map(shift => (
          <div key={shift.id} className={`shift-card ${!shift.isActive ? 'inactive' : ''}`}>
            <div className="shift-header">
              <h3>{shift.name}</h3>
              <span className={`status-badge ${shift.isActive ? 'active' : 'inactive'}`}>
                {shift.isActive ? 'Активна' : 'Неактивна'}
              </span>
            </div>
            
            <div className="shift-time">
              <div className="time-block">
                <span className="time-label">Почеток:</span>
                <span className="time-value">{shift.startTime}</span>
              </div>
              <span className="time-arrow">→</span>
              <div className="time-block">
                <span className="time-label">Крај:</span>
                <span className="time-value">{shift.endTime}</span>
              </div>
            </div>

            <div className="shift-duration">
              Траење: {calculateDuration(shift.startTime, shift.endTime)}
            </div>

            {shift.description && (
              <div className="shift-description">
                {shift.description}
              </div>
            )}

            <div className="shift-actions">
              <button className="btn btn-sm btn-edit" onClick={() => handleEdit(shift)}>
                Измени
              </button>
              <button className="btn btn-sm btn-delete" onClick={() => handleDelete(shift.id)}>
                Избриши
              </button>
            </div>
          </div>
        ))}
      </div>

      {shifts.length === 0 && !loading && (
        <div className="empty-state">
          <p>Нема додадени смени. Додадете прва смена.</p>
        </div>
      )}

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>{editingShift ? 'Измени смена' : 'Додади смена'}</h2>
              <button className="modal-close" onClick={() => setShowModal(false)}>×</button>
            </div>

            <div className="modal-body">
              {error && <div className="error-message">{error}</div>}

              <div className="form-group">
                <label>Име на смена</label>
                <input
                  type="text"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  placeholder="пр. Прва смена"
                  required
                />
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label>Време на почеток</label>
                  <input
                    type="time"
                    value={formData.startTime}
                    onChange={(e) => setFormData({ ...formData, startTime: e.target.value })}
                    required
                  />
                </div>

                <div className="form-group">
                  <label>Време на крај</label>
                  <input
                    type="time"
                    value={formData.endTime}
                    onChange={(e) => setFormData({ ...formData, endTime: e.target.value })}
                    required
                  />
                </div>
              </div>

              <div className="shift-preview">
                Траење: {calculateDuration(formData.startTime, formData.endTime)}
              </div>

              <div className="form-group">
                <label>Опис</label>
                <textarea
                  value={formData.description}
                  onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                  placeholder="Опционален опис на смената..."
                  rows={3}
                />
              </div>

              {editingShift && (
                <div className="form-group">
                  <label className="checkbox-label">
                    <input
                      type="checkbox"
                      checked={formData.isActive}
                      onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                    />
                    Активна смена
                  </label>
                </div>
              )}
            </div>

            <div className="modal-footer">
              <button className="btn btn-secondary" onClick={() => setShowModal(false)}>
                Откажи
              </button>
              <button className="btn btn-primary" onClick={handleSave}>
                Зачувај
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default ShiftManagement;
