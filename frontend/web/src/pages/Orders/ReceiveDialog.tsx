import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import { Box, Grid, Typography } from '@mui/material';
import { toast } from 'react-toastify';
import { useQueryClient } from '@tanstack/react-query';
import FormDialog from '../../components/common/FormDialog';
import FormSelect from '../../components/forms/FormSelect';
import FormInput from '../../components/forms/FormInput';
import { customsApi, masterDataApi, wmsApi } from '../../services/api';
import { ClientOrderDto, clientOrderKeys } from '../../hooks/queries/useClientOrders';

interface DeclarationRow {
  id: string;
  declarationNumber: string;
  mrn: string;
  declarationDate: string;
  declarationType: string;
  status: number;
  lines?: Array<{ id: string; quantity: number }>;
}

interface WarehouseRow { id: string; code: string; name: string }
interface LocationRow { id: string; code: string; name?: string }

interface ReceiveForm {
  customsDeclarationId: string;
  warehouseId: string;
  targetLocationId: string;
  receiptDate: string;
  referenceNumber: string;
}

interface Props {
  open: boolean;
  order: ClientOrderDto;
  onClose: () => void;
  onCreated: (receiptId: string) => void;
}

const todayIso = () => new Date().toISOString().slice(0, 10);

/**
 * Phase 17 §E4 — Receive into warehouse from the hub.
 *
 * Lists declarations linked to this ClientOrder (excluding Draft / Cancelled),
 * lets the user pick one + warehouse + landing location, and POSTs the
 * `BulkReceiptFromDeclarationCommand` which explodes every declaration line
 * into a ReceiptLine in one call. Variance / per-line skart adjustments
 * happen on the dedicated Receipt screen — the hub flow is the 95% case.
 */
