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
import { commercialInvoicesApi } from '../../services/api';

interface CommercialInvoiceRow {
  id: string;
  number: string;
  clientOrderId?: string | null;
  clientOrderNumber?: string | null;
  shipmentId?: string | null;
  shipmentNumber?: string | null;
  consigneePartnerId: string;
  consigneeName?: string | null;
  consignorName?: string | null;
  invoiceDate: string;
  currency: string;
  totalAmount: number;
  status: number;
  statusName: string;
  incoterms: string;
}

const STATUS_COLOR: Record<number, 'default' | 'info' | 'success' | 'error'> = {
  1: 'default',
  2: 'success',
  3: 'error',
};

const STATUS_LABEL_KEY: Record<number, string> = {
  1: 'commercialInvoices.statuses.draft',
  2: 'commercialInvoices.statuses.issued',
  3: 'commercialInvoices.statuses.cancelled',
};

const CommercialInvoiceList: React.FC = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [rows, setRows] = useState<CommercialInvoiceRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const resp = await commercialInvoicesApi.getList({
        status: statusFilter ? Number(statusFilter) : undefined,
      });
      setRows((resp.data?.data ?? []) as CommercialInvoiceRow[]);
    } catch (err: any) {
      setError(
        err?.response?.data?.errorMessage ??
          err?.message ??
          'Failed to load commercial invoices.',
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [statusFilter]);

  const totalAmount = useMemo(
    () => rows.reduce((s, r) => s + (r.totalAmount ?? 0), 0),
    [rows],
  );

  return (
    <Box p={3}>
      <Stack direction="row" alignItems="center" justifyContent="space-between" mb={2}>
        <Typography variant="h5">{t('commercialInvoices.title')}</Typography>
        <Stack direction="row" spacing={1.5}>
          <TextField
            select
            size="small"
            label={t('commercialInvoices.filters.status')}
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            sx={{ minWidth: 180 }}
          >
            <MenuItem value="">{t('common.all')}</MenuItem>
            <MenuItem value="1">{t('commercialInvoices.statuses.draft')}</MenuItem>
            <MenuItem value="2">{t('commercialInvoices.statuses.issued')}</MenuItem>
            <MenuItem value="3">{t('commercialInvoices.statuses.cancelled')}</MenuItem>
          </TextField>
          <Button onClick={load} variant="outlined" size="small">
            {t('common.refresh')}
          </Button>
        </Stack>
      </Stack>

      <Typography variant="caption" color="text.secondary" sx={{ mb: 2, display: 'block' }}>
        {t('commercialInvoices.summary', { rows: rows.length, total: totalAmount.toFixed(2) })}
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
          <Typography color="text.secondary">{t('commercialInvoices.empty')}</Typography>
        </Paper>
      )}

      {rows.length > 0 && (
        <Paper sx={{ overflow: 'hidden' }}>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: '1.2fr 1fr 1.4fr 1.4fr 0.8fr 0.9fr 0.7fr 0.7fr',
              fontSize: 13,
            }}
          >
            {[
              t('commercialInvoices.cols.number'),
              t('commercialInvoices.cols.invoiceDate'),
              t('commercialInvoices.cols.consignor'),
              t('commercialInvoices.cols.consignee'),
              t('commercialInvoices.cols.incoterms'),
              t('commercialInvoices.cols.total'),
              t('commercialInvoices.cols.currency'),
              t('commercialInvoices.cols.status'),
            ].map((h, i) => (
              <Box
                key={i}
                sx={{
                  fontWeight: 600,
                  p: 1,
                  borderBottom: 1,
                  borderColor: 'divider',
                  bgcolor: 'background.default',
                  textAlign: i === 5 ? 'right' : 'left',
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
                  onClick={() => navigate(`/customs/commercial-invoices/${r.id}`)}
                >
                  {r.number}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                  {new Date(r.invoiceDate).toLocaleDateString('mk-MK')}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                  {r.consignorName ?? '—'}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                  {r.consigneeName ?? '—'}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                  {r.incoterms ?? '—'}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
                  {(r.totalAmount ?? 0).toFixed(2)}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>{r.currency}</Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                  <Chip
                    size="small"
                    label={t(STATUS_LABEL_KEY[r.status] ?? 'commercialInvoices.statuses.draft')}
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

export default CommercialInvoiceList;
