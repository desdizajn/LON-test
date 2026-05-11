import React from 'react';
import { Alert } from '@mui/material';
import { useTranslation } from 'react-i18next';

/**
 * Banner mounted on pages whose business data still lives in browser
 * localStorage. Tells the user that data may be lost on cache clear.
 *
 * P16.A2 added this to 6 pages; P16.C1/C2/C3 remove it as each page
 * is migrated to a real backend entity.
 */
const LocalStorageWarningBanner: React.FC = () => {
  const { t } = useTranslation();
  return (
    <Alert severity="warning" sx={{ mb: 2 }}>
      {t('common.localStorageWarning')}
    </Alert>
  );
};

export default LocalStorageWarningBanner;
