import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Grid,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { toast } from 'react-toastify';
import { logisticsApi } from '../../services/api';

interface LineRow {
  id: string;
  itemId: string;
  description: string;
  quantity: number;
  batchNumber?: string | null;
  mrn?: string | null;
  notes?: string | null;
}

interface DeliveryNoteDetailDto {
  id: string;
  number: string;
  documentType: number;
  documentTypeName: string;
  status: number;
  statusName: string;
  relatedDocumentId: string;
  dispatchDate: string;
  fromLocationId: string;
  toPartnerId?: string | null;
  toLocationId?: string | null;
  driverName?: string | null;
  vehicleRegistration?: string | null;
  remarks?: string | null;
  confirmedAt?: string | null;
  cancelReason?: string | null;
  lines: LineRow[];
}

const STATUS_COLOR: Record<number, 'default' | 'info' | 'success' | 'error'> = {
  1: 'default',
  2: 'info',
  3: 'success',
  4: 'error',
};
const STATUS_LABEL_KEY: Record<number, string> = {
  1: 'deliveryNotes.statuses.draft',
  2: 'deliveryNotes.statuses.sent',
  3: 'deliveryNotes.statuses.confirmed',
  4: 'deliveryNotes.statuses.cancelled',
};
const TYPE_LABEL_KEY: Record<number, string> = {
  1: 'deliveryNotes.types.producerDispatch',
  2: 'deliveryNotes.types.producerReturn',
  3: 'deliveryNotes.types.customerShipment',
};

