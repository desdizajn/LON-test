import React, { useEffect, useState } from 'react';
import { masterDataApi } from '../../../services/api';

interface WorkCenter {
  id: string;
  code: string;
  name: string;
  description?: string;
  standardCostPerHour: number;
  capacity: number;
  isActive: boolean;
  machinesCount?: number;
}

const WorkCenterList: React.FC = () => {
  const [workCenters, setWorkCenters] = useState<WorkCenter[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingWorkCenter, setEditingWorkCenter] = useState<WorkCenter | null>(null);
  const [formData, setFormData] = useState({
    code: '',
    name: '',
    description: '',
    standardCostPerHour: 0,
    capacity: 0,
    isActive: true
  });

  useEffect(() => {
    loadWorkCenters();
  }, []);

  const loadWorkCenters = async () => {
    try {
      setLoading(true);
      const response = await masterDataApi.getWorkCenters();
      setWorkCenters(response.data);
    } catch (err) {
      console.error('Failed to load work centers', err);
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = () => {
    setEditingWorkCenter(null);
    setFormData({
      code: '',
      name: '',
      description: '',
      standardCostPerHour: 0,
      capacity: 0,
      isActive: true
    });
    setShowModal(true);
  };

  const handleEdit = (workCenter: WorkCenter) => {
    setEditingWorkCenter(workCenter);
    setFormData({
      code: workCenter.code,
      name: workCenter.name,
      description: workCenter.description || '',
      standardCostPerHour: workCenter.standardCostPerHour,
      capacity: workCenter.capacity,
      isActive: workCenter.isActive
    });
    setShowModal(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      if (editingWorkCenter) {
        await masterDataApi.updateWorkCenter(editingWorkCenter.id, formData);
      } else {
        await masterDataApi.createWorkCenter(formData);
      }
      setShowModal(false);
      loadWorkCenters();
    } catch (err) {
      console.error('Failed to save work center', err);
      alert('Failed to save work center');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Дали сте сигурни дека сакате да го избришете работниот центар?')) return;
    try {
      await masterDataApi.deleteWorkCenter(id);
      loadWorkCenters();
    } catch (err) {
      console.error('Failed to delete work center', err);
      alert('Failed to delete work center');
    }
  };

  if (loading) return <div className="loading">Loading work centers...</div>;

  return (
    <div>
      <div className="header">
        <h2>Work Centers</h2>
        <button onClick={handleCreate} className="primary-button">
          + Нов работен центар
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>Код</th>
              <th>Име</th>
              <th>Опис</th>
              <th>Трошок/час</th>
              <th>Капацитет</th>
              <th>Машини</th>
              <th>Статус</th>
              <th>Акции</th>
            </tr>
          </thead>
          <tbody>
            {workCenters.map((wc) => (
              <tr key={wc.id}>
                <td><strong>{wc.code}</strong></td>
                <td>{wc.name}</td>
                <td>{wc.description || '-'}</td>
                <td>{wc.standardCostPerHour.toFixed(2)}</td>
                <td>{wc.capacity}</td>
                <td>{wc.machinesCount || 0}</td>
                <td>
                  <span className={`badge ${wc.isActive ? 'badge-success' : 'badge-danger'}`}>
                    {wc.isActive ? 'Активен' : 'Неактивен'}
                  </span>
                </td>
                <td>
                  <button onClick={() => handleEdit(wc)} className="btn-edit">Измени</button>
                  <button onClick={() => handleDelete(wc.id)} className="btn-delete">Избриши</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>{editingWorkCenter ? 'Измени работен центар' : 'Нов работен центар'}</h3>
              <button onClick={() => setShowModal(false)} className="close-button">×</button>
            </div>
            <form onSubmit={handleSubmit}>
              <div className="form-grid">
                <div className="form-group">
                  <label>Код *</label>
                  <input
                    type="text"
                    value={formData.code}
                    onChange={(e) => setFormData({ ...formData, code: e.target.value })}
                    required
                  />
                </div>
                <div className="form-group">
                  <label>Име *</label>
                  <input
                    type="text"
                    value={formData.name}
                    onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                    required
                  />
                </div>
                <div className="form-group full-width">
                  <label>Опис</label>
                  <textarea
                    value={formData.description}
                    onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                    rows={3}
                  />
                </div>
                <div className="form-group">
                  <label>Стандарден трошок по час</label>
                  <input
                    type="number"
                    step="0.01"
                    value={formData.standardCostPerHour}
                    onChange={(e) => setFormData({ ...formData, standardCostPerHour: parseFloat(e.target.value) || 0 })}
                  />
                </div>
                <div className="form-group">
                  <label>Капацитет</label>
                  <input
                    type="number"
                    step="0.01"
                    value={formData.capacity}
                    onChange={(e) => setFormData({ ...formData, capacity: parseFloat(e.target.value) || 0 })}
                  />
                </div>
                <div className="form-group">
                  <label>
                    <input
                      type="checkbox"
                      checked={formData.isActive}
                      onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                    />
                    {' '}Активен
                  </label>
                </div>
              </div>
              <div className="modal-footer">
                <button type="button" onClick={() => setShowModal(false)} className="secondary-button">
                  Откажи
                </button>
                <button type="submit" className="primary-button">
                  {editingWorkCenter ? 'Зачувај' : 'Креирај'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

export default WorkCenterList;
