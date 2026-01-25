import React, { useEffect, useState } from 'react';
import { masterDataApi } from '../../../services/api';

interface Machine {
  id: string;
  code: string;
  name: string;
  workCenterId: string;
  workCenterName?: string;
  serialNumber?: string;
  isActive: boolean;
}

interface WorkCenter {
  id: string;
  code: string;
  name: string;
}

const MachineList: React.FC = () => {
  const [machines, setMachines] = useState<Machine[]>([]);
  const [workCenters, setWorkCenters] = useState<WorkCenter[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingMachine, setEditingMachine] = useState<Machine | null>(null);
  const [formData, setFormData] = useState({
    code: '',
    name: '',
    workCenterId: '',
    serialNumber: '',
    isActive: true
  });

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [machinesRes, workCentersRes] = await Promise.all([
        masterDataApi.getMachines(),
        masterDataApi.getWorkCenters()
      ]);
      setMachines(machinesRes.data);
      setWorkCenters(workCentersRes.data);
    } catch (err) {
      console.error('Failed to load data', err);
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = () => {
    setEditingMachine(null);
    setFormData({
      code: '',
      name: '',
      workCenterId: '',
      serialNumber: '',
      isActive: true
    });
    setShowModal(true);
  };

  const handleEdit = (machine: Machine) => {
    setEditingMachine(machine);
    setFormData({
      code: machine.code,
      name: machine.name,
      workCenterId: machine.workCenterId,
      serialNumber: machine.serialNumber || '',
      isActive: machine.isActive
    });
    setShowModal(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      if (editingMachine) {
        await masterDataApi.updateMachine(editingMachine.id, formData);
      } else {
        await masterDataApi.createMachine(formData);
      }
      setShowModal(false);
      loadData();
    } catch (err) {
      console.error('Failed to save machine', err);
      alert('Failed to save machine');
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('Дали сте сигурни дека сакате да ја избришете машината?')) return;
    try {
      await masterDataApi.deleteMachine(id);
      loadData();
    } catch (err) {
      console.error('Failed to delete machine', err);
      alert('Failed to delete machine');
    }
  };

  if (loading) return <div className="loading">Loading machines...</div>;

  return (
    <div>
      <div className="header">
        <h2>Machines</h2>
        <button onClick={handleCreate} className="primary-button">
          + Нова машина
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>Код</th>
              <th>Име</th>
              <th>Работен центар</th>
              <th>Сериски број</th>
              <th>Статус</th>
              <th>Акции</th>
            </tr>
          </thead>
          <tbody>
            {machines.map((machine) => (
              <tr key={machine.id}>
                <td><strong>{machine.code}</strong></td>
                <td>{machine.name}</td>
                <td>{machine.workCenterName || '-'}</td>
                <td>{machine.serialNumber || '-'}</td>
                <td>
                  <span className={`badge ${machine.isActive ? 'badge-success' : 'badge-danger'}`}>
                    {machine.isActive ? 'Активна' : 'Неактивна'}
                  </span>
                </td>
                <td>
                  <button onClick={() => handleEdit(machine)} className="btn-edit">Измени</button>
                  <button onClick={() => handleDelete(machine.id)} className="btn-delete">Избриши</button>
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
              <h3>{editingMachine ? 'Измени машина' : 'Нова машина'}</h3>
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
                <div className="form-group">
                  <label>Работен центар *</label>
                  <select
                    value={formData.workCenterId}
                    onChange={(e) => setFormData({ ...formData, workCenterId: e.target.value })}
                    required
                  >
                    <option value="">Избери...</option>
                    {workCenters.map((wc) => (
                      <option key={wc.id} value={wc.id}>
                        {wc.code} - {wc.name}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="form-group">
                  <label>Сериски број</label>
                  <input
                    type="text"
                    value={formData.serialNumber}
                    onChange={(e) => setFormData({ ...formData, serialNumber: e.target.value })}
                  />
                </div>
                <div className="form-group">
                  <label>
                    <input
                      type="checkbox"
                      checked={formData.isActive}
                      onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                    />
                    {' '}Активна
                  </label>
                </div>
              </div>
              <div className="modal-footer">
                <button type="button" onClick={() => setShowModal(false)} className="secondary-button">
                  Откажи
                </button>
                <button type="submit" className="primary-button">
                  {editingMachine ? 'Зачувај' : 'Креирај'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

export default MachineList;
