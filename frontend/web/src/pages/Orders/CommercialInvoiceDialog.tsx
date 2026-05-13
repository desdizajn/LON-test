import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Autocomplete,
  Box,
  Grid,
  LinearProgress,
  MenuItem,
  TextField,
  Typography,
} from '@mui/material';
import { toast } from 'react-toastify';
import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import FormDialog from '../../components/common/FormDialog';
import { commercialInvoicesApi, masterDataApi } from '../../services/api';
import { ClientOrderDto, clientOrderKeys } from '../../hooks/queries/useClientOrders';

interface Props {
  open: boolean;
  order: ClientOrderDto;
  /** Shipment to draft this CI from. Required — the dialog suggests lines from it. */
  shipmentId: string | null;
  onClose: () => void;
  onCreated: () => void;
}

interface PartnerOption { id: string; code: string; name: string; partnerType?: number }

interface DraftLine {
  itemId: string;
  itemCode?: string | null;
  itemName?: string | null;
  description: string;
  quantity: number;
  uoMId: string;
  uoMCode?: string | null;
  unitPrice: number;
  countryOfOrigin?: string | null;
}

const INCOTERMS = ['FOB', 'EXW', 'CIF', 'CFR', 'DAP', 'DAT', 'DDP', 'FCA', 'CPT', 'CIP'];

/**
 * Phase 17 §E8.5 — Hub chain action after EX submit. Takes the just-created
 * Shipment, calls `/suggest-from-shipment` for line drafts, lets the user
 * fill consignee/consignor/incoterms/prices and POSTs to `create`.
 */
