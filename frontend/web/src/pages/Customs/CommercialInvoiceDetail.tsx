import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Chip,
  CircularProgress,
  Grid,
  IconButton,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/DeleteOutline';
import { toast } from 'react-toastify';
import { commercialInvoicesApi, masterDataApi } from '../../services/api';

interface LineRow {
  id?: string;
  lineNumber?: number;
  itemId: string;
  itemCode?: string | null;
  itemName?: string | null;
  description: string;
  quantity: number;
  uoMId: string;
  uoMCode?: string | null;
  unitPrice: number;
  lineTotal?: number;
  countryOfOrigin?: string | null;
  tariffCodeId?: string | null;
  notes?: string | null;
}

interface CommercialInvoiceDetailDto {
  id: string;
  number: string;
  clientOrderId?: string | null;
  clientOrderNumber?: string | null;
  shipmentId?: string | null;
  shipmentNumber?: string | null;
  customsDeclarationId?: string | null;
  customsDeclarationNumber?: string | null;
  consigneePartnerId: string;
  consigneeName?: string | null;
  consigneeCode?: string | null;
  consignorPartnerId: string;
  consignorName?: string | null;
  consignorCode?: string | null;
  invoiceDate: string;
  currency: string;
  subtotal: number;
  taxAmount?: number | null;
  totalAmount: number;
  countryOfDestination?: string | null;
  incoterms: string;
  paymentTerms?: string | null;
  status: number;
  statusName: string;
  issuedAt?: string | null;
  issuedBy?: string | null;
  cancelledAt?: string | null;
  cancellationReason?: string | null;
  notes?: string | null;
  lines: LineRow[];
}

interface PartnerOption {
  id: string;
  code: string;
  name: string;
  partnerType?: number;
}

const STATUS_COLOR: Record<number, 'default' | 'success' | 'error'> = {
  1: 'default',
  2: 'success',
  3: 'error',
};
const STATUS_LABEL_KEY: Record<number, string> = {
  1: 'commercialInvoices.statuses.draft',
  2: 'commercialInvoices.statuses.issued',
  3: 'commercialInvoices.statuses.cancelled',
};

const INCOTERMS = ['FOB', 'EXW', 'CIF', 'CFR', 'DAP', 'DAT', 'DDP', 'FCA', 'CPT', 'CIP'];

