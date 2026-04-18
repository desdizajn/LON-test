import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';

import mk from './locales/mk.json';
import sr from './locales/sr.json';
import sq from './locales/sq.json';
import en from './locales/en.json';

export const SUPPORTED_LANGUAGES = [
  { code: 'mk', name: 'Македонски', flag: '🇲🇰' },
  { code: 'sr', name: 'Српски', flag: '🇷🇸' },
  { code: 'sq', name: 'Shqip', flag: '🇦🇱' },
  { code: 'en', name: 'English', flag: '🇬🇧' },
] as const;

export type LanguageCode = typeof SUPPORTED_LANGUAGES[number]['code'];

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      mk: { translation: mk },
      sr: { translation: sr },
      sq: { translation: sq },
      en: { translation: en },
    },
    fallbackLng: 'mk',
    supportedLngs: SUPPORTED_LANGUAGES.map(l => l.code),
    interpolation: { escapeValue: false },
    detection: {
      order: ['localStorage', 'navigator'],
      caches: ['localStorage'],
      lookupLocalStorage: 'lon.lang',
    },
    returnEmptyString: false,
  });

export default i18n;