const CommercialInvoiceDialog: React.FC<Props> = ({ open, order, shipmentId, onClose, onCreated }) => {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const navigate = useNavigate();

  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [partners, setPartners] = useState<PartnerOption[]>([]);
  const [consignor, setConsignor] = useState<PartnerOption | null>(null);
  const [consignee, setConsignee] = useState<PartnerOption | null>(null);
  const [invoiceDate, setInvoiceDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [currency, setCurrency] = useState('EUR');
  const [incoterms, setIncoterms] = useState('FOB');
  const [destination, setDestination] = useState('');
  const [paymentTerms, setPaymentTerms] = useState('');
  const [taxAmount, setTaxAmount] = useState<number | ''>('');
  const [lines, setLines] = useState<DraftLine[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open || !shipmentId) return;
    let cancelled = false;
    (async () => {
      setLoading(true);
      setError(null);
      try {
        const [suggestResp, partnersResp] = await Promise.all([
          commercialInvoicesApi.suggestFromShipment(shipmentId),
          masterDataApi.getPartners(),
        ]);
        if (cancelled) return;
        const draft = suggestResp.data?.data;
        if (!draft) {
          setError(t('orders.ciDialog.errors.suggestFailed') as string);
          return;
        }
        const partnerList = (partnersResp.data ?? []) as PartnerOption[];
        setPartners(partnerList);
        setCurrency(draft.currency ?? 'EUR');
        setIncoterms(draft.incoterms ?? 'FOB');
        setDestination(draft.countryOfDestination ?? '');
        if (draft.consigneePartnerId) {
          setConsignee(partnerList.find((p) => p.id === draft.consigneePartnerId) ?? null);
        }
        setLines(
          (draft.lines ?? []).map((l: any) => ({
            itemId: l.itemId,
            itemCode: l.itemCode,
            itemName: l.itemName,
            description: l.description ?? '',
            quantity: l.quantity ?? 0,
            uoMId: l.uoMId,
            uoMCode: l.uoMCode,
            unitPrice: l.unitPrice ?? 0,
            countryOfOrigin: l.countryOfOrigin ?? 'MK',
          })),
        );
      } catch (err: any) {
        if (!cancelled) {
          setError(
            err?.response?.data?.errorMessage ??
              err?.message ??
              (t('orders.ciDialog.errors.suggestFailed') as string),
          );
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [open, shipmentId, t]);

  const subtotal = useMemo(
    () => lines.reduce((s, l) => s + (l.quantity ?? 0) * (l.unitPrice ?? 0), 0),
    [lines],
  );
  const total = subtotal + (typeof taxAmount === 'number' ? taxAmount : 0);

  const onSubmit = async () => {
    if (!shipmentId) return;
    if (!consignor || !consignee) {
      toast.error(t('orders.ciDialog.errors.missingParties') as string);
      return;
    }
    if (lines.length === 0) {
      toast.error(t('orders.ciDialog.errors.missingLines') as string);
      return;
    }
    setSubmitting(true);
    try {
      const resp = await commercialInvoicesApi.create({
        clientOrderId: order.id,
        shipmentId,
        consigneePartnerId: consignee.id,
        consignorPartnerId: consignor.id,
        invoiceDate,
        currency,
        incoterms,
        countryOfDestination: destination || null,
        paymentTerms: paymentTerms || null,
        taxAmount: typeof taxAmount === 'number' ? taxAmount : null,
        lines: lines.map((l) => ({
          itemId: l.itemId,
          description: l.description,
          quantity: l.quantity,
          uoMId: l.uoMId,
          unitPrice: l.unitPrice,
          countryOfOrigin: l.countryOfOrigin ?? null,
        })),
      });
      const env = resp.data;
      if (!env?.isSuccess) {
        toast.error(env?.errorMessage ?? (t('orders.ciDialog.errors.createFailed') as string));
        return;
      }
      toast.success(t('orders.ciDialog.created') as string);
      qc.invalidateQueries({ queryKey: clientOrderKeys.all });
      qc.invalidateQueries({ queryKey: ['clientOrders', 'commercialInvoices', order.id] });
      onCreated();
      navigate(`/customs/commercial-invoices/${env.data}`);
    } catch (err: any) {
      toast.error(
        err?.response?.data?.errorMessage ??
          err?.message ??
          (t('orders.ciDialog.errors.createFailed') as string),
      );
    } finally {
      setSubmitting(false);
    }
  };

  const updateLine = (idx: number, patch: Partial<DraftLine>) => {
    setLines((prev) => prev.map((l, i) => (i === idx ? { ...l, ...patch } : l)));
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={t('orders.ciDialog.title') as string}
      submitText={t('orders.ciDialog.submit') as string}
      cancelText={t('common.cancel') as string}
      onSubmit={onSubmit}
      isSubmitting={submitting}
      disableSubmit={loading || !consignor || !consignee || lines.length === 0}
      maxWidth="lg"
    >
      <Box>
        <Box sx={{ p: 1.5, mb: 2, bgcolor: 'background.default', borderRadius: 1 }}>
          <Typography variant="caption" color="text.secondary">
            {t('orders.ciDialog.hint')}: <strong>{order.orderNumber}</strong>
          </Typography>
        </Box>

        {loading && <LinearProgress sx={{ mb: 2 }} />}
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

        <Grid container spacing={2}>
          <Grid item xs={12} sm={6}>
            <Autocomplete
              size="small"
              options={partners}
              value={consignor}
              onChange={(_, v) => setConsignor(v)}
              getOptionLabel={(p) => `${p.code} — ${p.name}`}
              renderInput={(params) => (
                <TextField {...params} label={t('orders.ciDialog.consignor')} required />
              )}
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
                <TextField {...params} label={t('orders.ciDialog.consignee')} required />
              )}
            />
          </Grid>
          <Grid item xs={6} sm={3}>
            <TextField
              fullWidth size="small" type="date"
              label={t('orders.ciDialog.invoiceDate')}
              value={invoiceDate}
              onChange={(e) => setInvoiceDate(e.target.value)}
              InputLabelProps={{ shrink: true }}
            />
          </Grid>
          <Grid item xs={6} sm={2}>
            <TextField
              fullWidth size="small"
              label={t('orders.ciDialog.currency')}
              value={currency}
              onChange={(e) => setCurrency(e.target.value.toUpperCase())}
              inputProps={{ maxLength: 3 }}
            />
          </Grid>
          <Grid item xs={6} sm={2}>
            <TextField
              select fullWidth size="small"
              label={t('orders.ciDialog.incoterms')}
              value={incoterms}
              onChange={(e) => setIncoterms(e.target.value)}
            >
              {INCOTERMS.map((i) => (
                <MenuItem key={i} value={i}>{i}</MenuItem>
              ))}
            </TextField>
          </Grid>
          <Grid item xs={6} sm={2}>
            <TextField
              fullWidth size="small"
              label={t('orders.ciDialog.destinationCountry')}
              value={destination}
              onChange={(e) => setDestination(e.target.value.toUpperCase())}
              inputProps={{ maxLength: 2 }}
            />
          </Grid>
          <Grid item xs={12} sm={3}>
            <TextField
              fullWidth size="small"
              label={t('orders.ciDialog.paymentTerms')}
              value={paymentTerms}
              onChange={(e) => setPaymentTerms(e.target.value)}
            />
          </Grid>
        </Grid>

        <Box sx={{ mt: 3 }}>
          <Typography variant="overline" color="text.secondary">
            {t('orders.ciDialog.linesTitle')}
          </Typography>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: '0.5fr 1.4fr 2fr 0.8fr 0.5fr 0.9fr 0.9fr 0.6fr',
              fontSize: 13,
              mt: 1,
            }}
          >
            {[
              '#',
              t('orders.ciDialog.lineCols.item'),
              t('orders.ciDialog.lineCols.description'),
              t('orders.ciDialog.lineCols.qty'),
              t('orders.ciDialog.lineCols.uom'),
              t('orders.ciDialog.lineCols.unitPrice'),
              t('orders.ciDialog.lineCols.lineTotal'),
              t('orders.ciDialog.lineCols.origin'),
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
                <React.Fragment key={idx}>
                  <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>{idx + 1}</Box>
                  <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace', fontSize: 12 }}>
                    {l.itemCode ?? l.itemId.slice(0, 8)}
                  </Box>
                  <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                    <TextField
                      fullWidth size="small" variant="standard"
                      value={l.description}
                      onChange={(e) => updateLine(idx, { description: e.target.value })}
                    />
                  </Box>
                  <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
                    <TextField
                      type="number" size="small" variant="standard"
                      inputProps={{ style: { textAlign: 'right' } }}
                      value={l.quantity}
                      onChange={(e) => updateLine(idx, { quantity: Number(e.target.value) })}
                    />
                  </Box>
                  <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>{l.uoMCode ?? '—'}</Box>
                  <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
                    <TextField
                      type="number" size="small" variant="standard"
                      inputProps={{ style: { textAlign: 'right' }, step: 0.0001 }}
                      value={l.unitPrice}
                      onChange={(e) => updateLine(idx, { unitPrice: Number(e.target.value) })}
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
                    />
                  </Box>
                </React.Fragment>
              );
            })}
          </Box>

          <Box sx={{ mt: 2, display: 'flex', justifyContent: 'flex-end', gap: 3, alignItems: 'center' }}>
            <Typography variant="body2">
              {t('orders.ciDialog.subtotal')}: <b>{subtotal.toFixed(2)} {currency}</b>
            </Typography>
            <TextField
              size="small"
              type="number"
              label={t('orders.ciDialog.tax')}
              value={taxAmount}
              onChange={(e) => setTaxAmount(e.target.value === '' ? '' : Number(e.target.value))}
              sx={{ width: 120 }}
            />
            <Typography variant="body2">
              {t('orders.ciDialog.total')}: <b>{total.toFixed(2)} {currency}</b>
            </Typography>
          </Box>
        </Box>
      </Box>
    </FormDialog>
  );
};

export default CommercialInvoiceDialog;
