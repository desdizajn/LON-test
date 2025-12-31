import React, { useState, useEffect } from 'react';
import { authService, User, Role, CreateUserRequest, UpdateUserRequest } from '../services/authService';
import './UserManagement.css';

interface UserFormData {
  username: string;
  email: string;
  fullName: string;
  password: string;
  roleIds: string[];
  isActive: boolean;
}

const UserManagement: React.FC = () => {
  const [users, setUsers] = useState<User[]>([]);
  const [roles, setRoles] = useState<Role[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingUser, setEditingUser] = useState<User | null>(null);
  const [formData, setFormData] = useState<UserFormData>({
    username: '',
    email: '',
    fullName: '',
    password: '',
    roleIds: [],
    isActive: true
  });
  const [error, setError] = useState('');

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [usersData, rolesData] = await Promise.all([
        authService.getUsers(),
        authService.getRoles()
      ]);
      setUsers(usersData);
      setRoles(rolesData);
    } catch (err) {
      setError('Грешка при вчитување на податоци');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleAdd = () => {
    setEditingUser(null);
    setFormData({
      username: '',
      email: '',
      fullName: '',
      password: '',
      roleIds: [],
      isActive: true
    });
    setShowModal(true);
  };

  const handleEdit = (user: User) => {
    setEditingUser(user);
    setFormData({
      username: user.username,
      email: user.email,
      fullName: user.fullName,
      password: '',
      roleIds: user.roles,
      isActive: user.isActive
    });
    setShowModal(true);
  };

  const handleSave = async () => {
    try {
      setError('');
      if (editingUser) {
        // Update existing user
        const updateData: UpdateUserRequest = {
          email: formData.email,
          fullName: formData.fullName,
          isActive: formData.isActive,
          roleIds: formData.roleIds
        };
        await authService.updateUser(editingUser.id, updateData);
      } else {
        // Create new user
        const createData: CreateUserRequest = {
          username: formData.username,
          email: formData.email,
          fullName: formData.fullName,
          password: formData.password,
          roleIds: formData.roleIds
        };
        await authService.createUser(createData);
      }
      setShowModal(false);
      loadData();
    } catch (err: any) {
      setError(err.response?.data?.message || 'Грешка при зачувување');
    }
  };

  const handleDelete = async (userId: string) => {
    if (!window.confirm('Дали сте сигурни дека сакате да го избришете корисникот?')) {
      return;
    }
    try {
      await authService.deleteUser(userId);
      loadData();
    } catch (err) {
      setError('Грешка при бришење');
    }
  };

  const getRoleName = (roleId: string): string => {
    const role = roles.find(r => r.id === roleId);
    return role?.name || roleId;
  };

  if (loading) {
    return <div className="loading">Се вчитува...</div>;
  }

  return (
    <div className="user-management">
      <div className="page-header">
        <h1>Управување со корисници</h1>
        <button className="btn btn-primary" onClick={handleAdd}>
          + Додади корисник
        </button>
      </div>

      {error && <div className="error-banner">{error}</div>}

      <div className="users-table-container">
        <table className="users-table">
          <thead>
            <tr>
              <th>Корисничко име</th>
              <th>Целосно име</th>
              <th>Email</th>
              <th>Улоги</th>
              <th>Статус</th>
              <th>Последна најава</th>
              <th>Акции</th>
            </tr>
          </thead>
          <tbody>
            {users.map(user => (
              <tr key={user.id}>
                <td>{user.username}</td>
                <td>{user.fullName}</td>
                <td>{user.email}</td>
                <td>
                  <div className="roles-badges">
                    {user.roles.map(roleId => (
                      <span key={roleId} className="badge">{getRoleName(roleId)}</span>
                    ))}
                  </div>
                </td>
                <td>
                  <span className={`status-badge ${user.isActive ? 'active' : 'inactive'}`}>
                    {user.isActive ? 'Активен' : 'Неактивен'}
                  </span>
                </td>
                <td>{user.lastLogin ? new Date(user.lastLogin).toLocaleString('mk-MK') : 'Никогаш'}</td>
                <td>
                  <div className="action-buttons">
                    <button className="btn btn-sm btn-edit" onClick={() => handleEdit(user)}>
                      Измени
                    </button>
                    <button className="btn btn-sm btn-delete" onClick={() => handleDelete(user.id)}>
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
              <h2>{editingUser ? 'Измени корисник' : 'Додади корисник'}</h2>
              <button className="modal-close" onClick={() => setShowModal(false)}>×</button>
            </div>

            <div className="modal-body">
              {error && <div className="error-message">{error}</div>}

              <div className="form-group">
                <label>Корисничко име</label>
                <input
                  type="text"
                  value={formData.username}
                  onChange={(e) => setFormData({ ...formData, username: e.target.value })}
                  disabled={!!editingUser}
                  required
                />
              </div>

              <div className="form-group">
                <label>Целосно име</label>
                <input
                  type="text"
                  value={formData.fullName}
                  onChange={(e) => setFormData({ ...formData, fullName: e.target.value })}
                  required
                />
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

              {!editingUser && (
                <div className="form-group">
                  <label>Лозинка</label>
                  <input
                    type="password"
                    value={formData.password}
                    onChange={(e) => setFormData({ ...formData, password: e.target.value })}
                    required
                  />
                </div>
              )}

              <div className="form-group">
                <label>Улоги</label>
                <div className="checkbox-group">
                  {roles.map(role => (
                    <label key={role.id} className="checkbox-label">
                      <input
                        type="checkbox"
                        checked={formData.roleIds.includes(role.id)}
                        onChange={(e) => {
                          if (e.target.checked) {
                            setFormData({ ...formData, roleIds: [...formData.roleIds, role.id] });
                          } else {
                            setFormData({ ...formData, roleIds: formData.roleIds.filter(id => id !== role.id) });
                          }
                        }}
                      />
                      {role.name}
                    </label>
                  ))}
                </div>
              </div>

              <div className="form-group">
                <label className="checkbox-label">
                  <input
                    type="checkbox"
                    checked={formData.isActive}
                    onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                  />
                  Активен корисник
                </label>
              </div>
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

export default UserManagement;
