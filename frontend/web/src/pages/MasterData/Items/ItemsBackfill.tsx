import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Box,
  Paper,
  Typography,
  Button,
  Stack,
  Alert,
  LinearProgress,
  Grid,
  List,
  ListItem,
  ListItemText,
  Chip,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
} from '@mui/material';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import PreviewIcon from '@mui/icons-material/Preview';
import { itemsAdminApi } from '../../../services/api';
import { showError, showSuccess } from '../../../utils/toast';

/**
 * P6.30 consumer — admin page to decompose legacy item SKUs into
 * Base/Color/Size/ParentItemId. Dry-run first, then explicit execute with
 * confirm dialog to avoid accidental writes.
 */

interface BackfillResult {
  itemsScanned: number;
  variantsBackfilled: number;
  baseItemsCreated: number;
  untouchedBaseCodeAlreadyPresent: number;
  sampleChanges: string[];
}

const ItemsBackfill: React.FC = () => {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(false);
  const [lastMode, setLastMode] = useState<'dry' | 'exec' | null>(null);
  const [result, setResult] = useState<BackfillResult | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);

  const run = async (dryRun: boolean) => {
    setLoading(true);
    setLastMode(dryRun ? 'dry' : 'exec');
    try {
      const resp = await itemsAdminApi.backfillBaseVariants(dryRun);
      setResult(resp.data as BackfillResult);
      if (!dryRun) showSuccess(t('itemsBackfill.execute'));
    } catch (err: any) {
      showError(
        err?.response?.data?.errorMessage ||
          err?.response?.data ||
          'Backfill failed'
      );
    } finally {
      setLoading(false);
      setConfirmOpen(false);
    }
  };

  return (
    <Box sx={{ p: 3, maxWidth: 1100, mx: 'auto' }}>
      <Typography variant="h4" gutterBottom>
        🔧 {t('itemsBackfill.title')}
      </Typography>
      <Typography variant="body2" color="text.secondary" paragraph>
        {t('itemsBackfill.subtitle')}
      </Typography>

      <Paper sx={{ p: 2, mb: 2 }}>
        <Stack direction="row" spacing={2}>
          <Button
            variant="outlined"
            startIcon={<PreviewIcon />}
            disabled={loading}
            onClick={() => run(true)}
          >
            {t('itemsBackfill.runDryRun')}
          </Button>
          <Button
            variant="contained"
            color="warning"
            startIcon={<PlayArrowIcon />}
            disabled={loading || !result || result.variantsBackfilled + result.baseItemsCreated === 0}
            onClick={() => setConfirmOpen(true)}
          >
            {t('itemsBackfill.runExecute')}
          </Button>
        </Stack>
      </Paper>

      {loading && <LinearProgress sx={{ mb: 2 }} />}

      {result && (
        <>
          <Alert severity={lastMode === 'exec' ? 'success' : 'info'} sx={{ mb: 2 }}>
            {lastMode === 'dry' ? t('itemsBackfill.dryRun') : t('itemsBackfill.execute')}
          </Alert>

          <Grid container spacing={2} sx={{ mb: 2 }}>
            <Stat label={t('itemsBackfill.itemsScanned')} value={result.itemsScanned} />
            <Stat label={t('itemsBackfill.variantsBackfilled')} value={result.variantsBackfilled} color="primary" />
            <Stat label={t('itemsBackfill.baseItemsCreated')} value={result.baseItemsCreated} color="success" />
            <Stat label={t('itemsBackfill.untouched')} value={result.untouchedBaseCodeAlreadyPresent} />
          </Grid>

          {result.sampleChanges.length > 0 && (
            <Paper sx={{ p: 2 }}>
              <Typography variant="subtitle2" gutterBottom>
                {t('itemsBackfill.sampleChanges')}
              </Typography>
              <List dense>
                {result.sampleChanges.map((s, i) => (
                  <ListItem key={i} disableGutters>
                    <ListItemText
                      primary={s}
                      primaryTypographyProps={{ fontFamily: 'monospace', fontSize: 13 }}
                    />
                  </ListItem>
                ))}
              </List>
            </Paper>
          )}
        </>
      )}

      <Dialog open={confirmOpen} onClose={() => setConfirmOpen(false)}>
        <DialogTitle>{t('itemsBackfill.confirmExecute')}</DialogTitle>
        <DialogContent>
          <Typography variant="body2">
            {result && (
              <>
                {t('itemsBackfill.variantsBackfilled')}: <strong>{result.variantsBackfilled}</strong>
                <br />
                {t('itemsBackfill.baseItemsCreated')}: <strong>{result.baseItemsCreated}</strong>
              </>
            )}
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmOpen(false)}>{t('common.cancel')}</Button>
          <Button
            variant="contained"
            color="warning"
            onClick={() => run(false)}
            disabled={loading}
          >
            {t('itemsBackfill.execute')}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

const Stat: React.FC<{ label: string; value: number; color?: 'primary' | 'success' }> = ({
  label,
  value,
  color,
}) => (
  <Grid item xs={6} sm={3}>
    <Paper sx={{ p: 2, textAlign: 'center' }}>
      <Chip
        label={value.toLocaleString()}
        color={color ?? 'default'}
        sx={{ fontSize: 18, py: 2, px: 1, mb: 1 }}
      />
      <Typography variant="caption" display="block" color="text.secondary">
        {label}
      </Typography>
    </Paper>
  </Grid>
);

export default ItemsBackfill;
