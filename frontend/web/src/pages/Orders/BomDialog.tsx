import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import { Alert, Box, Grid, Stack, Typography } from '@mui/material';
import { toast } from 'react-toastify';
import { useQueryClient } from '@tanstack/react-query';
import FormDialog from '../../components/common/FormDialog';
import FormInput from '../../components/forms/FormInput';
import FormSelect from '../../components/forms/FormSelect';
import { clientOrdersApi, masterDataApi, productionApi } from '../../services/api';
import { ClientOrderDto, clientOrderKeys } from '../../hooks/queries/useClientOrders';

interface ItemRow { id: string; code: string; name?: string; itemType?: number; uoMId?: string }
interface UoMRow { id: string; code: string }
interface BomRow { id: string; bomNumber?: string; itemId: string; version?: number }

interface BomForm {
  itemId: string;
  quantity: string;
  uoMId: string;
  bomId: string;
  unitPriceForeign: string;
  currency: string;
  notes: string;
  /** If checked, also create a ProductionOrder for the finished good. */
  createProductionOrder: boolean;
  plannedStartDate: string;
  plannedEndDate: string;
}

interface Props {
  open: boolean;
  order: ClientOrderDto;
  onClose: () => void;
  onCreated: () => void;
}

const todayIso = () => new Date().toISOString().slice(0, 10);
const addDaysIso = (days: number) =>
  new Date(Date.now() + days * 86400000).toISOString().slice(0, 10);

/**
 * Phase 17 §E5 — adds a ClientOrderFinishedGood row + optionally creates a
 * ProductionOrder in one step. Smart prefill (BLUEPRINT §7.3): the BOM dropdown
 * lists active BOMs for the chosen item; the most-recent-version partner-scoped
 * BOM is selected by default when present.
 */