const ReceiveDialog: React.FC<Props> = ({ open, order, onClose, onCreated }) => {
  const { t } = useTranslation();
  const qc = useQueryClient();

  const [declarations, setDeclarations] = useState<DeclarationRow[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseRow[]>([]);
  const [locations, setLocations] = useState<LocationRow[]>([]);
  const [submitting, setSubmitting] = useState(false);

  const { control, handleSubmit, reset, watch } = useForm<ReceiveForm>({
    defaultValues: {
      customsDeclarationId: '',
      warehouseId: '',
      targetLocationId: '',
      receiptDate: todayIso(),
      referenceNumber: '',
    },
  });

  // Load when the dialog opens.
  useEffect(() => {
    if (!open) return;
    customsApi
      .getDeclarations({ clientOrderId: order.id })
      .then((r) => {
        const rows = (r.data ?? []) as DeclarationRow[];
        // Drop Draft (0) + Cancelled (99); Registered/Submitted/Cleared all qualify to receive.
        setDeclarations(rows.filter((d) => d.status !== 0 && d.status !== 99));
      })
      .catch(() => setDeclarations([]));
    masterDataApi.getWarehouses().then((r) => setWarehouses((r.data?.data ?? r.data ?? []) as WarehouseRow[])).catch(() => setWarehouses([]));
    reset({
      customsDeclarationId: '',
      warehouseId: '',
      targetLocationId: '',
      receiptDate: todayIso(),
      referenceNumber: '',
    });
  }, [open, order.id, reset]);

  const selectedWarehouseId = watch('warehouseId');
  useEffect(() => {
    if (!selectedWarehouseId) {
      setLocations([]);
      return;
    }
    masterDataApi
      .getLocations(selectedWarehouseId)
      .then((r) => setLocations((r.data?.data ?? r.data ?? []) as LocationRow[]))
      .catch(() => setLocations([]));
  }, [selectedWarehouseId]);

  const declOptions = useMemo(
    () =>
      declarations.map((d) => ({
        value: d.id,
        label: `${d.declarationNumber} · ${d.mrn} · ${d.lines?.length ?? 0} ${t('orders.receiveDialog.lines') as string}`,
      })),
    [declarations, t],
  );
  const warehouseOptions = useMemo(
    () => warehouses.map((w) => ({ value: w.id, label: w.name ? `${w.code} — ${w.name}` : w.code })),
    [warehouses],
  );
  const locationOptions = useMemo(
    () => locations.map((l) => ({ value: l.id, label: l.name ? `${l.code} — ${l.name}` : l.code })),
    [locations],
  );

  const selectedDecl = useMemo(
    () => declarations.find((d) => d.id === watch('customsDeclarationId')),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [declarations, watch('customsDeclarationId')],
  );

  const onSubmit = async (data: ReceiveForm) => {
    if (!data.customsDeclarationId) {
      toast.error(t('orders.receiveDialog.pickDeclaration') as string);
      return;
    }
    if (!data.warehouseId) {
      toast.error(t('orders.receiveDialog.pickWarehouse') as string);
      return;
    }
    try {
      setSubmitting(true);
      const resp = await wmsApi.bulkReceiptFromDeclaration({
        customsDeclarationId: data.customsDeclarationId,
        warehouseId: data.warehouseId,
        targetLocationId: data.targetLocationId || null,
        receiptDate: data.receiptDate || null,
        referenceNumber: data.referenceNumber || null,
      });
      const env = resp.data;
      if (!env?.isSuccess) {
        toast.error(env?.errorMessage || (t('orders.receiveDialog.failed') as string));
        return;
      }
      toast.success(t('orders.receiveDialog.created') as string);
      qc.invalidateQueries({ queryKey: clientOrderKeys.all });
      qc.invalidateQueries({ queryKey: ['clientOrders', 'receipts', order.id] });
      onCreated(env.data?.receiptId as string);
    } catch (err: any) {
      const msg =
        err?.response?.data?.errorMessage ||
        err?.response?.data?.message ||
        err?.message ||
        (t('orders.receiveDialog.failed') as string);
      toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={t('orders.receiveDialog.title') as string}
      submitText={t('orders.receiveDialog.submit') as string}
      cancelText={t('common.cancel') as string}
      onSubmit={handleSubmit(onSubmit)}
      isSubmitting={submitting}
      maxWidth="md"
    >
      <Box>
        <Box sx={{ p: 1.5, mb: 2, bgcolor: 'background.default', borderRadius: 1 }}>
          <Typography variant="caption" color="text.secondary">
            {t('orders.receiveDialog.hint')}: <strong>{order.orderNumber}</strong>
          </Typography>
        </Box>

        {declOptions.length === 0 ? (
          <Typography color="text.secondary" sx={{ py: 2 }}>
            {t('orders.receiveDialog.noDeclarations')}
          </Typography>
        ) : (
          <Grid container spacing={2}>
            <Grid item xs={12}>
              <FormSelect
                name="customsDeclarationId"
                control={control}
                label={t('orders.receiveDialog.declaration') as string}
                options={declOptions}
                rules={{ required: t('orders.receiveDialog.pickDeclaration') as string }}
              />
              {selectedDecl && (
                <Typography variant="caption" color="text.secondary">
                  MRN: {selectedDecl.mrn} · {selectedDecl.lines?.length ?? 0} {t('orders.receiveDialog.lines')}
                </Typography>
              )}
            </Grid>
            <Grid item xs={12} sm={6}>
              <FormSelect
                name="warehouseId"
                control={control}
                label={t('orders.receiveDialog.warehouse') as string}
                options={warehouseOptions}
                rules={{ required: t('orders.receiveDialog.pickWarehouse') as string }}
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <FormSelect
                name="targetLocationId"
                control={control}
                label={t('orders.receiveDialog.location') as string}
                options={locationOptions}
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <FormInput
                name="receiptDate"
                control={control}
                label={t('orders.receiveDialog.receiptDate') as string}
                type="date"
                InputLabelProps={{ shrink: true }}
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <FormInput
                name="referenceNumber"
                control={control}
                label={t('orders.receiveDialog.referenceNumber') as string}
                placeholder="(auto)"
              />
            </Grid>
          </Grid>
        )}
      </Box>
    </FormDialog>
  );
};

export default ReceiveDialog;
