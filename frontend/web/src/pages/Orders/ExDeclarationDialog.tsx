import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Box,
  Checkbox,
  Chip,
  Grid,
  LinearProgress,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { toast } from 'react-toastify';
import { useQueryClient } from '@tanstack/react-query';
import FormDialog from '../../components/common/FormDialog';
import { clientOrdersApi, customsApi, masterDataApi, wmsApi } from '../../services/api';
import { ClientOrderDto, clientOrderKeys } from '../../hooks/queries/useClientOrders';

interface Props {
  open: boolean;
  order: ClientOrderDto;
  onClose: () => void;
  /**
   * Called after a successful create. The optional second arg carries the
   * Shipment id so callers can chain into the §E8.5 CommercialInvoice flow
   * (suggest-from-shipment) without an extra round-trip.
   */
  onCreated: (chain?: { shipmentId: string }) => void;
}

interface FgRow {
  balanceId: string;
  itemId: string;
  itemCode: string;
  itemName?: string | null;
  batchNumber?: string | null;
  mrn?: string | null;
  quantity: number;
  qualityStatus: number;
  uoMId: string;
  uoMCode?: string | null;
  locationId: string;
  locationCode?: string | null;
  warehouseCode?: string | null;
}

interface ProcedureRow {
  id: string;
  code: string;
  description?: string;
}

interface PartnerRow { id: string; code: string; name: string; partnerType?: number; }

interface DeclarationRow {
  id: string;
  declarationNumber: string;
  declarationType: string;
  mrn?: string | null;
}

const todayIso = () => new Date().toISOString().slice(0, 10);

/**
 * Phase 17 §E8 — Hub action "Креирај извозна декларација".
 *
 * Compact 1-step replacement for the BLUEPRINT's 4-step wizard: picks FGs from
 * the ClientOrder + ships them with a chained EX declaration via the existing
 * `BulkShipmentFromFGCommand` (which is atomic by design). Pre-flight duty +
 * AI helper hints land in §E10.
 *
 * Submit fires `POST /api/WMS/shipments/bulk-from-fg` with
 * `createExportDeclaration=true` + `clientOrderId` so both the Shipment and
 * the chained EX get stamped with the parent order.
 */
