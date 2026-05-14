import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  MenuItem,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import AddIcon from '@mui/icons-material/Add';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate } from '../../utils/format';

/**
 * Phase 17 §E16 — manual FX rate maintenance (BLUEPRINT §5.14.8).
 * Auto-import is Phase 27.1. For v1 the admin types in daily rates.
 *
 * "Copy forward" shortcut clones the most-recent row of the same currency
 * pair with today's EffectiveDate, so the typical "yesterday's rate +
 * small drift" workflow is one-click.
 */

interface FxRateRow {
  id: string;
  fromCurrency: string;
  toCurrency: string;
  rate: number;
  effectiveDate: string;
  source: number;
  sourceName: string;
  notes?: string | null;
}

const CURRENCY_OPTIONS = ['EUR', 'MKD', 'USD', 'RSD', 'GBP'];
const SOURCE_OPTIONS = [
  { value: 1, key: 'manual' },
  { value: 2, key: 'nationalBank' },
];

interface EditFormState {
  id?: string;
  fromCurrency: string;
  toCurrency: string;
  rate: string;
  effectiveDate: string;
  source: number;
  notes: string;
}

const FxRates: React.FC = () => {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editForm, setEditForm] = useState<EditFormState | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [filterFrom, setFilterFrom] = useState('');
  const [filterTo, setFilterTo] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['fx-rates', filterFrom, filterTo],
    queryFn: async () => {
      const params: Record<string, string> = {};
      if (filterFrom) params.from = filterFrom;
      if (filterTo) params.to = filterTo;
      const resp = await api.get('/Finance/fx-rates', { params });
      const env = resp.data as { data?: FxRateRow[] };
      return env?.data ?? (resp.data as FxRateRow[]);
    },
  });

  const createMutation = useMutation({
    mutationFn: (payload: Omit<EditFormState, 'id'>) => api.post('/Finance/fx-rates', {
      fromCurrency: payload.fromCurrency,
      toCurrency: payload.toCurrency,
      rate: Number(payload.rate),
      effectiveDate: payload.effectiveDate,
      source: payload.source,
      notes: payload.notes || null,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['fx-rates'] }); setDialogOpen(false); setEditForm(null); },
    onError: (err) => setError(translateError(err)),
  });

  const updateMutation = useMutation({
    mutationFn: (payload: EditFormState) => api.put(`/Finance/fx-rates/${payload.id}`, {
      id: payload.id,
      rate: Number(payload.rate),
      effectiveDate: payload.effectiveDate,
      source: payload.source,
      notes: payload.notes || null,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['fx-rates'] }); setDialogOpen(false); setEditForm(null); },
    onError: (err) => setError(translateError(err)),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.delete(`/Finance/fx-rates/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['fx-rates'] }),
    onError: (err) => setError(translateError(err)),
  });

  const handleAdd = () => {
    setEditForm({
      fromCurrency: 'EUR',
      toCurrency: 'MKD',
      rate: '',
      effectiveDate: new Date().toISOString().slice(0, 10),
      source: 1,
      notes: '',
    });
    setDialogOpen(true);
  };

  const handleEdit = (row: FxRateRow) => {
    setEditForm({
      id: row.id,
      fromCurrency: row.fromCurrency,
      toCurrency: row.toCurrency,
      rate: String(row.rate),
      effectiveDate: row.effectiveDate.slice(0, 10),
      source: row.source,
      notes: row.notes ?? '',
    });
    setDialogOpen(true);
  };

  const handleCopyForward = (row: FxRateRow) => {
    setEditForm({
      fromCurrency: row.fromCurrency,
      toCurrency: row.toCurrency,
      rate: String(row.rate),
      effectiveDate: new Date().toISOString().slice(0, 10),
      source: row.source,
      notes: row.notes ?? '',
    });
    setDialogOpen(true);
  };

  const handleSubmit = () => {
    if (!editForm) return;
    setError(null);
    if (editForm.id) {
      updateMutation.mutate(editForm);
    } else {
      createMutation.mutate(editForm);
    }
  };

  const rows = data ?? [];

  return (
    <Box p={3}>
      <Typography variant="h4" gutterBottom>{t('fxRates.title')}</Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        {t('fxRates.subtitle')}
      </Typography>

      <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 2 }}>
        <TextField
          size="small"
          label={t('fxRates.filter.from')}
          value={filterFrom}
          onChange={(e) => setFilterFrom(e.target.value.toUpperCase())}
          select
          sx={{ minWidth: 100 }}
        >
          <MenuItem value="">{t('common.all')}</MenuItem>
          {CURRENCY_OPTIONS.map(c => <MenuItem key={c} value={c}>{c}</MenuItem>)}
        </TextField>
        <TextField
          size="small"
          label={t('fxRates.filter.to')}
          value={filterTo}
          onChange={(e) => setFilterTo(e.target.value.toUpperCase())}
          select
          sx={{ minWidth: 100 }}
        >
          <MenuItem value="">{t('common.all')}</MenuItem>
          {CURRENCY_OPTIONS.map(c => <MenuItem key={c} value={c}>{c}</MenuItem>)}
        </TextField>
        <Box flex={1} />
        <Button variant="contained" startIcon={<AddIcon />} onClick={handleAdd}>
          {t('fxRates.add')}
        </Button>
      </Stack>

      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>{error}</Alert>}

      {isLoading ? <Typography>{t('common.loading')}</Typography> : (
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>{t('fxRates.col.pair')}</TableCell>
                <TableCell align="right">{t('fxRates.col.rate')}</TableCell>
                <TableCell>{t('fxRates.col.effective')}</TableCell>
                <TableCell>{t('fxRates.col.source')}</TableCell>
                <TableCell>{t('fxRates.col.notes')}</TableCell>
                <TableCell align="right">{t('common.actions')}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.map((row) => (
                <TableRow key={row.id} hover>
                  <TableCell>{row.fromCurrency} → {row.toCurrency}</TableCell>
                  <TableCell align="right">{Number(row.rate).toFixed(6)}</TableCell>
                  <TableCell>{formatDate(row.effectiveDate)}</TableCell>
                  <TableCell>{t(`fxRates.source.${SOURCE_OPTIONS.find(s => s.value === row.source)?.key ?? 'manual'}`)}</TableCell>
                  <TableCell>{row.notes ?? '—'}</TableCell>
                  <TableCell align="right">
                    <IconButton size="small" title={t('fxRates.copyForward') as string} onClick={() => handleCopyForward(row)}>
                      <ContentCopyIcon fontSize="small" />
                    </IconButton>
                    <IconButton size="small" onClick={() => handleEdit(row)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                    <IconButton size="small" color="error" onClick={() => deleteMutation.mutate(row.id)}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog open={dialogOpen} onClose={() => { setDialogOpen(false); setEditForm(null); }} maxWidth="sm" fullWidth>
        <DialogTitle>{editForm?.id ? t('fxRates.editTitle') : t('fxRates.addTitle')}</DialogTitle>
        <DialogContent>
          {editForm && (
            <Stack spacing={2} sx={{ mt: 1 }}>
              <Stack direction="row" spacing={2}>
                <TextField
                  fullWidth select label={t('fxRates.form.from')}
                  value={editForm.fromCurrency}
                  onChange={(e) => setEditForm({ ...editForm, fromCurrency: e.target.value })}
                  disabled={!!editForm.id}
                >
                  {CURRENCY_OPTIONS.map(c => <MenuItem key={c} value={c}>{c}</MenuItem>)}
                </TextField>
                <TextField
                  fullWidth select label={t('fxRates.form.to')}
                  value={editForm.toCurrency}
                  onChange={(e) => setEditForm({ ...editForm, toCurrency: e.target.value })}
                  disabled={!!editForm.id}
                >
                  {CURRENCY_OPTIONS.map(c => <MenuItem key={c} value={c}>{c}</MenuItem>)}
                </TextField>
              </Stack>
              <TextField
                fullWidth label={t('fxRates.form.rate')}
                value={editForm.rate}
                onChange={(e) => setEditForm({ ...editForm, rate: e.target.value })}
                type="number"
                inputProps={{ step: 0.0001 }}
              />
              <TextField
                fullWidth label={t('fxRates.form.effective')}
                value={editForm.effectiveDate}
                onChange={(e) => setEditForm({ ...editForm, effectiveDate: e.target.value })}
                type="date"
                InputLabelProps={{ shrink: true }}
              />
              <TextField
                fullWidth select label={t('fxRates.form.source')}
                value={editForm.source}
                onChange={(e) => setEditForm({ ...editForm, source: Number(e.target.value) })}
              >
                {SOURCE_OPTIONS.map(s => (
                  <MenuItem key={s.value} value={s.value}>{t(`fxRates.source.${s.key}`)}</MenuItem>
                ))}
              </TextField>
              <TextField
                fullWidth label={t('fxRates.form.notes')}
                value={editForm.notes}
                onChange={(e) => setEditForm({ ...editForm, notes: e.target.value })}
                multiline rows={2}
              />
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => { setDialogOpen(false); setEditForm(null); }}>{t('common.cancel')}</Button>
          <Button variant="contained" onClick={handleSubmit} disabled={createMutation.isPending || updateMutation.isPending}>
            {t('common.save')}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default FxRates;
