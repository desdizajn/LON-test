import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Box,
  Grid,
  LinearProgress,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { toast } from 'react-toastify';
import { useQueryClient } from '@tanstack/react-query';
import FormDialog from '../../components/common/FormDialog';
import { masterDataApi, productionApi } from '../../services/api';
import { ClientOrderDto, clientOrderKeys } from '../../hooks/queries/useClientOrders';

interface Props {
  open: boolean;
  order: ClientOrderDto;
  onClose: () => void;
  onCreated: () => void;
}

interface PoSummary {
  id: string;
  orderNumber: string;
  status: number;
  orderQuantity: number;
  producedQuantity: number;
  scrapQuantity?: number;
  item?: { id: string; code: string; name?: string } | null;
  itemId?: string;
  uoMId?: string;
  uoM?: { id: string; code: string } | null;
}

interface PoDetail extends PoSummary {
  scrapQuantity: number;
}

interface WarehouseRow {
  id: string;
  code: string;
  name: string;
}

interface LocationRow {
  id: string;
  code: string;
  name?: string;
  type?: number;
  warehouseId?: string;
}

const todayIso = () => new Date().toISOString().slice(0, 10);

/**
 * Phase 17 §E7 — Hub action "Запиши производство".
 *
 * Picks a Released-or-InProgress ProductionOrder for this ClientOrder; auto-
 * suggests qty (remaining = orderQty − produced − scrap), an FG batch number
 * (`FG-{ItemCode}-{YYYYMMDD}`), and Quality OK. Hitting "Запиши" fires
 * `POST /api/Production/orders/{id}/receipts`. The server upserts the FG
 * InventoryBalance, links to MaterialIssues for traceability, and flips
 * status to Completed when ProducedQuantity + ScrapQuantity ≥ OrderQuantity.
 */
