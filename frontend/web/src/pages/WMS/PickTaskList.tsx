import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { wmsApi } from '../../services/api';
import PickTaskForm from '../../components/WMS/PickTaskForm';
import { PickTaskStatus } from '../../types/wms';

const PickTaskList: React.FC = () => {
  const { t } = useTranslation();
  const [pickTasks, setPickTasks] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [filterStatus, setFilterStatus] = useState<PickTaskStatus | ''>('');
  const [showForm, setShowForm] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'assign' | 'complete'>('create');
  const [selectedTask, setSelectedTask] = useState<any>(null);

  useEffect(() => {
    loadPickTasks();
    // eslint-disable-next-line react-hooks/exhaustive-deps
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

  const handleCreateTask = () => { setFormMode('create'); setSelectedTask(null); setShowForm(true); };
  const handleAssignTask = (task: any) => { setFormMode('assign'); setSelectedTask(task); setShowForm(true); };
  const handleCompleteTask = (task: any) => { setFormMode('complete'); setSelectedTask(task); setShowForm(true); };

  const handleFormSuccess = () => { setShowForm(false); setSelectedTask(null); loadPickTasks(); };
  const handleFormCancel = () => { setShowForm(false); setSelectedTask(null); };

  const getStatusBadge = (status: PickTaskStatus) => {
    const map: Record<number, { cls: string; key: string }> = {
      1: { cls: 'badge-warning', key: 'pending' },
      2: { cls: 'badge-info',    key: 'assigned' },
      3: { cls: 'badge-info',    key: 'inProgress' },
      4: { cls: 'badge-success', key: 'completed' },
      5: { cls: 'badge-danger',  key: 'cancelled' },
    };
    const b = map[status] || { cls: 'badge', key: 'unknown' };
    return <span className={`badge ${b.cls}`}>{t(`pickTasks.status.${b.key}`)}</span>;
  };

  const getPriorityBadge = (priority: number) => {
    if (priority <= 2) return <span className="badge badge-danger">{t('pickTasks.priority.high')}</span>;
    if (priority === 3) return <span className="badge badge-warning">{t('pickTasks.priority.normal')}</span>;
    return <span className="badge">{t('pickTasks.priority.low')}</span>;
  };

  const canAssign = (task: any) => task.status === PickTaskStatus.Pending && !task.assignedToEmployeeId;
  const canComplete = (task: any) => task.status === PickTaskStatus.Assigned || task.status === PickTaskStatus.InProgress;

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

  if (loading) return <div className="loading">{t('pickTasks.loading')}</div>;

  return (
    <div>
      <div className="header">
        <h2>🎯 {t('pickTasks.title')}</h2>
        <button className="btn-primary" onClick={handleCreateTask}>
          + {t('pickTasks.newTask')}
        </button>
      </div>

      <div className="filters" style={{ marginBottom: 20 }}>
        <label style={{ marginRight: 10 }}>{t('pickTasks.filterByStatus')}:</label>
        <select
          value={filterStatus}
          onChange={(e) => setFilterStatus(e.target.value ? parseInt(e.target.value) as PickTaskStatus : '')}
          className="form-control"
          style={{ width: 220, display: 'inline-block' }}
        >
          <option value="">— {t('pickTasks.allStatuses')} —</option>
          <option value={PickTaskStatus.Pending}>{t('pickTasks.status.pending')}</option>
          <option value={PickTaskStatus.Assigned}>{t('pickTasks.status.assigned')}</option>
          <option value={PickTaskStatus.InProgress}>{t('pickTasks.status.inProgress')}</option>
          <option value={PickTaskStatus.Completed}>{t('pickTasks.status.completed')}</option>
          <option value={PickTaskStatus.Cancelled}>{t('pickTasks.status.cancelled')}</option>
        </select>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('pickTasks.columns.taskNumber')}</th>
              <th>{t('pickTasks.columns.status')}</th>
              <th>{t('pickTasks.columns.priority')}</th>
              <th>{t('pickTasks.columns.item')}</th>
              <th>{t('pickTasks.columns.location')}</th>
              <th>{t('pickTasks.columns.batch')}</th>
              <th>{t('pickTasks.columns.mrn')}</th>
              <th>{t('pickTasks.columns.qtyToPick')}</th>
              <th>{t('pickTasks.columns.assignedTo')}</th>
              <th>{t('pickTasks.columns.created')}</th>
              <th>{t('pickTasks.columns.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {pickTasks.length === 0 ? (
              <tr>
                <td colSpan={11} style={{ textAlign: 'center', padding: 20 }}>
                  {t('pickTasks.empty')}
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
                        <small style={{ color: 'var(--success)' }}>
                          {t('pickTasks.picked')}: {task.quantityPicked.toFixed(2)}
                        </small>
                      </>
                    )}
                  </td>
                  <td>
                    {task.assignedToEmployee ? (
                      <>{task.assignedToEmployee.firstName} {task.assignedToEmployee.lastName}</>
                    ) : (
                      <span style={{ color: 'var(--ink-400)' }}>{t('pickTasks.unassigned')}</span>
                    )}
                  </td>
                  <td>
                    {new Date(task.createdAt).toLocaleDateString()}<br />
                    <small>{new Date(task.createdAt).toLocaleTimeString()}</small>
                  </td>
                  <td>
                    <div style={{ display: 'flex', gap: 5, flexDirection: 'column' }}>
                      {canAssign(task) && (
                        <button onClick={() => handleAssignTask(task)} title={t('pickTasks.actions.assign')}>
                          👤 {t('pickTasks.actions.assign')}
                        </button>
                      )}
                      {canComplete(task) && (
                        <button
                          onClick={() => handleCompleteTask(task)}
                          title={t('pickTasks.actions.complete')}
                          style={{ background: 'var(--success)', color: 'white', borderColor: 'var(--success)' }}
                        >
                          ✅ {t('pickTasks.actions.complete')}
                        </button>
                      )}
                      {task.status === PickTaskStatus.Completed && (
                        <span style={{ color: 'var(--success)', fontSize: 12 }}>
                          ✓ {t('pickTasks.done')}
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

      <div style={{ marginTop: 16, padding: 12, background: 'var(--ink-50)', borderRadius: 8, border: '1px solid var(--ink-200)' }}>
        <strong>{t('pickTasks.summary')}:</strong> {pickTasks.length} {t('pickTasks.summaryCount')}
        {filterStatus !== '' && <> ({t('pickTasks.filteredByStatus')})</>}
      </div>
    </div>
  );
};

export default PickTaskList;
