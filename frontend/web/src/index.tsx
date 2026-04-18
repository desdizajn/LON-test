import React from 'react';
import ReactDOM from 'react-dom/client';
import './index.css';
import './i18n/i18n'; // initialize i18next before any component using useTranslation
import App from './App';

const root = ReactDOM.createRoot(
  document.getElementById('root') as HTMLElement
);
root.render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
