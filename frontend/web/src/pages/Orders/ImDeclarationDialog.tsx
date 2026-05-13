import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import {
  Box,
  Button,
  Divider,
  Grid,
  IconButton,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import { toast } from 'react-toastify';
import { useQueryClient } from '@tanstack/react-query';
import FormDialog from '../../components/common/FormDialog';
import FormInput from '../../components/forms/FormInput';
import FormSelect from '../../components/forms/FormSelect';
import { customsApi, masterDataApi } from '../../services/api';
import { ClientOrderDto, clientOrderKeys } from '../../hooks/queries/useClientOrders';

interface ProcedureOption { id: string; code: string; name?: string }
interface ItemOption { id: string; code: string; name?: string }
interface UoMOption { id: string; code: string; name?: string }
interface PartnerOption { id: string; name: string; address?: string | null; country?: string | null; countryCode?: string | null }

interface HeaderForm {
  declarationDate: string;
  customsProcedureId: string;
  partnerId: string;
  senderName: string;
  senderAddress: string;
  senderCountry: string;
  countryOfDispatch: string;
  totalCustomsValue: string;
  currency: string;
  notes: string;
}

interface LineDraft {
  itemId: string;
  tariffCode: string;
  quantity: string;
  uoMId: string;
  customsValue: string;
  countryOfOrigin: string;
  dutyRate: string;
  vatRate: string;
}

const emptyLine: LineDraft = {
  itemId: '',
  tariffCode: '',
  quantity: '',
  uoMId: '',
  customsValue: '',
  countryOfOrigin: '',
  dutyRate: '0',
  vatRate: '18',
};

const todayIso = () => new Date().toISOString().slice(0, 10);

interface Props {
  open: boolean;
  order: ClientOrderDto;
  onClose: () => void;
  onCreated: (declarationId: string) => void;
}

/**
 * Phase 17 §E3 — inline IM-declaration creation from the ClientOrder hub.
 *
 * The hub passes the parent ClientOrder; we pre-fill LONAuthorizationId from
 * it and persist ClientOrderId so the new declaration links back. The backend
 * auto-numbers (IM-{year}-{seq:D6}) when declarationNumber is empty.
 */
const ImDeclarationDialog: React.FC<Props> = ({ open, order, onClose, onCreated }) => {
  const { t } = useTranslation();
  const qc = useQueryClient();

  const [procedures, setProcedures] = useState<ProcedureOption[]>([]);
  const [items, setItems] = useState<ItemOption[]>([]);
  const [uoms, setUoms] = useState<UoMOption[]>([]);
  const [partners, setPartners] = useState<PartnerOption[]>([]);
  const [lines, setLines] = useState<LineDraft[]>([{ ...emptyLine }]);
  const [submitting, setSubmitting] = useState(false);

  const { control, handleSubmit, reset, watch, setValue } = useForm<HeaderForm>({
    defaultValues: {
      declarationDate: todayIso(),
      customsProcedureId: '',
      partnerId: '',
      senderName: '',
      senderAddress: '',
      senderCountry: '',
      countryOfDispatch: '',
      totalCustomsValue: '',
      currency: 'EUR',
      notes: '',
    },
  });

  // Load reference data when the dialog opens.
  useEffect(() => {
    if (!open) return;
    customsApi
      .getProcedures()
      .then((r) => {
        const all = (r.data?.data ?? r.data ?? []) as ProcedureOption[];
        // Hub action is „Креирај увозна декларација (IM)" → prefer IM procedures.
        const im = all.filter((p) => p.code === '4200' || p.code === '5100');
        setProcedures(im.length ? im : all);
      })
      .catch(() => setProcedures([]));
    masterDataApi.getItems().then((r) => setItems((r.data?.data ?? r.data ?? []) as ItemOption[])).catch(() => setItems([]));
    masterDataApi.getUoM().then((r) => setUoms((r.data?.data ?? r.data ?? []) as UoMOption[])).catch(() => setUoms([]));
    masterDataApi.getPartners().then((r) => setPartners((r.data?.data ?? r.data ?? []) as PartnerOption[])).catch(() => setPartners([]));
    reset({
      declarationDate: todayIso(),
      customsProcedureId: '',
      partnerId: '',
      senderName: '',
      senderAddress: '',
      senderCountry: '',
      countryOfDispatch: '',
      totalCustomsValue: '',
      currency: 'EUR',
      notes: '',
    });
    setLines([{ ...emptyLine }]);
  }, [open, reset]);

  // Auto-fill sender fields when partner is chosen.
  const selectedPartnerId = watch('partnerId');
  useEffect(() => {
    if (!selectedPartnerId) return;
    const p = partners.find((x) => x.id === selectedPartnerId);
    if (!p) return;
    setValue('senderName', p.name ?? '');
    if (p.address) setValue('senderAddress', p.address);
    const country = (p.countryCode ?? p.country ?? '').toUpperCase();
    if (country) {
      setValue('senderCountry', country);
      setValue('countryOfDispatch', country);
    }
  }, [selectedPartnerId, partners, setValue]);

  const procedureOptions = useMemo(
    () => procedures.map((p) => ({ value: p.id, label: p.name ? `${p.code} — ${p.name}` : p.code })),
    [procedures],
  );
  const partnerOptions = useMemo(
    () => partners.map((p) => ({ value: p.id, label: p.name })),
    [partners],
  );
  const itemOptions = useMemo(
    () => items.map((i) => ({ value: i.id, label: i.name ? `${i.code} — ${i.name}` : i.code })),
    [items],
  );
  const uomOptions = useMemo(
    () => uoms.map((u) => ({ value: u.id, label: u.code })),
    [uoms],
  );

  const linesTotal = useMemo(
    () =>
      lines.reduce((sum, l) => {
        const v = parseFloat(l.customsValue);
        return Number.isFinite(v) ? sum + v : sum;
      }, 0),
    [lines],
  );

  const updateLine = (idx: number, patch: Partial<LineDraft>) =>
    setLines((prev) => prev.map((row, i) => (i === idx ? { ...row, ...patch } : row)));

  const onSubmit = async (header: HeaderForm) => {
    if (lines.length === 0 || lines.every((l) => !l.itemId)) {
      toast.error(t('orders.imDialog.atLeastOneLine') as string);
      return;
    }
    const cleanedLines = lines
      .filter((l) => l.itemId && l.quantity)
      .map((l) => ({
        itemId: l.itemId,
        tariffCode: l.tariffCode || null,
        quantity: parseFloat(l.quantity) || 0,
        uoMId: l.uoMId,
        customsValue: parseFloat(l.customsValue) || 0,
        countryOfOrigin: l.countryOfOrigin || null,
        dutyRate: parseFloat(l.dutyRate) || 0,
        vatRate: parseFloat(l.vatRate) || 0,
      }));

    try {
      setSubmitting(true);
      const resp = await customsApi.createDeclaration({
        declarationNumber: '',            // empty → SEQUENCE auto-fills IM-{year}-{seq}
        mrn: '',                          // dev placeholder unless customs portal returns one
        declarationDate: header.declarationDate,
        customsProcedureId: header.customsProcedureId,
        partnerId: header.partnerId || null,
        lonAuthorizationId: order.lonAuthorizationId,
        clientOrderId: order.id,          // hub linkage
        totalCustomsValue: parseFloat(header.totalCustomsValue) || linesTotal,
        currency: header.currency || 'EUR',
        senderName: header.senderName || null,
        senderAddress: header.senderAddress || null,
        senderCountry: header.senderCountry || null,
        countryOfDispatch: header.countryOfDispatch || null,
        lines: cleanedLines,
      });

      const env = resp.data;
      if (!env?.isSuccess) {
        toast.error(env?.errorMessage || (t('orders.imDialog.createFailed') as string));
        return;
      }
      toast.success(t('orders.imDialog.created') as string);
      qc.invalidateQueries({ queryKey: clientOrderKeys.detail(order.id) });
      qc.invalidateQueries({ queryKey: clientOrderKeys.all });
      onCreated(env.data as string);
    } catch (err: any) {
      const msg =
        err?.response?.data?.errorMessage ||
        err?.response?.data?.message ||
        err?.message ||
        (t('orders.imDialog.createFailed') as string);
      toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={t('orders.imDialog.title') as string}
      submitText={t('orders.imDialog.submit') as string}
      cancelText={t('common.cancel') as string}
      onSubmit={handleSubmit(onSubmit)}
      isSubmitting={submitting}
      maxWidth="lg"
    >
      <Box>
        {/* ────────── Header ────────── */}
        <Typography variant="overline" color="text.secondary">
          {t('orders.imDialog.section.header')}
        </Typography>
        <Grid container spacing={2}>
          <Grid item xs={12} sm={4}>
            <FormInput
              name="declarationDate"
              control={control}
              label={t('orders.imDialog.fields.declarationDate') as string}
              type="date"
              InputLabelProps={{ shrink: true }}
              rules={{ required: true }}
            />
          </Grid>
          <Grid item xs={12} sm={4}>
            <FormSelect
              name="customsProcedureId"
              control={control}
              label={t('orders.imDialog.fields.procedure') as string}
              options={procedureOptions}
              rules={{ required: t('orders.imDialog.fields.procedureRequired') as string }}
            />
          </Grid>
          <Grid item xs={12} sm={4}>
            <FormSelect
              name="partnerId"
              control={control}
              label={t('orders.imDialog.fields.partner') as string}
              options={partnerOptions}
            />
          </Grid>
          <Grid item xs={12}>
            <Box sx={{ p: 1.5, bgcolor: 'background.default', borderRadius: 1 }}>
              <Typography variant="caption" color="text.secondary">
                {t('orders.imDialog.lonAuthHint')}:{' '}
                <strong>{order.lonAuthorizationNumber ?? '—'}</strong>
                {' · '}
                {t('orders.imDialog.clientOrderHint')}:{' '}
                <strong>{order.orderNumber}</strong>
              </Typography>
            </Box>
          </Grid>
          <Grid item xs={12} sm={6}>
            <FormInput
              name="senderName"
              control={control}
              label={t('orders.imDialog.fields.senderName') as string}
            />
          </Grid>
          <Grid item xs={12} sm={3}>
            <FormInput
              name="senderCountry"
              control={control}
              label={t('orders.imDialog.fields.senderCountry') as string}
              inputProps={{ maxLength: 2 }}
            />
          </Grid>
          <Grid item xs={12} sm={3}>
            <FormInput
              name="countryOfDispatch"
              control={control}
              label={t('orders.imDialog.fields.countryOfDispatch') as string}
              inputProps={{ maxLength: 2 }}
            />
          </Grid>
          <Grid item xs={12}>
            <FormInput
              name="senderAddress"
              control={control}
              label={t('orders.imDialog.fields.senderAddress') as string}
            />
          </Grid>
          <Grid item xs={12} sm={4}>
            <FormInput
              name="currency"
              control={control}
              label={t('orders.imDialog.fields.currency') as string}
              inputProps={{ maxLength: 3 }}
            />
          </Grid>
          <Grid item xs={12} sm={4}>
            <FormInput
              name="totalCustomsValue"
              control={control}
              label={t('orders.imDialog.fields.totalCustomsValue') as string}
              type="number"
              placeholder={linesTotal ? linesTotal.toFixed(2) : '0.00'}
            />
          </Grid>
        </Grid>

        <Divider sx={{ my: 3 }} />

        {/* ────────── Lines ────────── */}
        <Stack direction="row" alignItems="center" justifyContent="space-between" mb={1}>
          <Typography variant="overline" color="text.secondary">
            {t('orders.imDialog.section.lines')}
          </Typography>
          <Button
            size="small"
            startIcon={<AddIcon />}
            onClick={() => setLines((prev) => [...prev, { ...emptyLine }])}
          >
            {t('orders.imDialog.addLine')}
          </Button>
        </Stack>

        <Paper variant="outlined" sx={{ p: 0, overflow: 'auto' }}>
          <Box sx={{ minWidth: 980 }}>
            <Box sx={{ display: 'grid', gridTemplateColumns: '2fr 1.4fr 1fr 1fr 1.2fr 0.8fr 0.8fr 0.8fr 32px', gap: 1, p: 1, bgcolor: 'background.default', fontSize: 12, fontWeight: 600 }}>
              <span>{t('orders.imDialog.cols.item')}</span>
              <span>{t('orders.imDialog.cols.tariff')}</span>
              <span>{t('orders.imDialog.cols.qty')}</span>
              <span>{t('orders.imDialog.cols.uom')}</span>
              <span>{t('orders.imDialog.cols.customsValue')}</span>
              <span>{t('orders.imDialog.cols.origin')}</span>
              <span>{t('orders.imDialog.cols.dutyRate')}</span>
              <span>{t('orders.imDialog.cols.vatRate')}</span>
              <span />
            </Box>
            {lines.map((line, idx) => (
              <Box
                key={idx}
                sx={{ display: 'grid', gridTemplateColumns: '2fr 1.4fr 1fr 1fr 1.2fr 0.8fr 0.8fr 0.8fr 32px', gap: 1, p: 1, borderTop: 1, borderColor: 'divider', alignItems: 'center' }}
              >
                <TextField
                  select
                  size="small"
                  value={line.itemId}
                  onChange={(e) => updateLine(idx, { itemId: e.target.value })}
                  placeholder={t('orders.imDialog.cols.item') as string}
                >
                  <MenuItem value=""><em>—</em></MenuItem>
                  {itemOptions.map((o) => (
                    <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>
                  ))}
                </TextField>
                <TextField
                  size="small"
                  value={line.tariffCode}
                  onChange={(e) => updateLine(idx, { tariffCode: e.target.value })}
                />
                <TextField
                  size="small"
                  type="number"
                  value={line.quantity}
                  onChange={(e) => updateLine(idx, { quantity: e.target.value })}
                />
                <TextField
                  select
                  size="small"
                  value={line.uoMId}
                  onChange={(e) => updateLine(idx, { uoMId: e.target.value })}
                >
                  <MenuItem value=""><em>—</em></MenuItem>
                  {uomOptions.map((o) => (
                    <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>
                  ))}
                </TextField>
                <TextField
                  size="small"
                  type="number"
                  value={line.customsValue}
                  onChange={(e) => updateLine(idx, { customsValue: e.target.value })}
                />
                <TextField
                  size="small"
                  value={line.countryOfOrigin}
                  onChange={(e) => updateLine(idx, { countryOfOrigin: e.target.value.toUpperCase() })}
                  inputProps={{ maxLength: 2 }}
                />
                <TextField
                  size="small"
                  type="number"
                  value={line.dutyRate}
                  onChange={(e) => updateLine(idx, { dutyRate: e.target.value })}
                />
                <TextField
                  size="small"
                  type="number"
                  value={line.vatRate}
                  onChange={(e) => updateLine(idx, { vatRate: e.target.value })}
                />
                <IconButton
                  size="small"
                  disabled={lines.length === 1}
                  onClick={() => setLines((prev) => prev.filter((_, i) => i !== idx))}
                  aria-label="remove line"
                >
                  <DeleteOutlineIcon fontSize="small" />
                </IconButton>
              </Box>
            ))}
            <Box sx={{ display: 'flex', justifyContent: 'flex-end', p: 1, fontWeight: 600 }}>
              {t('orders.imDialog.linesTotal')}: {linesTotal.toFixed(2)}
            </Box>
          </Box>
        </Paper>
      </Box>
    </FormDialog>
  );
};

export default ImDeclarationDialog;
