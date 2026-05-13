import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  Grid,
  IconButton,
  LinearProgress,
  Paper,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import RefreshIcon from '@mui/icons-material/Refresh';
import LightbulbIcon from '@mui/icons-material/Lightbulb';
import { toast } from 'react-toastify';
import { useQueryClient } from '@tanstack/react-query';
import FormDialog from '../../components/common/FormDialog';
import { masterDataApi, suggestionsApi, wmsApi } from '../../services/api';
import { ClientOrderDto, clientOrderKeys } from '../../hooks/queries/useClientOrders';

interface Props {
  open: boolean;
  order: ClientOrderDto;
  onClose: () => void;
  onCreated: () => void;
}

interface WarehouseRow {
  id: string;
  code: string;
  name: string;
}

interface PartnerRow {
  id: string;
  code: string;
  name: string;
  partnerType?: number;
}

interface InventoryRow {
  id: string;
  itemId: string;
  locationId: string;
  batchNumber?: string | null;
  mrn?: string | null;
  quantity: number;
  qualityStatus: number;
  lonProcessState?: number | null;
  assignedProducerId?: string | null;
  item?: { code: string; name?: string } | null;
  location?: { code: string; name?: string } | null;
  uoM?: { code: string } | null;
}

interface SuggestionResponse {
  producerId: string;
  code: string;
  name: string;
  score: number;
  reason: string;
  recentAssignmentCount?: number;
  recentTotalQuantity?: number;
}

/**
 * Phase 17 §E6 — Hub action "Распредели подизведувач".
 *
 * Distributes raw-material inventory at the chosen warehouse to ONE
 * sub-contractor producer. The physical stock stays put; the dialog stamps
 * `AssignedProducerId` on per-producer sibling InventoryBalance rows so the
 * Materials tab on the hub (and downstream MaterialIssue flow in §E7) can
 * filter by producer.
 *
 * Inventory is scoped server-side by `clientOrderId` to the materials
 * referenced by any ProductionOrder linked to this ClientOrder. When no POs
 * exist yet (§E5 hasn't run), the dialog warns the user and falls back to
 * "Прикажи сè" — all unassigned balances at the warehouse.
 */
