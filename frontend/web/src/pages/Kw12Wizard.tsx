import React, { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import {
  Box,
  Paper,
  Typography,
  Button,
  Stack,
  Chip,
  LinearProgress,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Alert,
  Divider,
} from '@mui/material';
import CloudUploadIcon from '@mui/icons-material/CloudUpload';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';
import { importApi } from '../services/api';
import { showError } from '../utils/toast';

/**
 * P6.34 consumer — single-file upload of KW12.xlsx → auto-creates 3 import
 * sessions (Items / CustomsDeclarations / Receipts) with TargetEntity
 * pre-set. Each session links back into the generic ImportWizard for
 * mapping + dry-run + commit.
 */

interface Kw12Response {
  itemsSessionId: string | null;
  customsDeclarationsSessionId: string | null;
  receiptsSessionId: string | null;
  sheetsFound: string[];
  sheetsSkipped: string[];
  suggestedDefaults: string[];
}

const Kw12Wizard: React.FC = () => {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<Kw12Response | null>(null);
  const [fileName, setFileName] = useState<string>('');
  const inputRef = useRef<HTMLInputElement>(null);

  const handleFile = async (file: File) => {
    setLoading(true);
    setFileName(file.name);
    try {
      const resp = await importApi.uploadKw12Preset(file);
      // Envelope: { isSuccess, data, ... }
      const payload = (resp.data?.data ?? resp.data) as Kw12Response;
      setResult({
        itemsSessionId: payload.itemsSessionId ?? null,
        customsDeclarationsSessionId: payload.customsDeclarationsSessionId ?? null,
        receiptsSessionId: payload.receiptsSessionId ?? null,
        sheetsFound: payload.sheetsFound ?? [],
        sheetsSkipped: payload.sheetsSkipped ?? [],
        suggestedDefaults: payload.suggestedDefaults ?? [],
      });
    } catch (err: any) {
      showError(
        err?.response?.data?.errorMessage ||
          err?.response?.data ||
          'KW12 upload failed'
      );
      setResult(null);
    } finally {
      setLoading(false);
    }
  };

  const reset = () => {
    setResult(null);
    setFileName('');
    if (inputRef.current) inputRef.current.value = '';
  };

  return (
    <Box sx={{ p: 3, maxWidth: 900, mx: 'auto' }}>
      <Typography variant="h4" gutterBottom>
        📦 {t('kw12Wizard.title')}
      </Typography>
      <Typography variant="body2" color="text.secondary" paragraph>
        {t('kw12Wizard.subtitle')}
      </Typography>

      {!result && !loading && (
        <Paper
          sx={{
            p: 6,
            textAlign: 'center',
            border: '2px dashed',
            borderColor: 'primary.light',
            bgcolor: 'action.hover',
          }}
        >
          <CloudUploadIcon sx={{ fontSize: 64, color: 'primary.main', mb: 2 }} />
          <Typography variant="h6" gutterBottom>
            {t('kw12Wizard.uploadPrompt')}
          </Typography>
          <input
            ref={inputRef}
            type="file"
            accept=".xlsx,.xls"
            hidden
            onChange={(e) => {
              const f = e.target.files?.[0];
              if (f) handleFile(f);
            }}
          />
          <Button
            variant="contained"
            size="large"
            startIcon={<CloudUploadIcon />}
            onClick={() => inputRef.current?.click()}
            sx={{ mt: 2 }}
          >
            {t('kw12Wizard.uploadPrompt')}
          </Button>
        </Paper>
      )}

      {loading && (
        <Paper sx={{ p: 4, textAlign: 'center' }}>
          <Typography variant="body1" sx={{ mb: 2 }}>
            {t('kw12Wizard.uploading')} {fileName && `— ${fileName}`}
          </Typography>
          <LinearProgress />
        </Paper>
      )}

      {result && (
        <Stack spacing={2}>
          <Alert severity="success" icon={<CheckCircleIcon />}>
            {t('kw12Wizard.sheetsFound')}: {result.sheetsFound.length} | {t('kw12Wizard.sessionsCreated')}:{' '}
            {[result.itemsSessionId, result.customsDeclarationsSessionId, result.receiptsSessionId].filter(Boolean).length}
          </Alert>

          {result.sheetsFound.length > 0 && (
            <Paper sx={{ p: 2 }}>
              <Typography variant="subtitle2" gutterBottom>
                {t('kw12Wizard.sheetsFound')}
              </Typography>
              <Stack direction="row" spacing={1} flexWrap="wrap">
                {result.sheetsFound.map((s, i) => (
                  <Chip key={i} size="small" label={s} color="primary" variant="outlined" />
                ))}
              </Stack>
            </Paper>
          )}

          {result.sheetsSkipped.length > 0 && (
            <Paper sx={{ p: 2 }}>
              <Typography variant="subtitle2" gutterBottom>
                {t('kw12Wizard.sheetsSkipped')}
              </Typography>
              <Stack direction="row" spacing={1} flexWrap="wrap">
                {result.sheetsSkipped.map((s, i) => (
                  <Chip key={i} size="small" label={s} variant="outlined" />
                ))}
              </Stack>
            </Paper>
          )}

          <Paper sx={{ p: 2 }}>
            <Typography variant="subtitle2" gutterBottom>
              {t('kw12Wizard.sessionsCreated')}
            </Typography>
            <List dense>
              {result.itemsSessionId && (
                <ListItem
                  secondaryAction={
                    <Button
                      component={RouterLink}
                      to={`/tools/import?session=${result.itemsSessionId}`}
                      size="small"
                      endIcon={<OpenInNewIcon />}
                    >
                      {t('kw12Wizard.openItemsSession')}
                    </Button>
                  }
                >
                  <ListItemIcon>
                    <CheckCircleIcon color="success" />
                  </ListItemIcon>
                  <ListItemText
                    primary="Items (Matriks)"
                    secondary={result.itemsSessionId}
                  />
                </ListItem>
              )}
              {result.customsDeclarationsSessionId && (
                <ListItem
                  secondaryAction={
                    <Button
                      component={RouterLink}
                      to={`/tools/import?session=${result.customsDeclarationsSessionId}`}
                      size="small"
                      endIcon={<OpenInNewIcon />}
                    >
                      {t('kw12Wizard.openCustomsSession')}
                    </Button>
                  }
                >
                  <ListItemIcon>
                    <CheckCircleIcon color="success" />
                  </ListItemIcon>
                  <ListItemText
                    primary="CustomsDeclarations (Faktura)"
                    secondary={result.customsDeclarationsSessionId}
                  />
                </ListItem>
              )}
              {result.receiptsSessionId && (
                <ListItem
                  secondaryAction={
                    <Button
                      component={RouterLink}
                      to={`/tools/import?session=${result.receiptsSessionId}`}
                      size="small"
                      endIcon={<OpenInNewIcon />}
                    >
                      {t('kw12Wizard.openReceiptsSession')}
                    </Button>
                  }
                >
                  <ListItemIcon>
                    <CheckCircleIcon color="success" />
                  </ListItemIcon>
                  <ListItemText
                    primary="Receipts (Transport)"
                    secondary={result.receiptsSessionId}
                  />
                </ListItem>
              )}
            </List>
          </Paper>

          {result.suggestedDefaults.length > 0 && (
            <Paper sx={{ p: 2 }}>
              <Typography variant="subtitle2" gutterBottom>
                {t('kw12Wizard.suggestedDefaults')}
              </Typography>
              <List dense>
                {result.suggestedDefaults.map((d, i) => (
                  <ListItem key={i} disableGutters>
                    <ListItemText primary={d} />
                  </ListItem>
                ))}
              </List>
            </Paper>
          )}

          <Divider />

          <Box sx={{ textAlign: 'center' }}>
            <Button variant="outlined" onClick={reset}>
              {t('kw12Wizard.uploadAnother')}
            </Button>
          </Box>
        </Stack>
      )}
    </Box>
  );
};

export default Kw12Wizard;
