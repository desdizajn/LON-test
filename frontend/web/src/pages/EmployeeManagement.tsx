import React, { useState, useEffect } from 'react';
import { employeeService, Employee, CreateEmployeeRequest, UpdateEmployeeRequest } from '../services/employeeService';
import { authService, User } from '../services/authService';
import { knowledgeBaseApi } from '../services/api';
import './EmployeeManagement.css';

interface EmployeeFormData {
  userId: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  // Phase 17 §E7.5 — free-text kept for the deprecation window; new rows leave
  // these empty and stamp `departmentId`/`positionId` instead.
  position: string;
  department: string;
  departmentId: string;
  positionId: string;
  hireDate: string;
  isActive: boolean;
}

interface CodeListRow {
  id: string;
  listType: string;
  code: string;
  descriptionMK: string;
  descriptionEN?: string | null;
  sortOrder?: number;
}

const DEPARTMENT_LIST_TYPE = 'EmployeeDepartment';
const POSITION_LIST_TYPE = 'EmployeePosition';

const EmployeeManagement: React.FC = () => {
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [departments, setDepartments] = useState<CodeListRow[]>([]);
  const [positions, setPositions] = useState<CodeListRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingEmployee, setEditingEmployee] = useState<Employee | null>(null);
  const [formData, setFormData] = useState<EmployeeFormData>({
    userId: '',
    firstName: '',
    lastName: '',
    email: '',
    phone: '',
    position: '',
    department: '',
    departmentId: '',
    positionId: '',
    hireDate: new Date().toISOString().split('T')[0],
    isActive: true
  });
  const [error, setError] = useState('');

  useEffect(() => {
    loadData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [employeesData, usersData] = await Promise.all([
        employeeService.getEmployees(),
        authService.getUsers()
      ]);
      setEmployees(employeesData);
      setUsers(usersData);
      await Promise.all([
        loadCodeList(DEPARTMENT_LIST_TYPE, setDepartments),
        loadCodeList(POSITION_LIST_TYPE, setPositions),
      ]);
    } catch (err) {
      setError('Грешка при вчитување на податоци');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  // Phase 17 §E7.5 — pull a single CodeListItem category. Backend returns
  // { listTypes, items, totalItems } grouped by listType; we just want the
  // flat list for the requested type.
  const loadCodeList = async (
    listType: string,
    setter: React.Dispatch<React.SetStateAction<CodeListRow[]>>,
  ) => {
    try {
      const resp = await knowledgeBaseApi.getCodeLists(listType);
      const items = resp.data?.items?.[listType] ?? [];
      setter(items as CodeListRow[]);
    } catch (err) {
      console.error(`Failed to load code-list ${listType}`, err);
      setter([]);
    }
  };

  // Inline „+ Нов" — prompts for code + description, creates a CodeListItem
  // in the chosen category, refreshes the dropdown, and selects the new id.
  const handleAddCodeListItem = async (
    listType: string,
    setter: React.Dispatch<React.SetStateAction<CodeListRow[]>>,
    onCreated: (id: string) => void,
  ) => {
    const code = window.prompt('Шифра (нпр. SEW, QC, SH-MGR):')?.trim();
    if (!code) return;
    const desc = window.prompt('Опис на македонски:')?.trim();
    if (!desc) return;
    try {
      const resp = await knowledgeBaseApi.createCodeListItem({
        listType,
        code,
        descriptionMK: desc,
        sortOrder: 0,
      });
      const created = resp.data;
      if (created?.id) {
        await loadCodeList(listType, setter);
        onCreated(created.id);
      }
    } catch (err: any) {
      setError(err.response?.data?.message || `Грешка при создавање на ${listType}.`);
    }
  };

  const handleAdd = () => {
    setEditingEmployee(null);
    setFormData({
      userId: '',
      firstName: '',
      lastName: '',
      email: '',
      phone: '',
      position: '',
      department: '',
      departmentId: '',
      positionId: '',
      hireDate: new Date().toISOString().split('T')[0],
      isActive: true
    });
    setShowModal(true);
  };

  const handleEdit = (employee: Employee) => {
    setEditingEmployee(employee);
    setFormData({
      userId: employee.userId,
      firstName: employee.firstName,
      lastName: employee.lastName,
      email: employee.email,
      phone: employee.phone || '',
      position: employee.position,
      department: employee.department,
      departmentId: employee.departmentId ?? '',
      positionId: employee.positionId ?? '',
      hireDate: employee.hireDate.split('T')[0],
      isActive: employee.isActive
    });
    setShowModal(true);
  };

  const handleSave = async () => {
    try {
      setError('');
      // Mirror the FK selection into the legacy free-text field until the
      // deprecation window closes in Phase 18 — keeps any consumers that still
      // read `position`/`department` working.
      const departmentLabel =
        departments.find((d) => d.id === formData.departmentId)?.descriptionMK ?? formData.department;
      const positionLabel =
        positions.find((p) => p.id === formData.positionId)?.descriptionMK ?? formData.position;

      if (editingEmployee) {
        const updateData: UpdateEmployeeRequest = {
          firstName: formData.firstName,
          lastName: formData.lastName,
          email: formData.email,
          phone: formData.phone || undefined,
          position: positionLabel,
          department: departmentLabel,
          departmentId: formData.departmentId || null,
          positionId: formData.positionId || null,
          isActive: formData.isActive
        };
        await employeeService.updateEmployee(editingEmployee.id, updateData);
      } else {
        const createData: CreateEmployeeRequest = {
          userId: formData.userId,
          firstName: formData.firstName,
          lastName: formData.lastName,
          email: formData.email,
          phone: formData.phone || undefined,
          position: positionLabel,
          department: departmentLabel,
          departmentId: formData.departmentId || null,
          positionId: formData.positionId || null,
          hireDate: formData.hireDate
        };
        await employeeService.createEmployee(createData);
      }
      setShowModal(false);
      loadData();
    } catch (err: any) {
      setError(err.response?.data?.message || 'Грешка при зачувување');
    }
  };

  const handleDelete = async (employeeId: string) => {
    if (!window.confirm('Дали сте сигурни дека сакате да го избришете вработениот?')) {
      return;
    }
    try {
      await employeeService.deleteEmployee(employeeId);
      loadData();
    } catch (err) {
      setError('Грешка при бришење');
    }
  };

  const getUserName = (userId: string): string => {
    const user = users.find(u => u.id === userId);
    return user?.fullName || user?.username || 'N/A';
  };

  if (loading) {
    return <div className="loading">Се вчитува...</div>;
  }

  return (
    <div className="employee-management">
      <div className="page-header">
        <h1>Управување со вработени</h1>
        <button className="btn btn-primary" onClick={handleAdd}>
          + Додади вработен
        </button>
      </div>

      {error && <div className="error-banner">{error}</div>}

      <div className="employees-table-container">
        <table className="employees-table">
          <thead>
            <tr>
              <th>Име и презиме</th>
              <th>Email</th>
              <th>Телефон</th>
              <th>Позиција</th>
              <th>Одделение</th>
              <th>Датум на вработување</th>
              <th>Статус</th>
              <th>Акции</th>
            </tr>
          </thead>
          <tbody>
            {employees.map(employee => (
              <tr key={employee.id}>
                <td>
                  <div>
                    <strong>{employee.firstName} {employee.lastName}</strong>
                    <div className="user-link">{getUserName(employee.userId)}</div>
                  </div>
                </td>
                <td>{employee.email}</td>
                <td>{employee.phone || 'N/A'}</td>
                <td>{employee.positionName ?? employee.position ?? '—'}</td>
                <td>{employee.departmentName ?? employee.department ?? '—'}</td>
                <td>{new Date(employee.hireDate).toLocaleDateString('mk-MK')}</td>
                <td>
                  <span className={`status-badge ${employee.isActive ? 'active' : 'inactive'}`}>
                    {employee.isActive ? 'Активен' : 'Неактивен'}
                  </span>
                </td>
                <td>
                  <div className="action-buttons">
                    <button className="btn btn-sm btn-edit" onClick={() => handleEdit(employee)}>
                      Измени
                    </button>
                    <button className="btn btn-sm btn-delete" onClick={() => handleDelete(employee.id)}>
                      Избриши
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>{editingEmployee ? 'Измени вработен' : 'Додади вработен'}</h2>
              <button className="modal-close" onClick={() => setShowModal(false)}>×</button>
            </div>

            <div className="modal-body">
              {error && <div className="error-message">{error}</div>}

              {!editingEmployee && (
                <div className="form-group">
                  <label>Корисник</label>
                  <select
                    value={formData.userId}
                    onChange={(e) => setFormData({ ...formData, userId: e.target.value })}
                    required
                  >
                    <option value="">Избери корисник</option>
                    {users.map(user => (
                      <option key={user.id} value={user.id}>
                        {user.fullName} ({user.username})
                      </option>
                    ))}
                  </select>
                </div>
              )}

              <div className="form-row">
                <div className="form-group">
                  <label>Име</label>
                  <input
                    type="text"
                    value={formData.firstName}
                    onChange={(e) => setFormData({ ...formData, firstName: e.target.value })}
                    required
                  />
                </div>

                <div className="form-group">
                  <label>Презиме</label>
                  <input
                    type="text"
                    value={formData.lastName}
                    onChange={(e) => setFormData({ ...formData, lastName: e.target.value })}
                    required
                  />
                </div>
              </div>

              <div className="form-group">
                <label>Email</label>
                <input
                  type="email"
                  value={formData.email}
                  onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                  required
                />
              </div>

              <div className="form-group">
                <label>Телефон</label>
                <input
                  type="tel"
                  value={formData.phone}
                  onChange={(e) => setFormData({ ...formData, phone: e.target.value })}
                />
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label>Позиција</label>
                  <div style={{ display: 'flex', gap: 6 }}>
                    <select
                      value={formData.positionId}
                      onChange={(e) => setFormData({ ...formData, positionId: e.target.value })}
                      style={{ flex: 1 }}
                      required
                    >
                      <option value="">— избери —</option>
                      {positions.map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.descriptionMK} ({p.code})
                        </option>
                      ))}
                    </select>
                    <button
                      type="button"
                      className="btn btn-sm"
                      onClick={() =>
                        handleAddCodeListItem(POSITION_LIST_TYPE, setPositions, (id) =>
                          setFormData((d) => ({ ...d, positionId: id })),
                        )
                      }
                      title="Додај нова позиција"
                    >
                      + Нов
                    </button>
                  </div>
                </div>

                <div className="form-group">
                  <label>Одделение</label>
                  <div style={{ display: 'flex', gap: 6 }}>
                    <select
                      value={formData.departmentId}
                      onChange={(e) => setFormData({ ...formData, departmentId: e.target.value })}
                      style={{ flex: 1 }}
                      required
                    >
                      <option value="">— избери —</option>
                      {departments.map((d) => (
                        <option key={d.id} value={d.id}>
                          {d.descriptionMK} ({d.code})
                        </option>
                      ))}
                    </select>
                    <button
                      type="button"
                      className="btn btn-sm"
                      onClick={() =>
                        handleAddCodeListItem(DEPARTMENT_LIST_TYPE, setDepartments, (id) =>
                          setFormData((d) => ({ ...d, departmentId: id })),
                        )
                      }
                      title="Додај ново одделение"
                    >
                      + Нов
                    </button>
                  </div>
                </div>
              </div>

              {!editingEmployee && (
                <div className="form-group">
                  <label>Датум на вработување</label>
                  <input
                    type="date"
                    value={formData.hireDate}
                    onChange={(e) => setFormData({ ...formData, hireDate: e.target.value })}
                    required
                  />
                </div>
              )}

              {editingEmployee && (
                <div className="form-group">
                  <label className="checkbox-label">
                    <input
                      type="checkbox"
                      checked={formData.isActive}
                      onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                    />
                    Активен вработен
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

export default EmployeeManagement;