const PodelbaDialog: React.FC<Props> = ({ open, order, onClose, onCreated }) => {
  const { t } = useTranslation();
  const qc = useQueryClient();

  const [warehouses, setWarehouses] = useState<WarehouseRow[]>([]);
  const [producers, setProducers] = useState<PartnerRow[]>([]);
  const [suggestion, setSuggestion] = useState<SuggestionResponse | null>(null);
  const [inventory, setInventory] = useState<InventoryRow[]>([]);
  const [warehouseId, setWarehouseId] = useState<string>('');
  const [producerId, setProducerId] = useState<string>('');
  const [reason, setReason] = useState<string>('');
  const [showAllMaterials, setShowAllMaterials] = useState(false);
  const [pickedQty, setPickedQty] = useState<Record<string, string>>({});
  const [loadingInventory, setLoadingInventory] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  // Reset on open.
  useEffect(() => {
    if (!open) return;
    setSuggestion(null);
    setInventory([]);
    setPickedQty({});
    setShowAllMaterials(false);
    setReason('');
    setProducerId('');
    setWarehouseId('');

    masterDataApi
      .getWarehouses()
      .then((r) => {
        const rows = (r.data?.data ?? r.data ?? []) as WarehouseRow[];
        setWarehouses(rows);
        if (rows.length > 0) setWarehouseId(rows[0].id);
      })
      .catch(() => setWarehouses([]));

    // PartnerType.Producer = 6 (legacy enum value).
    masterDataApi
      .getPartners('6')
      .then((r) => setProducers((r.data?.data ?? r.data ?? []) as PartnerRow[]))
      .catch(() => setProducers([]));

    suggestionsApi
      .producer(order.id)
      .then((r) => {
        if (r.status === 204 || !r.data) {
          setSuggestion(null);
          return;
        }
        setSuggestion(r.data as SuggestionResponse);
      })
      .catch(() => setSuggestion(null));
  }, [open, order.id]);

  // (Re)load inventory whenever warehouse / scope toggle changes.
  useEffect(() => {
    if (!open || !warehouseId) {
      setInventory([]);
      return;
    }
    setLoadingInventory(true);
    wmsApi
      .getInventory(undefined, undefined, {
        warehouseId,
        clientOrderId: showAllMaterials ? null : order.id,
        unassignedOnly: true,
      })
      .then((r) => {
        const rows = (r.data ?? []) as InventoryRow[];
        // Defensive client-side filters in case the backend variant returns extras.
        setInventory(
          rows.filter(
            (b) => !b.assignedProducerId && (b.quantity ?? 0) > 0 && b.qualityStatus === 1,
          ),
        );
      })
      .catch(() => setInventory([]))
      .finally(() => setLoadingInventory(false));
  }, [open, warehouseId, showAllMaterials, order.id]);

  const producerOptions = useMemo(
    () =>
      producers
        .filter((p) => (p.partnerType ?? 6) === 6)
        .map((p) => ({ value: p.id, label: p.name ? `${p.code} — ${p.name}` : p.code })),
    [producers],
  );

  const warehouseOptions = useMemo(
    () => warehouses.map((w) => ({ value: w.id, label: w.name ? `${w.code} — ${w.name}` : w.code })),
    [warehouses],
  );

  const acceptSuggestion = () => {
    if (suggestion) setProducerId(suggestion.producerId);
  };

  const setQty = (balanceId: string, value: string) => {
    setPickedQty((prev) => ({ ...prev, [balanceId]: value }));
  };

  const fillMax = (row: InventoryRow) => {
    setPickedQty((prev) => ({ ...prev, [row.id]: String(row.quantity) }));
  };

  const totalPicked = useMemo(() => {
    return inventory.reduce((sum, row) => {
      const v = parseFloat(pickedQty[row.id] ?? '');
      return sum + (Number.isFinite(v) && v > 0 ? v : 0);
    }, 0);
  }, [inventory, pickedQty]);

  const linesCount = useMemo(() => {
    return inventory.reduce((c, row) => {
      const v = parseFloat(pickedQty[row.id] ?? '');
      return c + (Number.isFinite(v) && v > 0 ? 1 : 0);
    }, 0);
  }, [inventory, pickedQty]);

  const onSubmit = async () => {
    if (!producerId) {
      toast.error(t('orders.podelbaDialog.errors.pickProducer') as string);
      return;
    }
    const lines = inventory
      .map((row) => {
        const v = parseFloat(pickedQty[row.id] ?? '');
        return Number.isFinite(v) && v > 0
          ? { sourceBalanceId: row.id, quantity: v, available: row.quantity }
          : null;
      })
      .filter((x): x is { sourceBalanceId: string; quantity: number; available: number } => !!x);

    if (lines.length === 0) {
      toast.error(t('orders.podelbaDialog.errors.pickAtLeastOne') as string);
      return;
    }
    const overAllocated = lines.find((l) => l.quantity > l.available);
    if (overAllocated) {
      toast.error(t('orders.podelbaDialog.errors.overAllocated') as string);
      return;
    }

    try {
      setSubmitting(true);
      const resp = await wmsApi.podelbaToProducer({
        producerId,
        clientOrderId: order.id,
        reason: reason || null,
        lines: lines.map((l) => ({ sourceBalanceId: l.sourceBalanceId, quantity: l.quantity })),
      });
      const env = resp.data;
      if (!env?.isSuccess) {
        toast.error(env?.errorMessage || (t('orders.podelbaDialog.errors.failed') as string));
        return;
      }
      toast.success(
        t('orders.podelbaDialog.created', { count: lines.length, qty: totalPicked.toFixed(2) }) as string,
      );
      qc.invalidateQueries({ queryKey: clientOrderKeys.all });
      qc.invalidateQueries({ queryKey: ['clientOrders', 'materials', order.id] });
      qc.invalidateQueries({ queryKey: ['inventory'] });
      onCreated();
    } catch (err: any) {
      const msg =
        err?.response?.data?.errorMessage ||
        err?.response?.data?.message ||
        err?.message ||
        (t('orders.podelbaDialog.errors.failed') as string);
      toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={t('orders.podelbaDialog.title') as string}
      submitText={t('orders.podelbaDialog.submit') as string}
      cancelText={t('common.cancel') as string}
      onSubmit={onSubmit}
      isSubmitting={submitting}
      maxWidth="lg"
    >
      <Box>
        <Box sx={{ p: 1.5, mb: 2, bgcolor: 'background.default', borderRadius: 1 }}>
          <Typography variant="caption" color="text.secondary">
            {t('orders.podelbaDialog.hint')}: <strong>{order.orderNumber}</strong> ·{' '}
            {order.customerPartnerName ?? '—'}
          </Typography>
        </Box>

        {suggestion && (
          <Paper variant="outlined" sx={{ p: 1.5, mb: 2, bgcolor: 'info.lighter' }}>
            <Stack direction="row" alignItems="center" spacing={1.5}>
              <LightbulbIcon color="info" />
              <Box flex={1}>
                <Typography variant="body2">
                  <strong>{t('orders.podelbaDialog.suggestion.title')}:</strong> {suggestion.name}{' '}
                  <Typography component="span" variant="caption" color="text.secondary">
                    ({suggestion.code})
                  </Typography>
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {suggestion.reason === 'history.last90Days'
                    ? t('orders.podelbaDialog.suggestion.history', {
                        count: suggestion.recentAssignmentCount ?? 0,
                      })
                    : t('orders.podelbaDialog.suggestion.fallback')}
                </Typography>
              </Box>
              <Button
                size="small"
                variant="outlined"
                onClick={acceptSuggestion}
                disabled={producerId === suggestion.producerId}
              >
                {producerId === suggestion.producerId
                  ? t('orders.podelbaDialog.suggestion.accepted')
                  : t('orders.podelbaDialog.suggestion.accept')}
              </Button>
            </Stack>
          </Paper>
        )}

        <Grid container spacing={2} sx={{ mb: 2 }}>
          <Grid item xs={12} sm={6}>
            <TextField
              select
              fullWidth
              size="small"
              label={t('orders.podelbaDialog.fields.warehouse')}
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
              label={t('orders.podelbaDialog.fields.producer')}
              value={producerId}
              onChange={(e) => setProducerId(e.target.value)}
              SelectProps={{ native: true }}
              InputLabelProps={{ shrink: true }}
            >
              <option value="" />
              {producerOptions.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </TextField>
          </Grid>
          <Grid item xs={12}>
            <TextField
              fullWidth
              size="small"
              label={t('orders.podelbaDialog.fields.reason')}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder={t('orders.podelbaDialog.fields.reasonPlaceholder') as string}
            />
          </Grid>
        </Grid>

        <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 1 }}>
          <Typography variant="overline" color="text.secondary">
            {t('orders.podelbaDialog.section.materials')}
          </Typography>
          <Stack direction="row" alignItems="center" spacing={1}>
            <label style={{ fontSize: 13, display: 'inline-flex', alignItems: 'center' }}>
              <Checkbox
                size="small"
                checked={showAllMaterials}
                onChange={(e) => setShowAllMaterials(e.target.checked)}
              />
              {t('orders.podelbaDialog.showAll')}
            </label>
            <Tooltip title={t('orders.podelbaDialog.refresh') as string}>
              <span>
                <IconButton
                  size="small"
                  onClick={() => setWarehouseId((w) => w)}
                  disabled={loadingInventory}
                >
                  <RefreshIcon fontSize="small" />
                </IconButton>
              </span>
            </Tooltip>
          </Stack>
        </Stack>

        {loadingInventory && <LinearProgress sx={{ mb: 1 }} />}

        {!loadingInventory && !showAllMaterials && inventory.length === 0 && (
          <Alert severity="info" sx={{ mb: 2 }}>
            {t('orders.podelbaDialog.noScopedInventory')}
          </Alert>
        )}

        {!loadingInventory && showAllMaterials && inventory.length === 0 && (
          <Alert severity="warning" sx={{ mb: 2 }}>
            {t('orders.podelbaDialog.noInventory')}
          </Alert>
        )}

        {inventory.length > 0 && (
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: '1.5fr 1.1fr 1fr 0.7fr 0.7fr 0.8fr',
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
              t('orders.podelbaDialog.cols.item'),
              t('orders.podelbaDialog.cols.batch'),
              t('orders.podelbaDialog.cols.mrn'),
              t('orders.podelbaDialog.cols.location'),
              t('orders.podelbaDialog.cols.available'),
              t('orders.podelbaDialog.cols.qtyToAssign'),
            ].map((h, i) => (
              <Box
                key={i}
                sx={{
                  fontWeight: 600,
                  p: 1,
                  borderBottom: 1,
                  borderColor: 'divider',
                  bgcolor: 'background.default',
                  textAlign: i >= 4 ? 'right' : 'left',
                }}
              >
                {h}
              </Box>
            ))}
            {inventory.map((row) => {
              const pickedRaw = pickedQty[row.id] ?? '';
              const picked = parseFloat(pickedRaw);
              const overRange =
                Number.isFinite(picked) && picked > row.quantity ? true : false;
              return (
                <React.Fragment key={row.id}>
                  <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                    {row.item ? (row.item.name ? `${row.item.code} — ${row.item.name}` : row.item.code) : '—'}
                  </Box>
                  <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace' }}>
                    {row.batchNumber ?? '—'}
                  </Box>
                  <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace', fontSize: 11 }}>
                    {row.mrn ?? '—'}
                  </Box>
                  <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                    {row.location?.code ?? '—'}
                  </Box>
                  <Box
                    sx={{
                      p: 1,
                      borderBottom: 1,
                      borderColor: 'divider',
                      textAlign: 'right',
                      fontWeight: 500,
                    }}
                  >
                    {row.quantity.toFixed(2)} {row.uoM?.code ?? ''}
                  </Box>
                  <Box
                    sx={{
                      p: 0.5,
                      borderBottom: 1,
                      borderColor: 'divider',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 0.5,
                    }}
                  >
                    <TextField
                      size="small"
                      type="number"
                      fullWidth
                      value={pickedRaw}
                      onChange={(e) => setQty(row.id, e.target.value)}
                      error={overRange}
                      helperText={overRange ? t('orders.podelbaDialog.cols.overAvailable') : undefined}
                      inputProps={{
                        min: 0,
                        max: row.quantity,
                        step: '0.01',
                        style: { textAlign: 'right' },
                      }}
                    />
                    <Tooltip title={t('orders.podelbaDialog.cols.fillMax') as string}>
                      <Button size="small" variant="text" onClick={() => fillMax(row)}>
                        max
                      </Button>
                    </Tooltip>
                  </Box>
                </React.Fragment>
              );
            })}
          </Box>
        )}

        <Stack direction="row" justifyContent="flex-end" spacing={2}>
          <Chip
            label={
              t('orders.podelbaDialog.summary', {
                lines: linesCount,
                qty: totalPicked.toFixed(2),
              }) as string
            }
            color={linesCount > 0 && producerId ? 'primary' : 'default'}
            variant="outlined"
          />
        </Stack>
      </Box>
    </FormDialog>
  );
};

export default PodelbaDialog;
