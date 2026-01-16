import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { analyticsApi } from '../services/api';
import { authService } from '../services/authService';
import './Dashboard.css';

interface DashboardData {
  inventory: {
    totalItems: number;
    totalLocations: number;
    totalBalance: number;
    blockedQty: number;
  };
  production: {
    activeOrders: number;
    completedToday: number;
    wip: number;
  };
  customs: {
    pendingDeclarations: number;
    activeMRNs: number;
    expiringMRNs: number;
  };
  guarantees: {
    totalAccounts: number;
    activeGuarantees: number;
    totalExposure: number;
    expiringGuarantees: number;
  };
}

const Dashboard: React.FC = () => {
  const navigate = useNavigate();
  const [user] = useState(() => authService.getCurrentUser());
  const [data, setData] = useState<DashboardData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!user) {
      navigate('/login');
      return;
    }
    loadDashboard();
  }, [user, navigate]);

  const loadDashboard = async () => {
    try {
      setLoading(true);
      const response = await analyticsApi.getDashboard();
      setData(response.data);
      setError(null);
    } catch (err: any) {
      setError(err.message || 'Failed to load dashboard');
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = () => {
    authService.logout();
    navigate('/login');
  };

  const adminModules = [
    {
      title: 'Корисници',
      description: 'Управување со корисници на системот',
      icon: '👥',
      path: '/admin/users',
      color: '#667eea'
    },
    {
      title: 'Вработени',
      description: 'Управување со вработени',
      icon: '👔',
      path: '/admin/employees',
      color: '#48bb78'
    },
    {
      title: 'Смени',
      description: 'Управување со работни смени',
      icon: '⏰',
      path: '/admin/shifts',
      color: '#ed8936'
    },
    {
      title: 'Улоги',
      description: 'Управување со улоги и дозволи',
      icon: '🔐',
      path: '/admin/roles',
      color: '#9f7aea'
    }
  ];

  if (loading) return <div className="loading">Loading dashboard...</div>;
  if (error) return <div className="error">Error: {error}</div>;
  if (!data || !user) return <div>No data available</div>;

  return (
    <div className="dashboard-new">
      <div className="dashboard-header">
        <div className="welcome-section">
          <h1>Добредојдовте, {user.fullName}!</h1>
          <p className="user-info">
            {user.username} | {user.roles.join(', ')}
          </p>
        </div>
        <button className="btn-logout" onClick={handleLogout}>
          Одјави се
        </button>
      </div>

      <section className="dashboard-section">
        <h2>Администрација</h2>
        <div className="modules-grid">
          {adminModules.map((module) => (
            <div
              key={module.path}
              className="module-card"
              onClick={() => navigate(module.path)}
              style={{ borderLeftColor: module.color }}
            >
              <div className="module-icon" style={{ backgroundColor: `${module.color}20` }}>
                {module.icon}
              </div>
              <h3>{module.title}</h3>
              <p>{module.description}</p>
            </div>
          ))}
        </div>
      </section>

      <div className="original-dashboard">
        <div className="header">
          <h2>Статистики</h2>
          <button className="btn btn-primary" onClick={loadDashboard}>Освежи</button>
        </div>

      <div className="card-grid">
        <div className="card info">
          <h3>Total Items</h3>
          <div className="value">{data.inventory.totalItems}</div>
        </div>
        <div className="card success">
          <h3>Active Production Orders</h3>
          <div className="value">{data.production.activeOrders}</div>
        </div>
        <div className="card warning">
          <h3>Pending Declarations</h3>
          <div className="value">{data.customs.pendingDeclarations}</div>
        </div>
        <div className="card danger">
          <h3>Active Guarantees</h3>
          <div className="value">{data.guarantees.activeGuarantees}</div>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px', marginTop: '30px' }}>
        <div className="card">
          <h3 style={{ marginBottom: '15px', fontSize: '18px' }}>Inventory Status</h3>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>Total Balance:</span>
            <strong>{data.inventory.totalBalance.toFixed(2)}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>Blocked Quantity:</span>
            <strong style={{ color: '#e74c3c' }}>{data.inventory.blockedQty.toFixed(2)}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span>Locations:</span>
            <strong>{data.inventory.totalLocations}</strong>
          </div>
        </div>

        <div className="card">
          <h3 style={{ marginBottom: '15px', fontSize: '18px' }}>Production Status</h3>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>Active Orders:</span>
            <strong>{data.production.activeOrders}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>Completed Today:</span>
            <strong style={{ color: '#27ae60' }}>{data.production.completedToday}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span>WIP:</span>
            <strong>{data.production.wip.toFixed(2)}</strong>
          </div>
        </div>

        <div className="card">
          <h3 style={{ marginBottom: '15px', fontSize: '18px' }}>Customs & MRN</h3>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>Pending Declarations:</span>
            <strong style={{ color: '#f39c12' }}>{data.customs.pendingDeclarations}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>Active MRNs:</span>
            <strong>{data.customs.activeMRNs}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span>Expiring MRNs:</span>
            <strong style={{ color: '#e74c3c' }}>{data.customs.expiringMRNs}</strong>
          </div>
        </div>

        <div className="card">
          <h3 style={{ marginBottom: '15px', fontSize: '18px' }}>Guarantees</h3>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>Total Accounts:</span>
            <strong>{data.guarantees.totalAccounts}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>Active Guarantees:</span>
            <strong style={{ color: '#f39c12' }}>{data.guarantees.activeGuarantees}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>Total Exposure:</span>
            <strong style={{ color: '#e74c3c' }}>{data.guarantees.totalExposure.toFixed(2)}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span>Expiring Soon:</span>
            <strong>{data.guarantees.expiringGuarantees}</strong>
          </div>
        </div>
      </div>
      </div>
    </div>
  );
};

export default Dashboard;
