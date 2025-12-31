import React, { useState, useEffect } from 'react';
import { authService, Role, Permission } from '../services/authService';
import './RoleManagement.css';

interface RoleFormData {
  name: string;
  description: string;
  permissionIds: string[];
}

const RoleManagement: React.FC = () => {
  const [roles, setRoles] = useState<Role[]>([]);
  const [permissions, setPermissions] = useState<Permission[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingRole, setEditingRole] = useState<Role | null>(null);
  const [formData, setFormData] = useState<RoleFormData>({
    name: '',
    description: '',
    permissionIds: []
  });
  const [error, setError] = useState('');
  const [selectedRole, setSelectedRole] = useState<Role | null>(null);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [rolesData, permissionsData] = await Promise.all([
        authService.getRoles(),
        authService.getPermissions()
      ]);
      setRoles(rolesData);
      setPermissions(permissionsData);
    } catch (err) {
      setError('Грешка при вчитување на податоци');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleAdd = () => {
    setEditingRole(null);
    setFormData({
      name: '',
      description: '',
      permissionIds: []
    });
    setShowModal(true);
  };

  const handleEdit = (role: Role) => {
    setEditingRole(role);
    setFormData({
      name: role.name,
      description: role.description || '',
      permissionIds: role.permissions.map(p => p.id)
    });
    setShowModal(true);
  };

  const handleSave = async () => {
    try {
      setError('');
      if (editingRole) {
        await authService.updateRole(editingRole.id, {
          name: formData.name,
          description: formData.description || undefined,
          permissionIds: formData.permissionIds
        });
      } else {
        await authService.createRole({
          name: formData.name,
          description: formData.description || undefined,
          permissionIds: formData.permissionIds
        });
      }
      setShowModal(false);
      loadData();
    } catch (err: any) {
      setError(err.response?.data?.message || 'Грешка при зачувување');
    }
  };

  const handleDelete = async (roleId: string) => {
    if (!window.confirm('Дали сте сигурни дека сакате да ја избришете улогата?')) {
      return;
    }
    try {
      await authService.deleteRole(roleId);
      loadData();
    } catch (err) {
      setError('Грешка при бришење');
    }
  };

  const groupPermissionsByResource = () => {
    const grouped: { [key: string]: Permission[] } = {};
    permissions.forEach(perm => {
      if (!grouped[perm.resource]) {
        grouped[perm.resource] = [];
      }
      grouped[perm.resource].push(perm);
    });
    return grouped;
  };

  const groupedPermissions = groupPermissionsByResource();

  if (loading) {
    return <div className="loading">Се вчитува...</div>;
  }

  return (
    <div className="role-management">
      <div className="page-header">
        <h1>Управување со улоги и дозволи</h1>
        <button className="btn btn-primary" onClick={handleAdd}>
          + Додади улога
        </button>
      </div>

      {error && <div className="error-banner">{error}</div>}

      <div className="roles-layout">
        <div className="roles-list">
          <h2>Улоги</h2>
          {roles.map(role => (
            <div
              key={role.id}
              className={`role-item ${selectedRole?.id === role.id ? 'selected' : ''}`}
              onClick={() => setSelectedRole(role)}
            >
              <div className="role-info">
                <h3>{role.name}</h3>
                {role.description && <p>{role.description}</p>}
                <span className="permissions-count">
                  {role.permissions.length} {role.permissions.length === 1 ? 'дозвола' : 'дозволи'}
                </span>
              </div>
              <div className="role-actions">
                <button
                  className="btn btn-sm btn-edit"
                  onClick={(e) => {
                    e.stopPropagation();
                    handleEdit(role);
                  }}
                >
                  Измени
                </button>
                <button
                  className="btn btn-sm btn-delete"
                  onClick={(e) => {
                    e.stopPropagation();
                    handleDelete(role.id);
                  }}
                >
                  Избриши
                </button>
              </div>
            </div>
          ))}
        </div>

        <div className="permissions-detail">
          {selectedRole ? (
            <>
              <h2>Дозволи за: {selectedRole.name}</h2>
              {selectedRole.description && (
                <p className="role-description">{selectedRole.description}</p>
              )}
              <div className="permissions-groups">
                {Object.entries(groupedPermissions).map(([resource, perms]) => {
                  const rolePermIds = selectedRole.permissions.map(p => p.id);
                  const hasAnyPerm = perms.some(p => rolePermIds.includes(p.id));
                  
                  if (!hasAnyPerm) return null;

                  return (
                    <div key={resource} className="permission-group">
                      <h3>{resource}</h3>
                      <div className="permissions-list">
                        {perms.map(perm => {
                          const hasPermission = rolePermIds.includes(perm.id);
                          if (!hasPermission) return null;
                          
                          return (
                            <div key={perm.id} className="permission-badge">
                              <span className="permission-action">{perm.action}</span>
                              {perm.description && (
                                <span className="permission-desc">{perm.description}</span>
                              )}
                            </div>
                          );
                        })}
                      </div>
                    </div>
                  );
                })}
              </div>
            </>
          ) : (
            <div className="empty-detail">
              <p>Изберете улога за да ги видите дозволите</p>
            </div>
          )}
        </div>
      </div>

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal large" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>{editingRole ? 'Измени улога' : 'Додади улога'}</h2>
              <button className="modal-close" onClick={() => setShowModal(false)}>×</button>
            </div>

            <div className="modal-body">
              {error && <div className="error-message">{error}</div>}

              <div className="form-group">
                <label>Име на улога</label>
                <input
                  type="text"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  placeholder="пр. Администратор"
                  required
                />
              </div>

              <div className="form-group">
                <label>Опис</label>
                <textarea
                  value={formData.description}
                  onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                  placeholder="Опционален опис на улогата..."
                  rows={2}
                />
              </div>

              <div className="form-group">
                <label>Дозволи</label>
                <div className="permissions-selector">
                  {Object.entries(groupedPermissions).map(([resource, perms]) => (
                    <div key={resource} className="permission-group-selector">
                      <h4>{resource}</h4>
                      <div className="checkbox-group">
                        {perms.map(perm => (
                          <label key={perm.id} className="checkbox-label">
                            <input
                              type="checkbox"
                              checked={formData.permissionIds.includes(perm.id)}
                              onChange={(e) => {
                                if (e.target.checked) {
                                  setFormData({
                                    ...formData,
                                    permissionIds: [...formData.permissionIds, perm.id]
                                  });
                                } else {
                                  setFormData({
                                    ...formData,
                                    permissionIds: formData.permissionIds.filter(id => id !== perm.id)
                                  });
                                }
                              }}
                            />
                            <div className="permission-info">
                              <strong>{perm.action}</strong>
                              {perm.description && <span>{perm.description}</span>}
                            </div>
                          </label>
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
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

export default RoleManagement;