const CommercialInvoiceDetail: React.FC = () => {
  const { id = '' } = useParams<{ id: string }>();
  const { t } = useTranslation();
  const navigate = useNavigate();

  const [ci, setCi] = useState<CommercialInvoiceDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const [partners, setPartners] = useState<PartnerOption[]>([]);
  const [invoiceDate, setInvoiceDate] = useState('');
  const [consignor, setConsignor] = useState<PartnerOption | null>(null);
  const [consignee, setConsignee] = useState<PartnerOption | null>(null);
  const [incoterms, setIncoterms] = useState('FOB');
  const [currency, setCurrency] = useState('EUR');
  const [destination, setDestination] = useState('');
  const [paymentTerms, setPaymentTerms] = useState('');
  const [taxAmount, setTaxAmount] = useState<number | ''>('');
  const [notes, setNotes] = useState('');
  const [lines, setLines] = useState<LineRow[]>([]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const [ciResp, partnersResp] = await Promise.all([
        commercialInvoicesApi.getById(id),
        masterDataApi.getPartners(),
      ]);
      const data = ciResp.data?.data as CommercialInvoiceDetailDto | undefined;
      if (!data) {
        setError(t('commercialInvoices.detail.notFound') as string);
        return;
      }
      setCi(data);
      setInvoiceDate(data.invoiceDate.slice(0, 10));
      setIncoterms(data.incoterms || 'FOB');
      setCurrency(data.currency || 'EUR');
      setDestination(data.countryOfDestination ?? '');
      setPaymentTerms(data.paymentTerms ?? '');
      setTaxAmount(data.taxAmount ?? '');
      setNotes(data.notes ?? '');
      setLines(data.lines.map((l) => ({ ...l })));
      const partnerList = (partnersResp.data ?? []) as PartnerOption[];
      setPartners(partnerList);
      setConsignor(partnerList.find((p) => p.id === data.consignorPartnerId) ?? null);
      setConsignee(partnerList.find((p) => p.id === data.consigneePartnerId) ?? null);
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

  const isDraft = ci?.status === 1;

  const subtotal = lines.reduce((s, l) => s + (l.quantity ?? 0) * (l.unitPrice ?? 0), 0);
  const total = subtotal + (typeof taxAmount === 'number' ? taxAmount : 0);

  const handleSave = async () => {
    if (!ci || !isDraft) return;
    if (!consignor || !consignee) {
      toast.error(t('commercialInvoices.detail.missingParties') as string);
      return;
    }
    if (lines.length === 0) {
      toast.error(t('commercialInvoices.detail.missingLines') as string);
      return;
    }
    setSubmitting(true);
    try {
      const resp = await commercialInvoicesApi.update(ci.id, {
        consigneePartnerId: consignee.id,
        consignorPartnerId: consignor.id,
        invoiceDate,
        currency,
        countryOfDestination: destination || null,
        incoterms,
        paymentTerms: paymentTerms || null,
        taxAmount: typeof taxAmount === 'number' ? taxAmount : null,
        notes: notes || null,
        lines: lines.map((l) => ({
          itemId: l.itemId,
          description: l.description,
          quantity: l.quantity,
          uoMId: l.uoMId,
          unitPrice: l.unitPrice,
          countryOfOrigin: l.countryOfOrigin ?? null,
          tariffCodeId: l.tariffCodeId ?? null,
          notes: l.notes ?? null,
        })),
      });
      if (!resp.data?.isSuccess) {
        toast.error(resp.data?.errorMessage ?? (t('commercialInvoices.detail.updateFailed') as string));
        return;
      }
      toast.success(t('commercialInvoices.detail.updated') as string);
      await load();
    } catch (err: any) {
      toast.error(err?.response?.data?.errorMessage ?? (t('commercialInvoices.detail.updateFailed') as string));
    } finally {
      setSubmitting(false);
    }
  };

  const handleIssue = async () => {
    if (!ci || !isDraft) return;
    setSubmitting(true);
    try {
      const resp = await commercialInvoicesApi.issue(ci.id);
      if (!resp.data?.isSuccess) {
        toast.error(resp.data?.errorMessage ?? (t('commercialInvoices.detail.issueFailed') as string));
        return;
      }
      toast.success(t('commercialInvoices.detail.issued') as string);
      await load();
    } catch (err: any) {
      toast.error(err?.response?.data?.errorMessage ?? (t('commercialInvoices.detail.issueFailed') as string));
    } finally {
      setSubmitting(false);
    }
  };

  const handleCancel = async () => {
    if (!ci) return;
    const reason = window.prompt(t('commercialInvoices.detail.cancelReasonPrompt') as string) ?? '';
    setSubmitting(true);
    try {
      const resp = await commercialInvoicesApi.cancel(ci.id, reason || undefined);
      if (!resp.data?.isSuccess) {
        toast.error(resp.data?.errorMessage ?? (t('commercialInvoices.detail.cancelFailed') as string));
        return;
      }
      toast.success(t('commercialInvoices.detail.cancelled') as string);
      await load();
    } catch (err: any) {
      toast.error(err?.response?.data?.errorMessage ?? (t('commercialInvoices.detail.cancelFailed') as string));
    } finally {
      setSubmitting(false);
    }
  };

  const updateLine = (idx: number, patch: Partial<LineRow>) => {
    setLines((prev) => prev.map((l, i) => (i === idx ? { ...l, ...patch } : l)));
  };

  const removeLine = (idx: number) => {
    setLines((prev) => prev.filter((_, i) => i !== idx));
  };

  if (loading) {
    return <Box p={3}><CircularProgress /></Box>;
  }
  if (error || !ci) {
    return (
      <Box p={3}>
        <Alert severity="error" sx={{ mb: 2 }}>{error ?? t('commercialInvoices.detail.notFound')}</Alert>
        <Button onClick={() => navigate('/customs/commercial-invoices')}>
          {t('commercialInvoices.detail.backToList')}
        </Button>
      </Box>
    );
  }

  return (
    <Box p={3}>
      <Paper sx={{ p: 3, mb: 3 }}>
        <Stack direction="row" alignItems="center" justifyContent="space-between" mb={2}>
          <Stack direction="row" alignItems="center" spacing={2}>
            <Typography variant="h5" sx={{ fontFamily: 'monospace' }}>{ci.number}</Typography>
            <Chip
              label={t(STATUS_LABEL_KEY[ci.status] ?? 'commercialInvoices.statuses.draft')}
              color={STATUS_COLOR[ci.status] ?? 'default'}
            />
            {ci.clientOrderNumber && (
              <Typography variant="caption" color="text.secondary">
                {t('commercialInvoices.detail.clientOrder')}: <b>{ci.clientOrderNumber}</b>
              </Typography>
            )}
            {ci.shipmentNumber && (
              <Typography variant="caption" color="text.secondary">
                {t('commercialInvoices.detail.shipment')}: <b>{ci.shipmentNumber}</b>
              </Typography>
            )}
          </Stack>
          <Stack direction="row" spacing={1}>
            <Button onClick={() => navigate('/customs/commercial-invoices')} size="small">
              {t('commercialInvoices.detail.backToList')}
            </Button>
            <Button
              variant="outlined"
              size="small"
              component="a"
              href={commercialInvoicesApi.pdfUrl(ci.id)}
              target="_blank"
              rel="noopener noreferrer"
            >
              {t('commercialInvoices.detail.print')}
            </Button>
            <Button
              variant="outlined"
              color="primary"
              size="small"
              disabled={!isDraft || submitting}
              onClick={handleSave}
            >
              {t('commercialInvoices.detail.save')}
            </Button>
            <Tooltip
              title={!isDraft ? (t('commercialInvoices.detail.onlyDraft') as string) : ''}
              disableHoverListener={isDraft}
            >
              <span>
                <Button
                  variant="contained"
                  color="primary"
                  size="small"
                  disabled={!isDraft || submitting}
                  onClick={handleIssue}
                >
                  {t('commercialInvoices.detail.issue')}
                </Button>
              </span>
            </Tooltip>
            <Button
              variant="outlined"
              color="error"
              size="small"
              disabled={ci.status === 3 || submitting}
              onClick={handleCancel}
            >
              {t('commercialInvoices.detail.cancel')}
            </Button>
          </Stack>
        </Stack>

        <Grid container spacing={2}>
          <Grid item xs={12} sm={6}>
            <Autocomplete
              size="small"
              options={partners}
              value={consignor}
              onChange={(_, v) => setConsignor(v)}
              getOptionLabel={(p) => `${p.code} — ${p.name}`}
              renderInput={(params) => (
                <TextField {...params} label={t('commercialInvoices.detail.consignor')} />
              )}
              disabled={!isDraft}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <Autocomplete
              size="small"
              options={partners}
              value={consignee}
              onChange={(_, v) => setConsignee(v)}
              getOptionLabel={(p) => `${p.code} — ${p.name}`}
              renderInput={(params) => (
                <TextField {...params} label={t('commercialInvoices.detail.consignee')} />
              )}
              disabled={!isDraft}
            />
          </Grid>
          <Grid item xs={6} sm={3}>
            <TextField
              fullWidth size="small" type="date"
              label={t('commercialInvoices.detail.invoiceDate')}
              value={invoiceDate}
              onChange={(e) => setInvoiceDate(e.target.value)}
              InputLabelProps={{ shrink: true }}
              disabled={!isDraft}
            />
          </Grid>
          <Grid item xs={6} sm={2}>
            <TextField
              fullWidth size="small"
              label={t('commercialInvoices.detail.currency')}
              value={currency}
              onChange={(e) => setCurrency(e.target.value.toUpperCase())}
              inputProps={{ maxLength: 3 }}
              disabled={!isDraft}
            />
          </Grid>
          <Grid item xs={6} sm={2}>
            <TextField
              select fullWidth size="small"
              label={t('commercialInvoices.detail.incoterms')}
              value={incoterms}
              onChange={(e) => setIncoterms(e.target.value)}
              disabled={!isDraft}
            >
              {INCOTERMS.map((i) => (
                <MenuItem key={i} value={i}>{i}</MenuItem>
              ))}
            </TextField>
          </Grid>
          <Grid item xs={6} sm={2}>
            <TextField
              fullWidth size="small"
              label={t('commercialInvoices.detail.destinationCountry')}
              value={destination}
              onChange={(e) => setDestination(e.target.value.toUpperCase())}
              inputProps={{ maxLength: 2 }}
              disabled={!isDraft}
            />
          </Grid>
          <Grid item xs={12} sm={3}>
            <TextField
              fullWidth size="small"
              label={t('commercialInvoices.detail.paymentTerms')}
              value={paymentTerms}
              onChange={(e) => setPaymentTerms(e.target.value)}
              disabled={!isDraft}
            />
          </Grid>
          <Grid item xs={12}>
            <TextField
              fullWidth size="small" multiline rows={2}
              label={t('commercialInvoices.detail.notes')}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              disabled={!isDraft}
            />
          </Grid>
        </Grid>
      </Paper>

      <Paper sx={{ p: 2 }}>
        <Stack direction="row" alignItems="center" justifyContent="space-between" mb={1}>
          <Typography variant="h6">{t('commercialInvoices.detail.linesTitle')}</Typography>
          <Stack direction="row" spacing={2} alignItems="center">
            <Typography variant="caption" color="text.secondary">
              {t('commercialInvoices.detail.subtotal')}: <b>{subtotal.toFixed(2)} {currency}</b>
            </Typography>
            <TextField
              size="small"
              type="number"
              label={t('commercialInvoices.detail.tax')}
              value={taxAmount}
              onChange={(e) => setTaxAmount(e.target.value === '' ? '' : Number(e.target.value))}
              disabled={!isDraft}
              sx={{ width: 120 }}
            />
            <Typography variant="caption" color="text.secondary">
              {t('commercialInvoices.detail.total')}: <b>{total.toFixed(2)} {currency}</b>
            </Typography>
          </Stack>
        </Stack>
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: '0.5fr 1.4fr 2fr 0.8fr 0.6fr 0.9fr 0.9fr 0.6fr 0.4fr',
            fontSize: 13,
          }}
        >
          {[
            '#',
            t('commercialInvoices.detail.lines.item'),
            t('commercialInvoices.detail.lines.description'),
            t('commercialInvoices.detail.lines.qty'),
            t('commercialInvoices.detail.lines.uom'),
            t('commercialInvoices.detail.lines.unitPrice'),
            t('commercialInvoices.detail.lines.lineTotal'),
            t('commercialInvoices.detail.lines.origin'),
            '',
          ].map((h, i) => (
            <Box
              key={i}
              sx={{
                fontWeight: 600, p: 1,
                borderBottom: 1, borderColor: 'divider',
                bgcolor: 'background.default',
                textAlign: i >= 3 && i <= 6 ? 'right' : 'left',
              }}
            >
              {h}
            </Box>
          ))}
          {lines.map((l, idx) => {
            const lineTotal = (l.quantity ?? 0) * (l.unitPrice ?? 0);
            return (
              <React.Fragment key={l.id ?? idx}>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>{idx + 1}</Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace', fontSize: 12 }}>
                  {l.itemCode ?? l.itemId.slice(0, 8)}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                  <TextField
                    fullWidth size="small" variant="standard"
                    value={l.description}
                    onChange={(e) => updateLine(idx, { description: e.target.value })}
                    disabled={!isDraft}
                  />
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
                  <TextField
                    type="number" size="small" variant="standard"
                    inputProps={{ style: { textAlign: 'right' } }}
                    value={l.quantity}
                    onChange={(e) => updateLine(idx, { quantity: Number(e.target.value) })}
                    disabled={!isDraft}
                  />
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>{l.uoMCode ?? '—'}</Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
                  <TextField
                    type="number" size="small" variant="standard"
                    inputProps={{ style: { textAlign: 'right' }, step: 0.0001 }}
                    value={l.unitPrice}
                    onChange={(e) => updateLine(idx, { unitPrice: Number(e.target.value) })}
                    disabled={!isDraft}
                  />
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
                  {lineTotal.toFixed(4)}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                  <TextField
                    size="small" variant="standard"
                    inputProps={{ maxLength: 2 }}
                    value={l.countryOfOrigin ?? ''}
                    onChange={(e) =>
                      updateLine(idx, { countryOfOrigin: e.target.value.toUpperCase() || null })
                    }
                    disabled={!isDraft}
                  />
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'center' }}>
                  {isDraft && (
                    <IconButton size="small" onClick={() => removeLine(idx)} color="error">
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  )}
                </Box>
              </React.Fragment>
            );
          })}
        </Box>

        {ci.cancellationReason && (
          <Alert severity="error" sx={{ mt: 2 }}>
            {t('commercialInvoices.detail.cancelReason')}: {ci.cancellationReason}
          </Alert>
        )}
      </Paper>
    </Box>
  );
};

export default CommercialInvoiceDetail;
