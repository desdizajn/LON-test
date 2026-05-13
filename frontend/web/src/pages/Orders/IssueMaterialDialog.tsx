import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Box,
  Chip,
  Grid,
  LinearProgress,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { toast } from 'react-toastify';
import { useQueryClient } from '@tanstack/react-query';
import FormDialog from '../../components/common/FormDialog';
import { productionApi } from '../../services/api';
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
  item?: { code: string; name?: string } | null;
}

interface MaterialRow {
  id: string;
  itemId: string;
  requiredQuantity: number;
  issuedQuantity: number;
  uoMId: string;
  preAssignedBatchNumber?: string | null;
  preAssignedMRN?: string | null;
  item?: { code: string; name?: string } | null;
  uoM?: { code: string } | null;
}

const todayIso = () => new Date().toISOString().slice(0, 10);

/**
 * Phase 17 §E7 — Hub action "Издади материјал".
 *
 * Picks a Released-or-InProgress ProductionOrder for this ClientOrder, shows
 * the BOM-required-vs-issued breakdown for each material, and "Issue all"
 * fires `POST /api/Production/orders/{id}/issues/bulk` with today's date.
 * The bulk handler walks `ProductionOrderMaterial` rows and creates one
 * `MaterialIssue` per still-unissued material with FEFO auto-pick (or
 * pre-assigned batch/MRN when textile imports stamped one).
 */
