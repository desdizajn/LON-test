import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { authService } from '../services/authService';
import LanguageSwitcher from '../components/LanguageSwitcher';
import './Login.css';

const Login: React.FC = () => {
  const { t } = useTranslation();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      await authService.login({ username, password });
      navigate('/dashboard');
    } catch (err: any) {
      const status = err.response?.status;
      if (status === 401) setError(t('login.invalidCredentials'));
      else setError(err.response?.data?.message || t('login.serverError'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-container">
      <div className="login-hero">
        <img
          src={`${process.env.PUBLIC_URL}/taris-favicon.png`}
          alt=""
          aria-hidden="true"
          className="login-hero__bgmark"
        />
        <div className="login-hero__top">
          <div className="login-hero__mark">
            <img src={`${process.env.PUBLIC_URL}/taris-favicon.png`} alt="Taris" />
          </div>
          <div className="login-hero__wordmark">
            <strong>TARIS</strong>
            <span>LON management</span>
          </div>
        </div>

        <div className="login-hero__body">
          <h2>{t('login.heroTitle')}</h2>
          <p>{t('login.heroSubtitle')}</p>
          <div className="login-hero__pillars">
            <span className="login-hero__pillar">🏭 {t('login.pillars.production')}</span>
            <span className="login-hero__pillar">📦 {t('login.pillars.wms')}</span>
            <span className="login-hero__pillar">🛃 {t('login.pillars.customs')}</span>
            <span className="login-hero__pillar">💵 {t('login.pillars.finance')}</span>
            <span className="login-hero__pillar">📊 {t('login.pillars.kpis')}</span>
          </div>
        </div>

        <div className="login-hero__footer">
          © {new Date().getFullYear()} Elbosoft Consulting DOOEL
        </div>
      </div>

      <div className="login-form-side">
        <div className="login-box">
          <div className="login-header">
            <h1>{t('login.title')}</h1>
            <p>{t('login.subtitle')}</p>
          </div>

          <form onSubmit={handleSubmit} className="login-form">
            {error && (
              <div className="error-message">{error}</div>
            )}

            <div className="form-group">
              <label htmlFor="username">{t('login.username')}</label>
              <input
                type="text"
                id="username"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                required
                autoFocus
                disabled={loading}
              />
            </div>

            <div className="form-group">
              <label htmlFor="password">{t('login.password')}</label>
              <input
                type="password"
                id="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                disabled={loading}
              />
            </div>

            <button type="submit" className="login-button" disabled={loading}>
              {loading ? t('common.loading') : t('login.submit')}
            </button>
          </form>

          <div className="login-footer">
            <LanguageSwitcher />
            <p>{t('login.footer')}</p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Login;
