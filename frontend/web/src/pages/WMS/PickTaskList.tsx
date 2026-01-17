import React, { useEffect, useState } from 'react';
import { wmsApi } from '../../services/api';
import PickTaskForm from '../../components/WMS/PickTaskForm';
import { PickTaskStatus } from '../../types/wms';

const PickTaskList: React.FC = () => {
  const [pickTasks, setPickTasks] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [filterStatus, setFilterStatus] = useState<PickTaskStatus | ''>('');
  const [showForm, setShowForm] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'assign' | 'complete'>('create');
  const [selectedTask, setSelectedTask] = useState<any>(null);

  useEffect(() => {
    loadPickTasks();
  }, [filterStatus]);

  const loadPickTasks = async () => {
    try {
      setLoading(true);
      const response = await wmsApi.getPickTasks(filterStatus as string | undefined);
      setPickTasks(response.data);
    } catch (err) {
      console.error('Failed to load pick tasks', err);
    } finally {
      setLoading(false);
    }
  };

  const handleCreateTask = () => {
    setFormMode('create');
    setSelectedTask(null);
    setShowForm(true);
  };

  const handleAssignTask = (task: any) => {
    setFormMode('assign');
    setSelectedTask(task);
    setShowForm(true);
  };

  const handleCompleteTask = (task: any) => {
    setFormMode('complete');
    setSelectedTask(task);
    setShowForm(true);
  };

  const handleFormSuccess = () => {
    setShowForm(false);
    setSelectedTask(null);
    loadPickTasks();
  };

  const handleFormCancel = () => {
    setShowForm(false);
    setSelectedTask(null);
  };

  const getStatusBadge = (status: PickTaskStatus) => {
    const badges: { [key: number]: { class: string, text: string } } = {
      1: { class: 'badge-warning', text: 'Pending' },
      2: { class: 'badge-info', text: 'Assigned' },
      3: { class: 'badge-primary', text: 'In Progress' },
      4: { class: 'badge-success', text: 'Completed' },
      5: { class: 'badge-danger', text: 'Cancelled' },
    };
    const badge = badges[status] || { class: 'badge-secondary', text: 'Unknown' };
    return <span className={`badge ${badge.class}`}>{badge.text}</span>;
  };

  const getPriorityBadge = (priority: number) => {
    if (priority <= 2) return <span className="badge badge-danger">High</span>;
    if (priority === 3) return <span className="badge badge-warning">Normal</span>;
    return <span className="badge badge-secondary">Low</span>;
  };

  const canAssign = (task: any) => {
    return task.status === PickTaskStatus.Pending && !task.assignedToEmployeeId;
  };

  const canComplete = (task: any) => {
    return task.status === PickTaskStatus.Assigned || task.status === PickTaskStatus.InProgress;
  };

  if (showForm) {
    return (
      <PickTaskForm
        mode={formMode}
        existingTask={selectedTask}
        onSuccess={handleFormSuccess}
        onCancel={handleFormCancel}
      />
    );
  }

  if (loading) return <div className="loading">Loading pick tasks...</div>;

  return (
    <div>
      <div className="header">
        <h2>🎯 Pick Task Management</h2>
        <button className="btn btn-primary" onClick={handleCreateTask}>
          + New Pick Task
        </button>
      </div>

      <div className="filters" style={{ marginBottom: '20px' }}>
        <label style={{ marginRight: '10px' }}>Filter by Status:</label>
        <select
          value={filterStatus}
          onChange={(e) => setFilterStatus(e.target.value ? parseInt(e.target.value) as PickTaskStatus : '')}
          className="form-control"
          style={{ width: '200px', display: 'inline-block' }}
        >
          <option value="">-- All Statuses --</option>
          <option value={PickTaskStatus.Pending}>Pending</option>
          <option value={PickTaskStatus.Assigned}>Assigned</option>
          <option value={PickTaskStatus.InProgress}>In Progress</option>
          <option value={PickTaskStatus.Completed}>Completed</option>
          <option value={PickTaskStatus.Cancelled}>Cancelled</option>
        </select>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>Task #</th>
              <th>Status</th>
              <th>Priority</th>
              <th>Item</th>
              <th>Location</th>
              <th>Batch</th>
              <th>MRN</th>
              <th>Qty to Pick</th>
              <th>Assigned To</th>
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {pickTasks.length === 0 ? (
              <tr>
                <td colSpan={11} style={{ textAlign: 'center', padding: '20px' }}>
                  No pick tasks found. Create your first pick task!
                </td>
              </tr>
            ) : (
              pickTasks.map((task) => (
                <tr key={task.id}>
                  <td><strong>{task.taskNumber}</strong></td>
                  <td>{getStatusBadge(task.status)}</td>
                  <td>{getPriorityBadge(task.priority)}</td>
                  <td>
                    {task.item?.code}<br />
                    <small>{task.item?.name}</small>
                  </td>
                  <td>{task.location?.name}</td>
                  <td>{task.batchNumber || '-'}</td>
                  <td>{task.mrn || '-'}</td>
                  <td>
                    {task.quantityToPick?.toFixed(2)} {task.uoM?.code}
                    {task.quantityPicked && (
                      <>
                        <br />
                        <small className="text-success">
                          Picked: {task.quantityPicked.toFixed(2)}
                        </small>
                      </>
                    )}
                  </td>
                  <td>
                    {task.assignedToEmployee ? (
                      <>
                        {task.assignedToEmployee.firstName} {task.assignedToEmployee.lastName}
                      </>
                    ) : (
                      <span className="text-muted">Unassigned</span>
                    )}
                  </td>
                  <td>
                    {new Date(task.createdAt).toLocaleDateString()}<br />
                    <small>{new Date(task.createdAt).toLocaleTimeString()}</small>
                  </td>
                  <td>
                    <div style={{ display: 'flex', gap: '5px', flexDirection: 'column' }}>
                      {canAssign(task) && (
                        <button
                          className="btn btn-xs btn-info"
                          onClick={() => handleAssignTask(task)}
                          title="Assign to employee"
                        >
                          👤 Assign
                        </button>
                      )}
                      {canComplete(task) && (
                        <button
                          className="btn btn-xs btn-success"
                          onClick={() => handleCompleteTask(task)}
                          title="Complete pick task"
                        >
                          ✅ Complete
                        </button>
                      )}
                      {task.status === PickTaskStatus.Completed && (
                        <span className="text-success" style={{ fontSize: '12px' }}>
                          ✓ Done
                        </span>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div className="summary" style={{ marginTop: '20px', padding: '15px', background: '#f8f9fa', borderRadius: '4px' }}>
        <strong>Summary:</strong> {pickTasks.length} pick task(s) found
        {filterStatus && <> (filtered by status)</>}
      </div>
    </div>
  );
};

export default PickTaskList;