const IssueMaterialDialog: React.FC<Props> = ({ open, order, onClose, onCreated }) => {
  const { t } = useTranslation();
  const qc = useQueryClient();

  const [pos, setPos] = useState<PoSummary[]>([]);
  const [poId, setPoId] = useState<string>('');
  const [materials, setMaterials] = useState<MaterialRow[]>([]);
  const [issueDate, setIssueDate] = useState<string>(todayIso());
  const [loadingMaterials, setLoadingMaterials] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    setPoId('');
    setMaterials([]);
    setIssueDate(todayIso());
    productionApi
      .getOrders({ clientOrderId: order.id })
      .then((r) => {
        const rows = (r.data ?? []) as PoSummary[];
        // Released = 2, InProgress = 3 per ProductionOrderStatus enum.
        setPos(rows.filter((p) => p.status === 2 || p.status === 3));
      })
      .catch(() => setPos([]));
  }, [open, order.id]);

  // Load PO detail (with Materials) once a PO is picked.
  useEffect(() => {
    if (!poId) {
      setMaterials([]);
      return;
    }
    setLoadingMaterials(true);
    productionApi
      .getOrder(poId)
      .then((r) => {
        const detail = r.data ?? {};
        setMaterials((detail.materials ?? []) as MaterialRow[]);
      })
      .catch(() => setMaterials([]))
      .finally(() => setLoadingMaterials(false));
  }, [poId]);

  const poOptions = useMemo(
    () =>
      pos.map((p) => ({
        value: p.id,
        label: `${p.orderNumber} · ${p.item ? (p.item.name ? `${p.item.code} — ${p.item.name}` : p.item.code) : '—'} · ${p.producedQuantity?.toFixed?.(2) ?? '0'}/${p.orderQuantity?.toFixed?.(2) ?? '0'}`,
      })),
    [pos],
  );

  const pendingMaterials = useMemo(
    () => materials.filter((m) => m.requiredQuantity - m.issuedQuantity > 0),
    [materials],
  );

  const totalPending = useMemo(
    () => pendingMaterials.reduce((s, m) => s + (m.requiredQuantity - m.issuedQuantity), 0),
    [pendingMaterials],
  );

  const onSubmit = async () => {
    if (!poId) {
      toast.error(t('orders.issueDialog.errors.pickPo') as string);
      return;
    }
    if (pendingMaterials.length === 0) {
      toast.error(t('orders.issueDialog.errors.nothingToIssue') as string);
      return;
    }
    try {
      setSubmitting(true);
      const resp = await productionApi.issueAllMaterials(poId, issueDate);
      const env = resp.data;
      if (!env?.isSuccess) {
        toast.error(env?.errorMessage || (t('orders.issueDialog.errors.failed') as string));
        return;
      }
      toast.success(
        t('orders.issueDialog.created', {
          lines: pendingMaterials.length,
          qty: totalPending.toFixed(2),
        }) as string,
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
        (t('orders.issueDialog.errors.failed') as string);
      toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={t('orders.issueDialog.title') as string}
      submitText={t('orders.issueDialog.submit') as string}
      cancelText={t('common.cancel') as string}
      onSubmit={onSubmit}
      isSubmitting={submitting}
      disableSubmit={!poId || pendingMaterials.length === 0}
      maxWidth="md"
    >
      <Box>
        <Box sx={{ p: 1.5, mb: 2, bgcolor: 'background.default', borderRadius: 1 }}>
          <Typography variant="caption" color="text.secondary">
            {t('orders.issueDialog.hint')}: <strong>{order.orderNumber}</strong>
          </Typography>
        </Box>

        {poOptions.length === 0 ? (
          <Alert severity="info" sx={{ mb: 2 }}>
            {t('orders.issueDialog.noEligiblePos')}
          </Alert>
        ) : (
          <Grid container spacing={2} sx={{ mb: 2 }}>
            <Grid item xs={12} sm={8}>
              <TextField
                select
                fullWidth
                size="small"
                label={t('orders.issueDialog.fields.po')}
                value={poId}
                onChange={(e) => setPoId(e.target.value)}
                SelectProps={{ native: true }}
                InputLabelProps={{ shrink: true }}
                helperText={t('orders.issueDialog.fields.poHelper')}
              >
                <option value="" />
                {poOptions.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </TextField>
            </Grid>
            <Grid item xs={12} sm={4}>
              <TextField
                fullWidth
                size="small"
                type="date"
                label={t('orders.issueDialog.fields.issueDate')}
                value={issueDate}
                onChange={(e) => setIssueDate(e.target.value)}
                InputLabelProps={{ shrink: true }}
              />
            </Grid>
          </Grid>
        )}

        {loadingMaterials && <LinearProgress />}

        {poId && !loadingMaterials && pendingMaterials.length === 0 && materials.length > 0 && (
          <Alert severity="success" sx={{ mb: 2 }}>
            {t('orders.issueDialog.allIssued')}
          </Alert>
        )}

        {poId && !loadingMaterials && materials.length === 0 && (
          <Alert severity="warning" sx={{ mb: 2 }}>
            {t('orders.issueDialog.noMaterials')}
          </Alert>
        )}

        {pendingMaterials.length > 0 && (
          <>
            <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 1 }}>
              <Typography variant="overline" color="text.secondary">
                {t('orders.issueDialog.section.materials')}
              </Typography>
              <Chip
                size="small"
                label={
                  t('orders.issueDialog.summary', {
                    lines: pendingMaterials.length,
                    qty: totalPending.toFixed(2),
                  }) as string
                }
              />
            </Stack>
            <Box
              sx={{
                display: 'grid',
                gridTemplateColumns: '1.6fr 0.8fr 0.8fr 0.8fr 1fr',
                gap: 0,
                fontSize: 13,
                border: 1,
                borderColor: 'divider',
                borderRadius: 1,
                overflow: 'hidden',
                mb: 2,
              }}
            >
              {[
                t('orders.issueDialog.cols.material'),
                t('orders.issueDialog.cols.required'),
                t('orders.issueDialog.cols.issued'),
                t('orders.issueDialog.cols.remaining'),
                t('orders.issueDialog.cols.preAssigned'),
              ].map((h, i) => (
                <Box
                  key={i}
                  sx={{
                    fontWeight: 600,
                    p: 1,
                    borderBottom: 1,
                    borderColor: 'divider',
                    bgcolor: 'background.default',
                    textAlign: i >= 1 && i <= 3 ? 'right' : 'left',
                  }}
                >
                  {h}
                </Box>
              ))}
              {pendingMaterials.map((m) => {
                const remaining = m.requiredQuantity - m.issuedQuantity;
                return (
                  <React.Fragment key={m.id}>
                    <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                      {m.item ? (m.item.name ? `${m.item.code} — ${m.item.name}` : m.item.code) : '—'}
                    </Box>
                    <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
                      {m.requiredQuantity.toFixed(2)} {m.uoM?.code ?? ''}
                    </Box>
                    <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
                      {m.issuedQuantity.toFixed(2)}
                    </Box>
                    <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right', fontWeight: 600 }}>
                      {remaining.toFixed(2)}
                    </Box>
                    <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontSize: 11 }}>
                      {m.preAssignedBatchNumber || m.preAssignedMRN
                        ? `${m.preAssignedBatchNumber ?? '—'} / ${m.preAssignedMRN ?? '—'}`
                        : t('orders.issueDialog.cols.fefoAuto')}
                    </Box>
                  </React.Fragment>
                );
              })}
            </Box>
            <Typography variant="caption" color="text.secondary">
              {t('orders.issueDialog.bulkHint')}
            </Typography>
          </>
        )}
      </Box>
    </FormDialog>
  );
};

export default IssueMaterialDialog;
