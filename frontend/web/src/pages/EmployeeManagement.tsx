import React, { useState, useEffect } from 'react';
import { employeeService, Employee, CreateEmployeeRequest, UpdateEmployeeRequest } from '../services/employeeService';
import { authService, User } from '../services/authService';
import './EmployeeManagement.css';

interface EmployeeFormData {
  userId: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  position: string;
  department: string;
  hireDate: string;
  isActive: boolean;
}

const EmployeeManagement: React.FC = () => {
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [users, setUsers] = useState<User[]>([]);
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
    hireDate: new Date().toISOString().split('T')[0],
    isActive: true
  });
  const [error, setError] = useState('');

  useEffect(() => {
    loadData();
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
    } catch (err) {
      setError('Грешка при вчитување на податоци');
      console.error(err);
    } finally {
      setLoading(false);
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
      hireDate: employee.hireDate.split('T')[0],
      isActive: employee.isActive
    });
    setShowModal(true);
  };

  const handleSave = async () => {
    try {
      setError('');
      if (editingEmployee) {
        const updateData: UpdateEmployeeRequest = {
          firstName: formData.firstName,
          lastName: formData.lastName,
          email: formData.email,
          phone: formData.phone || undefined,
          position: formData.position,
          department: formData.department,
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
          position: formData.position,
          department: formData.department,
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
                <td>{employee.position}</td>
                <td>{employee.department}</td>
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
                  <input
                    type="text"
                    value={formData.position}
                    onChange={(e) => setFormData({ ...formData, position: e.target.value })}
                    required
                  />
                </div>

                <div className="form-group">
                  <label>Одделение</label>
                  <input
                    type="text"
                    value={formData.department}
                    onChange={(e) => setFormData({ ...formData, department: e.target.value })}
                    required
                  />
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