const BomDialog: React.FC<Props> = ({ open, order, onClose, onCreated }) => {
  const { t } = useTranslation();
  const qc = useQueryClient();

  const [items, setItems] = useState<ItemRow[]>([]);
  const [uoms, setUoms] = useState<UoMRow[]>([]);
  const [boms, setBoms] = useState<BomRow[]>([]);
  const [submitting, setSubmitting] = useState(false);

  const { control, handleSubmit, reset, watch, setValue } = useForm<BomForm>({
    defaultValues: {
      itemId: '',
      quantity: '',
      uoMId: '',
      bomId: '',
      unitPriceForeign: '',
      currency: 'EUR',
      notes: '',
      createProductionOrder: true,
      plannedStartDate: todayIso(),
      plannedEndDate: addDaysIso(14),
    },
  });

  useEffect(() => {
    if (!open) return;
    masterDataApi.getItems().then((r) => {
      const all = (r.data?.data ?? r.data ?? []) as ItemRow[];
      // Finished-good item type = 1 (per master-data conventions); fall back to "all" when empty.
      const fg = all.filter((i) => i.itemType === 1);
      setItems(fg.length ? fg : all);
    }).catch(() => setItems([]));
    masterDataApi.getUoM().then((r) => setUoms((r.data?.data ?? r.data ?? []) as UoMRow[])).catch(() => setUoms([]));
    reset({
      itemId: '',
      quantity: '',
      uoMId: '',
      bomId: '',
      unitPriceForeign: '',
      currency: order.finishedGoods[0]?.currency ?? 'EUR',
      notes: '',
      createProductionOrder: true,
      plannedStartDate: todayIso(),
      plannedEndDate: addDaysIso(14),
    });
    setBoms([]);
  }, [open, order, reset]);

  // When item changes — auto-fill UoM from the item + load BOMs for that item.
  const selectedItemId = watch('itemId');
  useEffect(() => {
    if (!selectedItemId) {
      setBoms([]);
      return;
    }
    const item = items.find((i) => i.id === selectedItemId);
    if (item?.uoMId) setValue('uoMId', item.uoMId);
    productionApi.getBOMs(selectedItemId).then((r) => {
      setBoms((r.data?.data ?? r.data ?? []) as BomRow[]);
    }).catch(() => setBoms([]));
  }, [selectedItemId, items, setValue]);

  // BLUEPRINT §7.3 — prefill the most-recent BOM by default.
  useEffect(() => {
    if (boms.length === 0) return;
    const sorted = [...boms].sort((a, b) => (b.version ?? 0) - (a.version ?? 0));
    setValue('bomId', sorted[0].id);
  }, [boms, setValue]);

  const itemOptions = useMemo(
    () => items.map((i) => ({ value: i.id, label: i.name ? `${i.code} — ${i.name}` : i.code })),
    [items],
  );
  const uomOptions = useMemo(() => uoms.map((u) => ({ value: u.id, label: u.code })), [uoms]);
  const bomOptions = useMemo(
    () =>
      boms.map((b) => ({
        value: b.id,
        label: `${b.bomNumber ?? b.id.slice(0, 8)}${b.version ? ` (v${b.version})` : ''}`,
      })),
    [boms],
  );

  const createPO = watch('createProductionOrder');

  const onSubmit = async (data: BomForm) => {
    if (!data.itemId || !data.uoMId || !data.quantity) {
      toast.error(t('orders.bomDialog.missingRequired') as string);
      return;
    }
    try {
      setSubmitting(true);
      // Step 1 — persist the FG row.
      const fgResp = await clientOrdersApi.addFinishedGood(order.id, {
        itemId: data.itemId,
        quantity: parseFloat(data.quantity) || 0,
        uoMId: data.uoMId,
        bomId: data.bomId || null,
        unitPriceForeign: data.unitPriceForeign ? parseFloat(data.unitPriceForeign) : null,
        currency: data.currency || 'EUR',
        notes: data.notes || null,
      });
      if (!fgResp.data?.isSuccess) {
        toast.error(fgResp.data?.errorMessage || (t('orders.bomDialog.failedFG') as string));
        return;
      }

      // Step 2 — optionally fire a ProductionOrder against the same item + BOM.
      if (data.createProductionOrder) {
        const poResp = await productionApi.createOrder({
          itemId: data.itemId,
          orderQuantity: parseFloat(data.quantity) || 0,
          uoMId: data.uoMId,
          plannedStartDate: data.plannedStartDate,
          plannedEndDate: data.plannedEndDate,
          bomId: data.bomId || null,
          partnerId: order.customerPartnerId,
          clientOrderId: order.id,
          salesOrderReference: order.orderNumber,
        });
        if (!poResp.data?.isSuccess) {
          toast.warn(
            (t('orders.bomDialog.fgCreatedButPOFailed') as string) +
            ': ' + (poResp.data?.errorMessage ?? '?'),
          );
          // Don't return — FG row is already persisted; user can retry PO from the dedicated page.
        }
      }

      toast.success(t('orders.bomDialog.created') as string);
      qc.invalidateQueries({ queryKey: clientOrderKeys.all });
      onCreated();
    } catch (err: any) {
      const msg =
        err?.response?.data?.errorMessage ||
        err?.response?.data?.message ||
        err?.message ||
        (t('orders.bomDialog.failedFG') as string);
      toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={t('orders.bomDialog.title') as string}
      submitText={t('orders.bomDialog.submit') as string}
      cancelText={t('common.cancel') as string}
      onSubmit={handleSubmit(onSubmit)}
      isSubmitting={submitting}
      maxWidth="md"
    >
      <Box>
        <Box sx={{ p: 1.5, mb: 2, bgcolor: 'background.default', borderRadius: 1 }}>
          <Typography variant="caption" color="text.secondary">
            {t('orders.bomDialog.hint')}: <strong>{order.orderNumber}</strong> · {order.customerPartnerName ?? '—'}
          </Typography>
        </Box>

        <Stack spacing={2}>
          <Typography variant="overline" color="text.secondary">
            {t('orders.bomDialog.section.fg')}
          </Typography>
          <Grid container spacing={2}>
            <Grid item xs={12} sm={6}>
              <FormSelect
                name="itemId"
                control={control}
                label={t('orders.bomDialog.fields.item') as string}
                options={itemOptions}
                rules={{ required: t('orders.bomDialog.fields.itemRequired') as string }}
              />
            </Grid>
            <Grid item xs={6} sm={3}>
              <FormInput
                name="quantity"
                control={control}
                label={t('orders.bomDialog.fields.quantity') as string}
                type="number"
                rules={{ required: true }}
              />
            </Grid>
            <Grid item xs={6} sm={3}>
              <FormSelect
                name="uoMId"
                control={control}
                label={t('orders.bomDialog.fields.uom') as string}
                options={uomOptions}
                rules={{ required: true }}
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <FormSelect
                name="bomId"
                control={control}
                label={t('orders.bomDialog.fields.bom') as string}
                options={bomOptions.length ? bomOptions : [{ value: '', label: t('orders.bomDialog.fields.noBom') as string }]}
              />
              {boms.length > 0 && (
                <Typography variant="caption" color="text.secondary">
                  {t('orders.bomDialog.bomCount', { count: boms.length })}
                </Typography>
              )}
              {selectedItemId && boms.length === 0 && (
                <Alert severity="info" sx={{ mt: 1 }}>
                  {t('orders.bomDialog.noBomsForItem')}
                </Alert>
              )}
            </Grid>
            <Grid item xs={6} sm={3}>
              <FormInput
                name="unitPriceForeign"
                control={control}
                label={t('orders.bomDialog.fields.unitPrice') as string}
                type="number"
              />
            </Grid>
            <Grid item xs={6} sm={3}>
              <FormInput
                name="currency"
                control={control}
                label={t('orders.bomDialog.fields.currency') as string}
                inputProps={{ maxLength: 3 }}
              />
            </Grid>
            <Grid item xs={12}>
              <FormInput
                name="notes"
                control={control}
                label={t('orders.bomDialog.fields.notes') as string}
                multiline
                rows={2}
              />
            </Grid>
          </Grid>

          <Typography variant="overline" color="text.secondary">
            {t('orders.bomDialog.section.po')}
          </Typography>
          <Grid container spacing={2}>
            <Grid item xs={12}>
              <label>
                <input
                  type="checkbox"
                  checked={createPO}
                  onChange={(e) => setValue('createProductionOrder', e.target.checked)}
                  style={{ marginRight: 8 }}
                />
                {t('orders.bomDialog.fields.alsoCreatePO')}
              </label>
            </Grid>
            <Grid item xs={12} sm={6}>
              <FormInput
                name="plannedStartDate"
                control={control}
                label={t('orders.bomDialog.fields.plannedStart') as string}
                type="date"
                InputLabelProps={{ shrink: true }}
                disabled={!createPO}
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <FormInput
                name="plannedEndDate"
                control={control}
                label={t('orders.bomDialog.fields.plannedEnd') as string}
                type="date"
                InputLabelProps={{ shrink: true }}
                disabled={!createPO}
              />
            </Grid>
          </Grid>
        </Stack>
      </Box>
    </FormDialog>
  );
};

export default BomDialog;