const DeliveryNoteDetail: React.FC = () => {
  const { id = '' } = useParams<{ id: string }>();
  const { t } = useTranslation();
  const navigate = useNavigate();

  const [dn, setDn] = useState<DeliveryNoteDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const [driverName, setDriverName] = useState('');
  const [vehicle, setVehicle] = useState('');
  const [remarks, setRemarks] = useState('');

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const resp = await logisticsApi.getDeliveryNote(id);
      const data = resp.data?.data as DeliveryNoteDetailDto | undefined;
      if (!data) {
        setError(t('deliveryNotes.detail.notFound') as string);
        return;
      }
      setDn(data);
      setDriverName(data.driverName ?? '');
      setVehicle(data.vehicleRegistration ?? '');
      setRemarks(data.remarks ?? '');
    } catch (err: any) {
      setError(err?.response?.data?.errorMessage ?? err?.message ?? 'Load failed');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  const isDraft = dn?.status === 1;

  const handleSave = async () => {
    if (!dn || !isDraft) return;
    setSubmitting(true);
    try {
      const resp = await logisticsApi.updateDeliveryNote(dn.id, {
        driverName: driverName || null,
        vehicleRegistration: vehicle || null,
        remarks: remarks || null,
      });
      if (!resp.data?.isSuccess) {
        toast.error(resp.data?.errorMessage ?? (t('deliveryNotes.detail.updateFailed') as string));
        return;
      }
      toast.success(t('deliveryNotes.detail.updated') as string);
      await load();
    } catch (err: any) {
      toast.error(err?.response?.data?.errorMessage ?? (t('deliveryNotes.detail.updateFailed') as string));
    } finally {
      setSubmitting(false);
    }
  };

  const handleConfirm = async () => {
    if (!dn || !isDraft) return;
    setSubmitting(true);
    try {
      const resp = await logisticsApi.confirmDeliveryNote(dn.id);
      if (!resp.data?.isSuccess) {
        toast.error(resp.data?.errorMessage ?? (t('deliveryNotes.detail.confirmFailed') as string));
        return;
      }
      toast.success(t('deliveryNotes.detail.confirmed') as string);
      await load();
    } catch (err: any) {
      toast.error(err?.response?.data?.errorMessage ?? (t('deliveryNotes.detail.confirmFailed') as string));
    } finally {
      setSubmitting(false);
    }
  };

  const handleCancel = async () => {
    if (!dn || !isDraft) return;
    const reason = window.prompt(t('deliveryNotes.detail.cancelReasonPrompt') as string) ?? '';
    setSubmitting(true);
    try {
      const resp = await logisticsApi.cancelDeliveryNote(dn.id, reason || null);
      if (!resp.data?.isSuccess) {
        toast.error(resp.data?.errorMessage ?? (t('deliveryNotes.detail.cancelFailed') as string));
        return;
      }
      toast.success(t('deliveryNotes.detail.cancelled') as string);
      await load();
    } catch (err: any) {
      toast.error(err?.response?.data?.errorMessage ?? (t('deliveryNotes.detail.cancelFailed') as string));
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return <Box p={3}><CircularProgress /></Box>;
  }

  if (error || !dn) {
    return (
      <Box p={3}>
        <Alert severity="error" sx={{ mb: 2 }}>{error ?? t('deliveryNotes.detail.notFound')}</Alert>
        <Button onClick={() => navigate('/warehouse/delivery-notes')}>
          {t('deliveryNotes.detail.backToList')}
        </Button>
      </Box>
    );
  }

  return (
    <Box p={3}>
      <Paper sx={{ p: 3, mb: 3 }}>
        <Stack direction="row" alignItems="center" justifyContent="space-between" mb={2}>
          <Stack direction="row" alignItems="center" spacing={2}>
            <Typography variant="h5" sx={{ fontFamily: 'monospace' }}>{dn.number}</Typography>
            <Chip label={t(STATUS_LABEL_KEY[dn.status] ?? 'deliveryNotes.statuses.draft')} color={STATUS_COLOR[dn.status]} />
            <Typography variant="caption" color="text.secondary">
              {t(TYPE_LABEL_KEY[dn.documentType] ?? 'deliveryNotes.types.producerDispatch')}
            </Typography>
          </Stack>
          <Stack direction="row" spacing={1}>
            <Button onClick={() => navigate('/warehouse/delivery-notes')} size="small">
              {t('deliveryNotes.detail.backToList')}
            </Button>
            <Button
              variant="outlined"
              size="small"
              component="a"
              href={`/api/Logistics/delivery-notes/${dn.id}/pdf`}
              target="_blank"
              rel="noopener noreferrer"
            >
              {t('deliveryNotes.detail.printCoverSheet')}
            </Button>
            <Button
              variant="contained"
              color="primary"
              size="small"
              disabled={!isDraft || submitting}
              onClick={handleConfirm}
            >
              {t('deliveryNotes.detail.confirm')}
            </Button>
            <Button
              variant="outlined"
              color="error"
              size="small"
              disabled={!isDraft || submitting}
              onClick={handleCancel}
            >
              {t('deliveryNotes.detail.cancel')}
            </Button>
          </Stack>
        </Stack>

        <Grid container spacing={2}>
          <Grid item xs={12} sm={6}>
            <TextField
              fullWidth size="small"
              label={t('deliveryNotes.detail.driver')}
              value={driverName}
              onChange={(e) => setDriverName(e.target.value)}
              disabled={!isDraft}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <TextField
              fullWidth size="small"
              label={t('deliveryNotes.detail.vehicle')}
              value={vehicle}
              onChange={(e) => setVehicle(e.target.value)}
              disabled={!isDraft}
            />
          </Grid>
          <Grid item xs={12}>
            <TextField
              fullWidth size="small"
              multiline rows={2}
              label={t('deliveryNotes.detail.remarks')}
              value={remarks}
              onChange={(e) => setRemarks(e.target.value)}
              disabled={!isDraft}
            />
          </Grid>
          {isDraft && (
            <Grid item xs={12}>
              <Button variant="contained" onClick={handleSave} disabled={submitting} size="small">
                {t('deliveryNotes.detail.save')}
              </Button>
            </Grid>
          )}
        </Grid>

        {dn.cancelReason && (
          <Alert severity="warning" sx={{ mt: 2 }}>
            {t('deliveryNotes.detail.cancelReason')}: {dn.cancelReason}
          </Alert>
        )}
      </Paper>

      <Paper sx={{ p: 0, overflow: 'hidden' }}>
        <Box sx={{ p: 2 }}>
          <Typography variant="overline" color="text.secondary">{t('deliveryNotes.detail.lines')}</Typography>
        </Box>
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: '0.4fr 2fr 1fr 1.2fr 0.8fr',
            fontSize: 13,
          }}
        >
          {[
            '#',
            t('deliveryNotes.cols.description'),
            t('deliveryNotes.cols.batch'),
            t('deliveryNotes.cols.mrn'),
            t('deliveryNotes.cols.quantity'),
          ].map((h, i) => (
            <Box
              key={i}
              sx={{
                fontWeight: 600,
                p: 1,
                borderTop: 1,
                borderBottom: 1,
                borderColor: 'divider',
                bgcolor: 'background.default',
                textAlign: i === 4 ? 'right' : 'left',
              }}
            >
              {h}
            </Box>
          ))}
          {dn.lines.map((l, idx) => (
            <React.Fragment key={l.id}>
              <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>{idx + 1}</Box>
              <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>{l.description}</Box>
              <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace' }}>
                {l.batchNumber ?? '—'}
              </Box>
              <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace', fontSize: 11 }}>
                {l.mrn ?? '—'}
              </Box>
              <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
                {l.quantity.toFixed(4)}
              </Box>
            </React.Fragment>
          ))}
        </Box>
      </Paper>
    </Box>
  );
};

export default DeliveryNoteDetail;