const ProductionReceiptDialog: React.FC<Props> = ({ open, order, onClose, onCreated }) => {
  const { t } = useTranslation();
  const qc = useQueryClient();

  const [pos, setPos] = useState<PoSummary[]>([]);
  const [poId, setPoId] = useState<string>('');
  const [poDetail, setPoDetail] = useState<PoDetail | null>(null);
  const [warehouses, setWarehouses] = useState<WarehouseRow[]>([]);
  const [locations, setLocations] = useState<LocationRow[]>([]);
  const [warehouseId, setWarehouseId] = useState<string>('');
  const [locationId, setLocationId] = useState<string>('');
  const [quantity, setQuantity] = useState<string>('');
  const [scrapQuantity, setScrapQuantity] = useState<string>('0');
  const [batchNumber, setBatchNumber] = useState<string>('');
  const [receiptDate, setReceiptDate] = useState<string>(todayIso());
  const [qualityStatus, setQualityStatus] = useState<number>(1); // OK
  const [submitting, setSubmitting] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);

  useEffect(() => {
    if (!open) return;
    setPoId('');
    setPoDetail(null);
    setQuantity('');
    setScrapQuantity('0');
    setBatchNumber('');
    setReceiptDate(todayIso());
    setQualityStatus(1);
    setWarehouseId('');
    setLocationId('');

    productionApi
      .getOrders({ clientOrderId: order.id })
      .then((r) => {
        const rows = (r.data ?? []) as PoSummary[];
        // Released = 2, InProgress = 3. Completed/Cancelled excluded.
        setPos(rows.filter((p) => p.status === 2 || p.status === 3));
      })
      .catch(() => setPos([]));

    masterDataApi
      .getWarehouses()
      .then((r) => {
        const rows = (r.data?.data ?? r.data ?? []) as WarehouseRow[];
        setWarehouses(rows);
        if (rows.length > 0) setWarehouseId(rows[0].id);
      })
      .catch(() => setWarehouses([]));
  }, [open, order.id]);

  // Load PO detail when one is picked; auto-suggest qty + batch.
  useEffect(() => {
    if (!poId) {
      setPoDetail(null);
      return;
    }
    setLoadingDetail(true);
    productionApi
      .getOrder(poId)
      .then((r) => {
        const d = (r.data ?? {}) as PoDetail;
        setPoDetail(d);
        const remaining = (d.orderQuantity ?? 0) - (d.producedQuantity ?? 0) - (d.scrapQuantity ?? 0);
        setQuantity(remaining > 0 ? remaining.toFixed(2) : '0');
        const itemCode = d.item?.code ?? 'FG';
        const dateTag = todayIso().replace(/-/g, '');
        setBatchNumber(`FG-${itemCode}-${dateTag}`);
      })
      .catch(() => setPoDetail(null))
      .finally(() => setLoadingDetail(false));
  }, [poId]);

  // Load locations when warehouse changes; default to Production stage if present.
  useEffect(() => {
    if (!warehouseId) {
      setLocations([]);
      setLocationId('');
      return;
    }
    masterDataApi
      .getLocations(warehouseId)
      .then((r) => {
        const rows = (r.data?.data ?? r.data ?? []) as LocationRow[];
        setLocations(rows);
        // Prefer Production stage (LocationType.Production = 4) or code prefix PROD/FG.
        const preferred =
          rows.find((l) => l.type === 4) ||
          rows.find((l) => /^(PROD|FG|FINISHED)/i.test(l.code)) ||
          rows[0];
        setLocationId(preferred?.id ?? '');
      })
      .catch(() => setLocations([]));
  }, [warehouseId]);

  const poOptions = useMemo(
    () =>
      pos.map((p) => ({
        value: p.id,
        label: `${p.orderNumber} · ${p.item ? (p.item.name ? `${p.item.code} — ${p.item.name}` : p.item.code) : '—'} · ${(p.producedQuantity ?? 0).toFixed(2)}/${(p.orderQuantity ?? 0).toFixed(2)}`,
      })),
    [pos],
  );

  const warehouseOptions = useMemo(
    () => warehouses.map((w) => ({ value: w.id, label: w.name ? `${w.code} — ${w.name}` : w.code })),
    [warehouses],
  );
  const locationOptions = useMemo(
    () => locations.map((l) => ({ value: l.id, label: l.name ? `${l.code} — ${l.name}` : l.code })),
    [locations],
  );

  const qtyNum = parseFloat(quantity) || 0;
  const scrapNum = parseFloat(scrapQuantity) || 0;
  const remainingAfter = useMemo(() => {
    if (!poDetail) return 0;
    const r = (poDetail.orderQuantity ?? 0) - (poDetail.producedQuantity ?? 0) - (poDetail.scrapQuantity ?? 0) - qtyNum - scrapNum;
    return r;
  }, [poDetail, qtyNum, scrapNum]);

  const willComplete = useMemo(() => {
    if (!poDetail) return false;
    const after = (poDetail.producedQuantity ?? 0) + qtyNum + (poDetail.scrapQuantity ?? 0) + scrapNum;
    return after >= (poDetail.orderQuantity ?? 0);
  }, [poDetail, qtyNum, scrapNum]);

  const onSubmit = async () => {
    if (!poId || !poDetail) {
      toast.error(t('orders.receiptDialog.errors.pickPo') as string);
      return;
    }
    if (qtyNum <= 0) {
      toast.error(t('orders.receiptDialog.errors.qtyRequired') as string);
      return;
    }
    if (!batchNumber.trim()) {
      toast.error(t('orders.receiptDialog.errors.batchRequired') as string);
      return;
    }
    if (!locationId) {
      toast.error(t('orders.receiptDialog.errors.locationRequired') as string);
      return;
    }
    const itemId = poDetail.itemId ?? poDetail.item?.id;
    const uoMId = poDetail.uoMId ?? poDetail.uoM?.id;
    if (!itemId || !uoMId) {
      toast.error(t('orders.receiptDialog.errors.poMissingMetadata') as string);
      return;
    }

    try {
      setSubmitting(true);
      const resp = await productionApi.createReceiptForOrder(poId, {
        receiptDate,
        itemId,
        uoMId,
        quantity: qtyNum,
        scrapQuantity: scrapNum > 0 ? scrapNum : null,
        batchNumber: batchNumber.trim(),
        locationId,
        qualityStatus,
      });
      const env = resp.data;
      if (!env?.isSuccess) {
        toast.error(env?.errorMessage || (t('orders.receiptDialog.errors.failed') as string));
        return;
      }
      toast.success(
        t('orders.receiptDialog.created', { qty: qtyNum.toFixed(2) }) as string,
      );
      qc.invalidateQueries({ queryKey: clientOrderKeys.all });
      qc.invalidateQueries({ queryKey: ['clientOrders', 'productionOrders', order.id] });
      qc.invalidateQueries({ queryKey: ['clientOrders', 'materials', order.id] });
      onCreated();
    } catch (err: any) {
      const msg =
        err?.response?.data?.errorMessage ||
        err?.response?.data?.message ||
        err?.message ||
        (t('orders.receiptDialog.errors.failed') as string);
      toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={t('orders.receiptDialog.title') as string}
      submitText={t('orders.receiptDialog.submit') as string}
      cancelText={t('common.cancel') as string}
      onSubmit={onSubmit}
      isSubmitting={submitting}
      disableSubmit={!poId || qtyNum <= 0 || !batchNumber.trim() || !locationId}
      maxWidth="md"
    >
      <Box>
        <Box sx={{ p: 1.5, mb: 2, bgcolor: 'background.default', borderRadius: 1 }}>
          <Typography variant="caption" color="text.secondary">
            {t('orders.receiptDialog.hint')}: <strong>{order.orderNumber}</strong>
          </Typography>
        </Box>

        {poOptions.length === 0 ? (
          <Alert severity="info" sx={{ mb: 2 }}>
            {t('orders.receiptDialog.noEligiblePos')}
          </Alert>
        ) : (
          <Grid container spacing={2} sx={{ mb: 1 }}>
            <Grid item xs={12}>
              <TextField
                select
                fullWidth
                size="small"
                label={t('orders.receiptDialog.fields.po')}
                value={poId}
                onChange={(e) => setPoId(e.target.value)}
                SelectProps={{ native: true }}
                InputLabelProps={{ shrink: true }}
              >
                <option value="" />
                {poOptions.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </TextField>
            </Grid>
          </Grid>
        )}

        {loadingDetail && <LinearProgress />}

        {poDetail && (
          <>
            <Box sx={{ p: 1.5, mb: 2, bgcolor: 'info.lighter', borderRadius: 1, fontSize: 13 }}>
              <Stack direction="row" spacing={3} flexWrap="wrap">
                <span>
                  <strong>{t('orders.receiptDialog.fields.item')}:</strong>{' '}
                  {poDetail.item?.code ?? '—'}
                </span>
                <span>
                  <strong>{t('orders.receiptDialog.fields.uom')}:</strong>{' '}
                  {poDetail.uoM?.code ?? '—'}
                </span>
                <span>
                  <strong>{t('orders.receiptDialog.fields.orderQty')}:</strong>{' '}
                  {poDetail.orderQuantity?.toFixed(2)}
                </span>
                <span>
                  <strong>{t('orders.receiptDialog.fields.produced')}:</strong>{' '}
                  {poDetail.producedQuantity?.toFixed(2) ?? '0.00'}
                </span>
                <span>
                  <strong>{t('orders.receiptDialog.fields.scrap')}:</strong>{' '}
                  {poDetail.scrapQuantity?.toFixed?.(2) ?? '0.00'}
                </span>
              </Stack>
            </Box>

            <Grid container spacing={2}>
              <Grid item xs={6} sm={3}>
                <TextField
                  fullWidth
                  size="small"
                  type="number"
                  label={t('orders.receiptDialog.fields.quantity')}
                  value={quantity}
                  onChange={(e) => setQuantity(e.target.value)}
                  inputProps={{ min: 0, step: '0.01' }}
                  helperText={t('orders.receiptDialog.fields.quantityHelper')}
                />
              </Grid>
              <Grid item xs={6} sm={3}>
                <TextField
                  fullWidth
                  size="small"
                  type="number"
                  label={t('orders.receiptDialog.fields.scrapQty')}
                  value={scrapQuantity}
                  onChange={(e) => setScrapQuantity(e.target.value)}
                  inputProps={{ min: 0, step: '0.01' }}
                />
              </Grid>
              <Grid item xs={12} sm={6}>
                <TextField
                  fullWidth
                  size="small"
                  label={t('orders.receiptDialog.fields.batch')}
                  value={batchNumber}
                  onChange={(e) => setBatchNumber(e.target.value)}
                  placeholder="FG-…"
                />
              </Grid>
              <Grid item xs={12} sm={6}>
                <TextField
                  select
                  fullWidth
                  size="small"
                  label={t('orders.receiptDialog.fields.warehouse')}
                  value={warehouseId}
                  onChange={(e) => setWarehouseId(e.target.value)}
                  SelectProps={{ native: true }}
                  InputLabelProps={{ shrink: true }}
                >
                  <option value="" />
                  {warehouseOptions.map((o) => (
                    <option key={o.value} value={o.value}>
                      {o.label}
                    </option>
                  ))}
                </TextField>
              </Grid>
              <Grid item xs={12} sm={6}>
                <TextField
                  select
                  fullWidth
                  size="small"
                  label={t('orders.receiptDialog.fields.location')}
                  value={locationId}
                  onChange={(e) => setLocationId(e.target.value)}
                  SelectProps={{ native: true }}
                  InputLabelProps={{ shrink: true }}
                >
                  <option value="" />
                  {locationOptions.map((o) => (
                    <option key={o.value} value={o.value}>
                      {o.label}
                    </option>
                  ))}
                </TextField>
              </Grid>
              <Grid item xs={6} sm={3}>
                <TextField
                  fullWidth
                  size="small"
                  type="date"
                  label={t('orders.receiptDialog.fields.receiptDate')}
                  value={receiptDate}
                  onChange={(e) => setReceiptDate(e.target.value)}
                  InputLabelProps={{ shrink: true }}
                />
              </Grid>
              <Grid item xs={6} sm={3}>
                <TextField
                  select
                  fullWidth
                  size="small"
                  label={t('orders.receiptDialog.fields.qualityStatus')}
                  value={qualityStatus}
                  onChange={(e) => setQualityStatus(Number(e.target.value))}
                  SelectProps={{ native: true }}
                  InputLabelProps={{ shrink: true }}
                >
                  <option value={1}>{t('qualityStatus.ok')}</option>
                  <option value={2}>{t('qualityStatus.quarantine')}</option>
                  <option value={3}>{t('qualityStatus.rejected')}</option>
                </TextField>
              </Grid>
            </Grid>

            {willComplete && qtyNum > 0 && (
              <Alert severity="info" sx={{ mt: 2 }}>
                {t('orders.receiptDialog.willComplete')}
              </Alert>
            )}
            {!willComplete && remainingAfter > 0 && qtyNum > 0 && (
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
                {t('orders.receiptDialog.remainingAfter', { qty: remainingAfter.toFixed(2) })}
              </Typography>
            )}
          </>
        )}
      </Box>
    </FormDialog>
  );
};

export default ProductionReceiptDialog;