const ExDeclarationDialog: React.FC<Props> = ({ open, order, onClose, onCreated }) => {
  const { t } = useTranslation();
  const qc = useQueryClient();

  const [fgs, setFgs] = useState<FgRow[]>([]);
  const [imDeclarations, setImDeclarations] = useState<DeclarationRow[]>([]);
  const [procedures, setProcedures] = useState<ProcedureRow[]>([]);
  const [partners, setPartners] = useState<PartnerRow[]>([]);

  const [loadingFgs, setLoadingFgs] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const [selectedBalanceIds, setSelectedBalanceIds] = useState<Set<string>>(new Set());
  const [procedureId, setProcedureId] = useState('');
  const [partnerId, setPartnerId] = useState('');
  const [countryDestination, setCountryDestination] = useState('');
  const [incoterm, setIncoterm] = useState('FCA');
  const [shipmentDate, setShipmentDate] = useState(todayIso());
  const [reference, setReference] = useState('');

  useEffect(() => {
    if (!open) return;
    setSelectedBalanceIds(new Set());
    setProcedureId('');
    setPartnerId(order.customerPartnerId ?? '');
    setCountryDestination('');
    setIncoterm('FCA');
    setShipmentDate(todayIso());
    setReference(order.orderNumber);

    setLoadingFgs(true);
    clientOrdersApi
      .getAvailableFinishedGoods(order.id)
      .then((r) => setFgs((r.data ?? []) as FgRow[]))
      .catch(() => setFgs([]))
      .finally(() => setLoadingFgs(false));

    customsApi
      .getDeclarations({ clientOrderId: order.id })
      .then((r) => {
        const rows = (r.data ?? []) as DeclarationRow[];
        setImDeclarations(rows.filter((d) => d.declarationType === 'IM'));
      })
      .catch(() => setImDeclarations([]));

    customsApi
      .getProcedures()
      .then((r) => {
        const rows = (r.data?.data ?? r.data ?? []) as ProcedureRow[];
        // Show only EX-style procedures; backend doesn't carry a direction
        // flag, so we filter by code prefix (3151 / 1000 / 3100 etc).
        setProcedures(rows.filter((p) => /^3|^1/.test(p.code)));
      })
      .catch(() => setProcedures([]));

    masterDataApi
      .getPartners()
      .then((r) => setPartners((r.data?.data ?? r.data ?? []) as PartnerRow[]))
      .catch(() => setPartners([]));
  }, [open, order]);

  const toggleBalance = (id: string) => {
    setSelectedBalanceIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const distinctMrns = useMemo(() => {
    const set = new Set<string>();
    fgs.forEach((r) => {
      if (selectedBalanceIds.has(r.balanceId) && r.mrn) set.add(r.mrn);
    });
    return Array.from(set);
  }, [fgs, selectedBalanceIds]);

  const totalQty = useMemo(
    () => fgs.filter((r) => selectedBalanceIds.has(r.balanceId)).reduce((s, r) => s + r.quantity, 0),
    [fgs, selectedBalanceIds],
  );

  const onSubmit = async () => {
    if (!procedureId) {
      toast.error(t('orders.exDialog.errors.pickProcedure') as string);
      return;
    }
    if (selectedBalanceIds.size === 0) {
      toast.error(t('orders.exDialog.errors.pickAtLeastOne') as string);
      return;
    }
    if (distinctMrns.length !== 1) {
      toast.error(
        t('orders.exDialog.errors.singleMrnRequired', { count: distinctMrns.length }) as string,
      );
      return;
    }

    // Server expects MRN + matching balances. The bulk handler resolves by
    // MRN filter, so submitting with the single MRN re-fetches the same set.
    const mrn = distinctMrns[0];
    try {
      setSubmitting(true);
      const resp = await wmsApi.bulkShipmentFromFG({
        mrn,
        partnerId: partnerId || null,
        customsProcedureId: procedureId,
        shipmentDate,
        reference: reference || null,
        createExportDeclaration: true,
        clientOrderId: order.id,
      });
      const env = resp.data;
      if (!env?.isSuccess) {
        toast.error(env?.errorMessage || (t('orders.exDialog.errors.failed') as string));
        return;
      }
      toast.success(
        t('orders.exDialog.created', {
          shipment: env.data?.shipmentNumber,
          qty: totalQty.toFixed(2),
        }) as string,
      );
      qc.invalidateQueries({ queryKey: clientOrderKeys.all });
      qc.invalidateQueries({ queryKey: ['clientOrders', 'declarations', order.id] });
      qc.invalidateQueries({ queryKey: ['clientOrders', 'shipments', order.id] });
      qc.invalidateQueries({ queryKey: ['clientOrders', 'materials', order.id] });
      qc.invalidateQueries({ queryKey: ['clientOrders', 'commercialInvoices', order.id] });
      const shipmentId = env.data?.shipmentId as string | undefined;
      onCreated(shipmentId ? { shipmentId } : undefined);
    } catch (err: any) {
      toast.error(
        err?.response?.data?.errorMessage ||
          err?.response?.data?.message ||
          err?.message ||
          (t('orders.exDialog.errors.failed') as string),
      );
    } finally {
      setSubmitting(false);
    }
  };

  const procedureOptions = procedures.map((p) => ({
    value: p.id,
    label: `${p.code}${p.description ? ` — ${p.description}` : ''}`,
  }));
  const partnerOptions = partners.map((p) => ({
    value: p.id,
    label: `${p.code} — ${p.name}`,
  }));

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={t('orders.exDialog.title') as string}
      submitText={t('orders.exDialog.submit') as string}
      cancelText={t('common.cancel') as string}
      onSubmit={onSubmit}
      isSubmitting={submitting}
      disableSubmit={!procedureId || selectedBalanceIds.size === 0 || distinctMrns.length !== 1}
      maxWidth="lg"
    >
      <Box>
        <Box sx={{ p: 1.5, mb: 2, bgcolor: 'background.default', borderRadius: 1 }}>
          <Typography variant="caption" color="text.secondary">
            {t('orders.exDialog.hint')}: <strong>{order.orderNumber}</strong> ·{' '}
            {order.customerPartnerName ?? '—'}
          </Typography>
        </Box>

        {/* Step 1 — pick FGs */}
        <Typography variant="overline" color="text.secondary">
          {t('orders.exDialog.section.fgs')}
        </Typography>
        {loadingFgs && <LinearProgress sx={{ mb: 1 }} />}
        {!loadingFgs && fgs.length === 0 && (
          <Alert severity="info" sx={{ mb: 2 }}>
            {t('orders.exDialog.noFgs')}
          </Alert>
        )}
        {fgs.length > 0 && (
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: '40px 1.4fr 1fr 1.2fr 0.7fr 0.8fr',
              fontSize: 13,
              border: 1,
              borderColor: 'divider',
              borderRadius: 1,
              overflow: 'hidden',
              mb: 2,
            }}
          >
            {['', t('orders.exDialog.cols.item'), t('orders.exDialog.cols.batch'), t('orders.exDialog.cols.mrn'), t('orders.exDialog.cols.location'), t('orders.exDialog.cols.available')].map(
              (h, i) => (
                <Box
                  key={i}
                  sx={{
                    fontWeight: 600,
                    p: 1,
                    borderBottom: 1,
                    borderColor: 'divider',
                    bgcolor: 'background.default',
                    textAlign: i === 5 ? 'right' : 'left',
                  }}
                >
                  {h}
                </Box>
              ),
            )}
            {fgs.map((r) => (
              <React.Fragment key={r.balanceId}>
                <Box sx={{ p: 0.5, borderBottom: 1, borderColor: 'divider', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                  <Checkbox
                    size="small"
                    checked={selectedBalanceIds.has(r.balanceId)}
                    onChange={() => toggleBalance(r.balanceId)}
                  />
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                  {r.itemName ? `${r.itemCode} — ${r.itemName}` : r.itemCode}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace' }}>
                  {r.batchNumber ?? '—'}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace', fontSize: 11 }}>
                  {r.mrn ?? '—'}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                  {r.warehouseCode ? `${r.warehouseCode} / ${r.locationCode}` : (r.locationCode ?? '—')}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right', fontWeight: 500 }}>
                  {r.quantity.toFixed(2)} {r.uoMCode ?? ''}
                </Box>
              </React.Fragment>
            ))}
          </Box>
        )}

        {distinctMrns.length > 1 && (
          <Alert severity="warning" sx={{ mb: 2 }}>
            {t('orders.exDialog.errors.singleMrnRequired', { count: distinctMrns.length })}
          </Alert>
        )}

        {/* Step 2 — shipment + customs metadata */}
        <Typography variant="overline" color="text.secondary">
          {t('orders.exDialog.section.shipment')}
        </Typography>
        <Grid container spacing={2} sx={{ mb: 2 }}>
          <Grid item xs={12} sm={6}>
            <TextField
              select
              fullWidth
              size="small"
              label={t('orders.exDialog.fields.procedure')}
              value={procedureId}
              onChange={(e) => setProcedureId(e.target.value)}
              SelectProps={{ native: true }}
              InputLabelProps={{ shrink: true }}
            >
              <option value="" />
              {procedureOptions.map((o) => (
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
              label={t('orders.exDialog.fields.consignee')}
              value={partnerId}
              onChange={(e) => setPartnerId(e.target.value)}
              SelectProps={{ native: true }}
              InputLabelProps={{ shrink: true }}
            >
              <option value="" />
              {partnerOptions.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </TextField>
          </Grid>
          <Grid item xs={6} sm={3}>
            <TextField
              fullWidth size="small"
              label={t('orders.exDialog.fields.countryDestination')}
              value={countryDestination}
              onChange={(e) => setCountryDestination(e.target.value.toUpperCase())}
              inputProps={{ maxLength: 2 }}
              placeholder="DE"
            />
          </Grid>
          <Grid item xs={6} sm={3}>
            <TextField
              select
              fullWidth size="small"
              label={t('orders.exDialog.fields.incoterm')}
              value={incoterm}
              onChange={(e) => setIncoterm(e.target.value)}
            >
              {['FCA', 'FOB', 'CIF', 'EXW', 'DAP', 'DDP'].map((i) => (
                <MenuItem key={i} value={i}>{i}</MenuItem>
              ))}
            </TextField>
          </Grid>
          <Grid item xs={6} sm={3}>
            <TextField
              fullWidth size="small" type="date"
              label={t('orders.exDialog.fields.shipmentDate')}
              value={shipmentDate}
              onChange={(e) => setShipmentDate(e.target.value)}
              InputLabelProps={{ shrink: true }}
            />
          </Grid>
          <Grid item xs={6} sm={3}>
            <TextField
              fullWidth size="small"
              label={t('orders.exDialog.fields.reference')}
              value={reference}
              onChange={(e) => setReference(e.target.value)}
            />
          </Grid>
        </Grid>

        {/* Auto-suggested IM declarations */}
        {imDeclarations.length > 0 && (
          <Box sx={{ p: 1.5, mb: 2, bgcolor: 'info.lighter', borderRadius: 1 }}>
            <Typography variant="caption" color="text.secondary">
              {t('orders.exDialog.imSuggestionTitle')}:
            </Typography>
            <Stack direction="row" spacing={1} sx={{ mt: 0.5, flexWrap: 'wrap' }}>
              {imDeclarations.map((d) => (
                <Chip
                  key={d.id}
                  size="small"
                  label={`${d.declarationNumber} (${d.mrn ?? '—'})`}
                  variant={distinctMrns.includes(d.mrn ?? '') ? 'filled' : 'outlined'}
                  color={distinctMrns.includes(d.mrn ?? '') ? 'primary' : 'default'}
                />
              ))}
            </Stack>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
              {t('orders.exDialog.imSuggestionHint')}
            </Typography>
          </Box>
        )}

        {/* Summary */}
        <Stack direction="row" justifyContent="flex-end" spacing={2}>
          <Chip
            size="small"
            label={
              t('orders.exDialog.summary', {
                lines: selectedBalanceIds.size,
                qty: totalQty.toFixed(2),
                mrn: distinctMrns[0] ?? '—',
              }) as string
            }
            color={selectedBalanceIds.size > 0 && distinctMrns.length === 1 ? 'primary' : 'default'}
            variant="outlined"
          />
        </Stack>
      </Box>
    </FormDialog>
  );
};

export default ExDeclarationDialog;
