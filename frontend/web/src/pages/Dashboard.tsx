import React, { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation();
  const [user] = useState(() => authService.getCurrentUser());
  const [data, setData] = useState<DashboardData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const hasLoadedRef = useRef(false);

  useEffect(() => {
    if (!user) {
      navigate('/login');
      return;
    }
    if (hasLoadedRef.current) {
      return;
    }
    hasLoadedRef.current = true;
    loadDashboard();
  }, [user, navigate]);

  const loadDashboard = async () => {
    if (!user) {
      return;
    }
    try {
      setLoading(true);
      const response = await analyticsApi.getDashboard();
      setData(response.data);
      setError(null);
    } catch (err: any) {
      if (err?.response?.status === 401) {
        authService.logout();
        navigate('/login');
        return;
      }
      setError(err.message || t('dashboard.loadFailed'));
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
      title: t('nav.users'),
      description: t('dashboard.usersManagement'),
      icon: '👥',
      path: '/admin/users',
      color: '#667eea'
    },
    {
      title: t('nav.employees'),
      description: t('dashboard.employeesManagement'),
      icon: '👔',
      path: '/admin/employees',
      color: '#48bb78'
    },
    {
      title: t('nav.shifts'),
      description: t('dashboard.shiftsManagement'),
      icon: '⏰',
      path: '/admin/shifts',
      color: '#ed8936'
    },
    {
      title: t('nav.roles'),
      description: t('dashboard.rolesManagement'),
      icon: '🔐',
      path: '/admin/roles',
      color: '#9f7aea'
    }
  ];

  if (loading) return <div className="loading">{t('dashboard.loadingData')}</div>;
  if (error) return <div className="error">{t('dashboard.errorPrefix')}: {error}</div>;
  if (!data || !user) return <div>{t('dashboard.noDataAvailable')}</div>;

  return (
    <div className="dashboard-new">
      <div className="dashboard-header">
        <div className="welcome-section">
          <h1>{t('app.welcome', { name: user.fullName })}</h1>
          <p className="user-info">
            {user.username} | {user.roles.join(', ')}
          </p>
        </div>
        <button className="btn-logout" onClick={handleLogout}>
          {t('nav.logout')}
        </button>
      </div>

      <section className="dashboard-section">
        <h2>{t('dashboard.administration')}</h2>
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
          <h2>{t('dashboard.statistics')}</h2>
          <button className="btn btn-primary" onClick={loadDashboard}>{t('common.refresh')}</button>
        </div>

      <div className="card-grid">
        <div className="card info">
          <h3>{t('dashboard.totalItems')}</h3>
          <div className="value">{data.inventory.totalItems}</div>
        </div>
        <div className="card success">
          <h3>{t('dashboard.activeProductionOrders')}</h3>
          <div className="value">{data.production.activeOrders}</div>
        </div>
        <div className="card warning">
          <h3>{t('dashboard.pendingDeclarations')}</h3>
          <div className="value">{data.customs.pendingDeclarations}</div>
        </div>
        <div className="card danger">
          <h3>{t('dashboard.activeGuarantees')}</h3>
          <div className="value">{data.guarantees.activeGuarantees}</div>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px', marginTop: '30px' }}>
        <div className="card">
          <h3 style={{ marginBottom: '15px', fontSize: '18px' }}>{t('dashboard.inventoryStatus')}</h3>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>{t('dashboard.totalBalance')}:</span>
            <strong>{data.inventory.totalBalance.toFixed(2)}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>{t('dashboard.blockedQuantity')}:</span>
            <strong style={{ color: '#e74c3c' }}>{data.inventory.blockedQty.toFixed(2)}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span>{t('dashboard.locations')}:</span>
            <strong>{data.inventory.totalLocations}</strong>
          </div>
        </div>

        <div className="card">
          <h3 style={{ marginBottom: '15px', fontSize: '18px' }}>{t('dashboard.productionStatus')}</h3>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>{t('dashboard.activeOrders')}:</span>
            <strong>{data.production.activeOrders}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>{t('dashboard.completedToday')}:</span>
            <strong style={{ color: '#27ae60' }}>{data.production.completedToday}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span>{t('dashboard.wip')}:</span>
            <strong>{data.production.wip.toFixed(2)}</strong>
          </div>
        </div>

        <div className="card">
          <h3 style={{ marginBottom: '15px', fontSize: '18px' }}>{t('dashboard.customsAndMrn')}</h3>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>{t('dashboard.pendingDeclarations')}:</span>
            <strong style={{ color: '#f39c12' }}>{data.customs.pendingDeclarations}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>{t('dashboard.activeMrns')}:</span>
            <strong>{data.customs.activeMRNs}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span>{t('dashboard.expiringMrns')}:</span>
            <strong style={{ color: '#e74c3c' }}>{data.customs.expiringMRNs}</strong>
          </div>
        </div>

        <div className="card">
          <h3 style={{ marginBottom: '15px', fontSize: '18px' }}>{t('dashboard.guaranteesSection')}</h3>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>{t('dashboard.totalAccounts')}:</span>
            <strong>{data.guarantees.totalAccounts}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>{t('dashboard.activeGuarantees')}:</span>
            <strong style={{ color: '#f39c12' }}>{data.guarantees.activeGuarantees}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span>{t('dashboard.totalExposure')}:</span>
            <strong style={{ color: '#e74c3c' }}>{data.guarantees.totalExposure.toFixed(2)}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span>{t('dashboard.expiringSoon')}:</span>
            <strong>{data.guarantees.expiringGuarantees}</strong>
          </div>
        </div>
      </div>
      </div>
    </div>
  );
};

export default Dashboard;
