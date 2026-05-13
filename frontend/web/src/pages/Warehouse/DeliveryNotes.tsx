import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { logisticsApi } from '../../services/api';

interface DeliveryNoteRow {
  id: string;
  number: string;
  documentType: number;
  documentTypeName: string;
  dispatchDate: string;
  status: number;
  statusName: string;
  fromLocationId: string;
  toPartnerId?: string | null;
  toLocationId?: string | null;
  driverName?: string | null;
  vehicleRegistration?: string | null;
  lines: Array<{ id: string; quantity: number }>;
}

const STATUS_COLOR: Record<number, 'default' | 'info' | 'success' | 'error'> = {
  1: 'default', // Draft
  2: 'info',    // Sent
  3: 'success', // Confirmed
  4: 'error',   // Cancelled
};

const TYPE_LABEL_KEY: Record<number, string> = {
  1: 'deliveryNotes.types.producerDispatch',
  2: 'deliveryNotes.types.producerReturn',
  3: 'deliveryNotes.types.customerShipment',
};

const STATUS_LABEL_KEY: Record<number, string> = {
  1: 'deliveryNotes.statuses.draft',
  2: 'deliveryNotes.statuses.sent',
  3: 'deliveryNotes.statuses.confirmed',
  4: 'deliveryNotes.statuses.cancelled',
};

const DeliveryNotes: React.FC = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [rows, setRows] = useState<DeliveryNoteRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [typeFilter, setTypeFilter] = useState<string>('');
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const resp = await logisticsApi.getDeliveryNotes({
        type: typeFilter ? Number(typeFilter) : undefined,
        status: statusFilter ? Number(statusFilter) : undefined,
      });
      setRows((resp.data?.data ?? []) as DeliveryNoteRow[]);
    } catch (err: any) {
      setError(err?.response?.data?.errorMessage ?? err?.message ?? 'Failed to load delivery notes.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [typeFilter, statusFilter]);

  const totalLines = useMemo(
    () => rows.reduce((s, r) => s + (r.lines?.length ?? 0), 0),
    [rows],
  );

  return (
    <Box p={3}>
      <Stack direction="row" alignItems="center" justifyContent="space-between" mb={2}>
        <Typography variant="h5">{t('deliveryNotes.title')}</Typography>
        <Stack direction="row" spacing={1.5}>
          <TextField
            select
            size="small"
            label={t('deliveryNotes.filters.type')}
            value={typeFilter}
            onChange={(e) => setTypeFilter(e.target.value)}
            sx={{ minWidth: 220 }}
          >
            <MenuItem value="">{t('common.all')}</MenuItem>
            <MenuItem value="1">{t('deliveryNotes.types.producerDispatch')}</MenuItem>
            <MenuItem value="2">{t('deliveryNotes.types.producerReturn')}</MenuItem>
            <MenuItem value="3">{t('deliveryNotes.types.customerShipment')}</MenuItem>
          </TextField>
          <TextField
            select
            size="small"
            label={t('deliveryNotes.filters.status')}
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            sx={{ minWidth: 180 }}
          >
            <MenuItem value="">{t('common.all')}</MenuItem>
            <MenuItem value="1">{t('deliveryNotes.statuses.draft')}</MenuItem>
            <MenuItem value="2">{t('deliveryNotes.statuses.sent')}</MenuItem>
            <MenuItem value="3">{t('deliveryNotes.statuses.confirmed')}</MenuItem>
            <MenuItem value="4">{t('deliveryNotes.statuses.cancelled')}</MenuItem>
          </TextField>
          <Button onClick={load} variant="outlined" size="small">{t('common.refresh')}</Button>
        </Stack>
      </Stack>

      <Typography variant="caption" color="text.secondary" sx={{ mb: 2, display: 'block' }}>
        {t('deliveryNotes.summary', { rows: rows.length, lines: totalLines })}
      </Typography>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {loading && (
        <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 2 }}>
          <CircularProgress size={16} />
          <Typography variant="caption">{t('common.loading')}</Typography>
        </Stack>
      )}

      {!loading && rows.length === 0 && !error && (
        <Paper sx={{ p: 3, textAlign: 'center' }}>
          <Typography color="text.secondary">{t('deliveryNotes.empty')}</Typography>
        </Paper>
      )}

      {rows.length > 0 && (
        <Paper sx={{ overflow: 'hidden' }}>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: '1.2fr 1fr 1.4fr 0.8fr 0.7fr 0.6fr 0.6fr',
              fontSize: 13,
            }}
          >
            {[
              t('deliveryNotes.cols.number'),
              t('deliveryNotes.cols.type'),
              t('deliveryNotes.cols.relatedDoc'),
              t('deliveryNotes.cols.driver'),
              t('deliveryNotes.cols.dispatchDate'),
              t('deliveryNotes.cols.lines'),
              t('deliveryNotes.cols.status'),
            ].map((h, i) => (
              <Box
                key={i}
                sx={{
                  fontWeight: 600,
                  p: 1,
                  borderBottom: 1,
                  borderColor: 'divider',
                  bgcolor: 'background.default',
                  textAlign: i >= 5 && i <= 5 ? 'right' : 'left',
                }}
              >
                {h}
              </Box>
            ))}
            {rows.map((r) => (
              <React.Fragment key={r.id}>
                <Box
                  sx={{
                    p: 1,
                    borderBottom: 1,
                    borderColor: 'divider',
                    fontFamily: 'monospace',
                    cursor: 'pointer',
                    color: 'primary.main',
                  }}
                  onClick={() => navigate(`/warehouse/delivery-notes/${r.id}`)}
                >
                  {r.number}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                  {t(TYPE_LABEL_KEY[r.documentType] ?? 'deliveryNotes.types.producerDispatch')}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace', fontSize: 11 }}>
                  {/* relatedDocumentId truncated for screen; full id visible on detail */}
                  {String((r as unknown as { relatedDocumentId?: string }).relatedDocumentId ?? '').slice(0, 8)}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                  {r.driverName ?? '—'}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                  {new Date(r.dispatchDate).toLocaleDateString('mk-MK')}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
                  {r.lines?.length ?? 0}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                  <Chip
                    size="small"
                    label={t(STATUS_LABEL_KEY[r.status] ?? 'deliveryNotes.statuses.draft')}
                    color={STATUS_COLOR[r.status] ?? 'default'}
                  />
                </Box>
              </React.Fragment>
            ))}
          </Box>
        </Paper>
      )}
    </Box>
  );
};

export default DeliveryNotes;
