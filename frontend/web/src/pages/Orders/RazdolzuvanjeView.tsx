import React, { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  Grid,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import { toast } from 'react-toastify';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { clientOrdersApi } from '../../services/api';

interface RazdolzuvanjeLine {
  lineId: string;
  declarationId: string;
  declarationNumber: string;
  declarationType: string;
  mrn: string;
  declarationDate: string;
  lineNumber: number;
  itemCode?: string | null;
  itemName?: string | null;
  quantity: number;
  uoMCode?: string | null;
  dutyAmount: number;
  vatAmount: number;
  razdolzenaDaNe: boolean;
  razdolzenaAt?: string | null;
  razdolzenaBy?: string | null;
}

interface RazdolzuvanjeReport {
  clientOrderId: string;
  orderNumber: string;
  status: number;
  statusName: string;
  authorizationNumber?: string | null;
  totalImDuty: number;
  totalExDuty: number;
  totalWasteDuty: number;
  totalReturnDuty: number;
  totalCredited: number;
  variance: number;
  toleranceEur: number;
  isReconciled: boolean;
  totalLines: number;
  linesRazdolzeno: number;
  allLinesFlagged: boolean;
  lines: RazdolzuvanjeLine[];
}

const STATUS_COLOR: Record<number, 'default' | 'info' | 'warning' | 'success' | 'error'> = {
  0: 'default',
  1: 'info',
  2: 'warning',
  3: 'success',
  4: 'success',
  99: 'error',
};

const RazdolzuvanjeView: React.FC = () => {
  const { id = '' } = useParams<{ id: string }>();
  const { t } = useTranslation();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [submitting, setSubmitting] = useState(false);

  const { data: report, isLoading, error } = useQuery<RazdolzuvanjeReport>({
    queryKey: ['clientOrders', 'razdolzuvanje', id],
    queryFn: async () => {
      const resp = await clientOrdersApi.getRazdolzuvanje(id);
      return resp.data as RazdolzuvanjeReport;
    },
    enabled: !!id,
  });

  const toggleLine = async (line: RazdolzuvanjeLine) => {
    if (!report || report.status === 4 /* Closed */ || report.status === 99 /* Cancelled */) return;
    try {
      const resp = await clientOrdersApi.markRazdolzuvanjeLine(id, line.lineId, !line.razdolzenaDaNe);
      if (!resp.data?.isSuccess) {
        toast.error(resp.data?.errorMessage ?? (t('razdolzuvanje.toggleFailed') as string));
        return;
      }
      qc.invalidateQueries({ queryKey: ['clientOrders', 'razdolzuvanje', id] });
    } catch (err: any) {
      toast.error(err?.response?.data?.errorMessage ?? (t('razdolzuvanje.toggleFailed') as string));
    }
  };

  const handleSnapshot = async () => {
    if (!report) return;
    setSubmitting(true);
    try {
      const resp = await clientOrdersApi.takeRazdolzuvanjeSnapshot(id);
      const env = resp.data;
      if (!env?.isSuccess) {
        toast.error(env?.errorMessage ?? (t('razdolzuvanje.snapshotFailed') as string));
        return;
      }
      if (env.data?.closedClientOrder) {
        toast.success(t('razdolzuvanje.closed', { rows: env.data?.snapshotRowsCreated ?? 0 }) as string);
      } else {
        toast.success(
          t('razdolzuvanje.snapshotTaken', {
            rows: env.data?.snapshotRowsCreated ?? 0,
            variance: (env.data?.variance ?? 0).toFixed(2),
          }) as string,
        );
      }
      qc.invalidateQueries({ queryKey: ['clientOrders', 'razdolzuvanje', id] });
      qc.invalidateQueries({ queryKey: ['clientOrders', id] });
    } catch (err: any) {
      toast.error(err?.response?.data?.errorMessage ?? (t('razdolzuvanje.snapshotFailed') as string));
    } finally {
      setSubmitting(false);
    }
  };

  const handleDownloadPee060 = async () => {
    try {
      const resp = await clientOrdersApi.downloadRazdolzuvanjePee060(id);
      const url = window.URL.createObjectURL(new Blob([resp.data], { type: 'application/xml' }));
      const a = document.createElement('a');
      a.href = url;
      a.download = `PEE060_${report?.orderNumber ?? id}.xml`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
    } catch (err: any) {
      toast.error(err?.response?.data?.errorMessage ?? (t('razdolzuvanje.pee060Failed') as string));
    }
  };

  if (isLoading) return <Box p={3}><CircularProgress /></Box>;
  if (error || !report) {
    return (
      <Box p={3}>
        <Alert severity="error" sx={{ mb: 2 }}>
          {error instanceof Error ? error.message : t('razdolzuvanje.notFound')}
        </Alert>
        <Button onClick={() => navigate(`/orders/${id}`)}>{t('razdolzuvanje.backToHub')}</Button>
      </Box>
    );
  }

  const isLocked = report.status === 4 || report.status === 99;

  return (
    <Box p={3}>
      <Paper sx={{ p: 3, mb: 3 }}>
        <Stack direction="row" justifyContent="space-between" alignItems="center" mb={2}>
          <Stack direction="row" spacing={2} alignItems="center">
            <Typography variant="h5" sx={{ fontFamily: 'monospace' }}>{report.orderNumber}</Typography>
            <Chip label={report.statusName} color={STATUS_COLOR[report.status] ?? 'default'} />
            {report.authorizationNumber && (
              <Typography variant="caption" color="text.secondary">
                {t('razdolzuvanje.authorization')}: <b>{report.authorizationNumber}</b>
              </Typography>
            )}
          </Stack>
          <Stack direction="row" spacing={1}>
            <Button size="small" onClick={() => navigate(`/orders/${id}`)}>
              {t('razdolzuvanje.backToHub')}
            </Button>
            <Button
              size="small" variant="outlined"
              component="a"
              href={clientOrdersApi.razdolzuvanjePdfUrl(id)}
              target="_blank" rel="noopener noreferrer"
            >
              {t('razdolzuvanje.downloadPdf')}
            </Button>
            <Button size="small" variant="outlined" onClick={handleDownloadPee060}>
              {t('razdolzuvanje.downloadPee060')}
            </Button>
            <Button
              size="small"
              variant="contained"
              disabled={isLocked || submitting}
              onClick={handleSnapshot}
            >
              {t('razdolzuvanje.takeSnapshot')}
            </Button>
          </Stack>
        </Stack>

        <Grid container spacing={2}>
          <Grid item xs={6} sm={3}>
            <Paper sx={{ p: 2, textAlign: 'center', bgcolor: 'background.default' }}>
              <Typography variant="caption" color="text.secondary">
                {t('razdolzuvanje.totals.im')}
              </Typography>
              <Typography variant="h5">{report.totalImDuty.toFixed(2)}</Typography>
            </Paper>
          </Grid>
          <Grid item xs={6} sm={3}>
            <Paper sx={{ p: 2, textAlign: 'center', bgcolor: 'background.default' }}>
              <Typography variant="caption" color="text.secondary">
                {t('razdolzuvanje.totals.ex')}
              </Typography>
              <Typography variant="h5">{report.totalExDuty.toFixed(2)}</Typography>
            </Paper>
          </Grid>
          <Grid item xs={6} sm={3}>
            <Paper sx={{ p: 2, textAlign: 'center', bgcolor: 'background.default' }}>
              <Typography variant="caption" color="text.secondary">
                {t('razdolzuvanje.totals.waste')}
              </Typography>
              <Typography variant="h5">{report.totalWasteDuty.toFixed(2)}</Typography>
            </Paper>
          </Grid>
          <Grid item xs={6} sm={3}>
            <Paper sx={{ p: 2, textAlign: 'center', bgcolor: 'background.default' }}>
              <Typography variant="caption" color="text.secondary">
                {t('razdolzuvanje.totals.return')}
              </Typography>
              <Typography variant="h5">{report.totalReturnDuty.toFixed(2)}</Typography>
            </Paper>
          </Grid>
        </Grid>

        <Box sx={{ mt: 2, display: 'flex', gap: 4, alignItems: 'baseline', flexWrap: 'wrap' }}>
          <Typography variant="body2">
            {t('razdolzuvanje.totals.credited')}: <b>{report.totalCredited.toFixed(2)}</b>
          </Typography>
          <Typography
            variant="body2"
            color={report.isReconciled ? 'success.main' : 'error.main'}
          >
            {t('razdolzuvanje.totals.variance')}: <b>{report.variance.toFixed(4)}</b>
            {' '}({t('razdolzuvanje.tolerance', { eur: report.toleranceEur.toFixed(2) })})
            {' '}{report.isReconciled ? '✓' : '✗'}
          </Typography>
          <Typography variant="body2">
            {t('razdolzuvanje.totals.linesFlagged', {
              flagged: report.linesRazdolzeno,
              total: report.totalLines,
            })}
          </Typography>
        </Box>
      </Paper>

      <Paper sx={{ p: 2 }}>
        <Typography variant="h6" gutterBottom>{t('razdolzuvanje.linesTitle')}</Typography>
        {report.lines.length === 0 ? (
          <Typography variant="body2" color="text.secondary" align="center" sx={{ py: 4 }}>
            {t('razdolzuvanje.noLines')}
          </Typography>
        ) : (
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: '0.4fr 1fr 1.2fr 1.4fr 1.6fr 0.6fr 0.6fr 0.8fr 0.8fr 0.5fr',
              fontSize: 13,
            }}
          >
            {[
              '#',
              t('razdolzuvanje.cols.declaration'),
              t('razdolzuvanje.cols.mrn'),
              t('razdolzuvanje.cols.date'),
              t('razdolzuvanje.cols.item'),
              t('razdolzuvanje.cols.qty'),
              t('razdolzuvanje.cols.uom'),
              t('razdolzuvanje.cols.duty'),
              t('razdolzuvanje.cols.vat'),
              t('razdolzuvanje.cols.razd'),
            ].map((h, i) => (
              <Box
                key={i}
                sx={{
                  fontWeight: 600,
                  p: 1,
                  borderBottom: 1,
                  borderColor: 'divider',
                  bgcolor: 'background.default',
                  textAlign: i >= 5 && i <= 8 ? 'right' : 'left',
                }}
              >
                {h}
              </Box>
            ))}
            {report.lines.map((l) => (
              <React.Fragment key={l.lineId}>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>{l.lineNumber}</Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace', fontSize: 12 }}>
                  {l.declarationNumber}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace', fontSize: 12 }}>
                  {l.mrn}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                  {new Date(l.declarationDate).toLocaleDateString('mk-MK')}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                  {l.itemCode ?? '—'}
                  {l.itemName ? ` — ${l.itemName}` : ''}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
                  {l.quantity.toFixed(4)}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
                  {l.uoMCode ?? '—'}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
                  {l.dutyAmount.toFixed(2)}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
                  {l.vatAmount.toFixed(2)}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'center' }}>
                  <Checkbox
                    size="small"
                    checked={l.razdolzenaDaNe}
                    onChange={() => toggleLine(l)}
                    disabled={isLocked}
                  />
                </Box>
              </React.Fragment>
            ))}
          </Box>
        )}
      </Paper>
    </Box>
  );
};

export default RazdolzuvanjeView;
