import React from 'react';
import { useTranslation } from 'react-i18next';
import { SUPPORTED_LANGUAGES } from '../i18n/i18n';

/**
 * Language dropdown. Persists selection via i18next-browser-languagedetector
 * (localStorage key: `lon.lang`). Place in the sidebar / header.
 */
const LanguageSwitcher: React.FC<{ compact?: boolean }> = ({ compact = false }) => {
  const { i18n, t } = useTranslation();

  const handleChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    i18n.changeLanguage(e.target.value);
  };

  return (
    <div className={`language-switcher ${compact ? 'compact' : ''}`}>
      {!compact && <label>{t('nav.language')}</label>}
      <select value={i18n.language} onChange={handleChange} aria-label={t('nav.language')}>
        {SUPPORTED_LANGUAGES.map(lang => (
          <option key={lang.code} value={lang.code}>
            {lang.flag} {lang.name}
          </option>
        ))}
      </select>
    </div>
  );
};

export default LanguageSwitcher;
