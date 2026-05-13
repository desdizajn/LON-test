import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Box,
  Button,
  Chip,
  LinearProgress,
  Stack,
  Typography,
} from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import CancelIcon from '@mui/icons-material/Cancel';
import { toast } from 'react-toastify';
import { useQueryClient } from '@tanstack/react-query';
import FormDialog from '../../components/common/FormDialog';
import { clientOrdersApi, wmsApi } from '../../services/api';
import { ClientOrderDto, clientOrderKeys } from '../../hooks/queries/useClientOrders';

interface Props {
  open: boolean;
  order: ClientOrderDto;
  onClose: () => void;
  onCreated: () => void;
}

interface QcRow {
  balanceId: string;
  itemId: string;
  itemCode: string;
  itemName?: string | null;
  batchNumber?: string | null;
  mrn?: string | null;
  quantity: number;
  qualityStatus: number;
  uoMCode?: string | null;
  locationCode?: string | null;
  warehouseCode?: string | null;
}

const QUALITY_LABEL_KEY: Record<number, string> = {
  0: 'qualityStatus.ok',
  1: 'qualityStatus.ok',
  2: 'qualityStatus.blocked',
  3: 'qualityStatus.quarantine',
};

/**
 * Phase 17 §E8 — Hub action "QC + Пакување".
 *
 * Lists FG balances for this ClientOrder that aren't yet OK (`QualityStatus !=
 * 1`). Each row gets two quick-actions: „Pass QC" sets OK (status=1), „Reject"
 * prompts for a reason and sets Blocked (status=2). Rework PO / waste
 * declaration spawn is BLUEPRINT §5.9.2 territory and ships when the full QC
 * inspection entity lands; for v1 a Rejected balance is parked Blocked + audit
 * trail (via the QC InventoryMovement note) for follow-up.
 */
const QcDialog: React.FC<Props> = ({ open, order, onClose, onCreated }) => {
  const { t } = useTranslation();
  const qc = useQueryClient();

  const [rows, setRows] = useState<QcRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    try {
      const r = await clientOrdersApi.getAvailableFinishedGoods(order.id);
      const all = (r.data ?? []) as QcRow[];
      // Show only non-OK rows (Quarantine + Blocked). OK rows are shippable
      // already and don't belong in the QC bucket.
      setRows(all.filter((b) => b.qualityStatus !== 1));
    } catch {
      setRows([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (open) void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, order.id]);

  const handlePass = async (row: QcRow) => {
    setBusyId(row.balanceId);
    try {
      await wmsApi.updateQualityStatus({
        balanceId: row.balanceId,
        newQualityStatus: 1, // OK
        reason: null,
      });
      toast.success(t('orders.qcDialog.passed', { batch: row.batchNumber ?? '—' }) as string);
      qc.invalidateQueries({ queryKey: clientOrderKeys.all });
      qc.invalidateQueries({ queryKey: ['clientOrders', 'materials', order.id] });
      await load();
    } catch (err: any) {
      toast.error(err?.response?.data?.errorMessage ?? (t('orders.qcDialog.failed') as string));
    } finally {
      setBusyId(null);
    }
  };

  const handleReject = async (row: QcRow) => {
    const reason = window.prompt(t('orders.qcDialog.rejectReasonPrompt') as string) ?? '';
    if (!reason.trim()) return;
    setBusyId(row.balanceId);
    try {
      await wmsApi.updateQualityStatus({
        balanceId: row.balanceId,
        newQualityStatus: 2, // Blocked
        reason: reason.trim(),
      });
      toast.success(t('orders.qcDialog.rejected', { batch: row.batchNumber ?? '—' }) as string);
      qc.invalidateQueries({ queryKey: clientOrderKeys.all });
      qc.invalidateQueries({ queryKey: ['clientOrders', 'materials', order.id] });
      await load();
    } catch (err: any) {
      toast.error(err?.response?.data?.errorMessage ?? (t('orders.qcDialog.failed') as string));
    } finally {
      setBusyId(null);
    }
  };

  const summary = useMemo(() => {
    return rows.reduce(
      (acc, r) => {
        acc.totalQty += r.quantity;
        if (r.qualityStatus === 3) acc.quarantine += 1;
        if (r.qualityStatus === 2) acc.blocked += 1;
        return acc;
      },
      { totalQty: 0, quarantine: 0, blocked: 0 },
    );
  }, [rows]);

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={t('orders.qcDialog.title') as string}
      submitText={t('common.close') as string}
      cancelText={t('common.close') as string}
      onSubmit={onClose}
      isSubmitting={false}
      maxWidth="lg"
    >
      <Box>
        <Box sx={{ p: 1.5, mb: 2, bgcolor: 'background.default', borderRadius: 1 }}>
          <Typography variant="caption" color="text.secondary">
            {t('orders.qcDialog.hint')}: <strong>{order.orderNumber}</strong>
          </Typography>
        </Box>

        <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
          <Chip size="small" label={t('orders.qcDialog.summary.quarantine', { count: summary.quarantine })} color="warning" variant="outlined" />
          <Chip size="small" label={t('orders.qcDialog.summary.blocked', { count: summary.blocked })} color="error" variant="outlined" />
          <Chip size="small" label={t('orders.qcDialog.summary.totalQty', { qty: summary.totalQty.toFixed(2) })} variant="outlined" />
        </Stack>

        {loading && <LinearProgress sx={{ mb: 1 }} />}

        {!loading && rows.length === 0 && (
          <Alert severity="success" sx={{ mb: 2 }}>
            {t('orders.qcDialog.allClean')}
          </Alert>
        )}

        {rows.length > 0 && (
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: '1.4fr 1fr 1.2fr 0.8fr 0.8fr 0.7fr 1.4fr',
              fontSize: 13,
              border: 1,
              borderColor: 'divider',
              borderRadius: 1,
              overflow: 'hidden',
            }}
          >
            {[
              t('orders.qcDialog.cols.item'),
              t('orders.qcDialog.cols.batch'),
              t('orders.qcDialog.cols.mrn'),
              t('orders.qcDialog.cols.location'),
              t('orders.qcDialog.cols.quantity'),
              t('orders.qcDialog.cols.status'),
              t('orders.qcDialog.cols.actions'),
            ].map((h, i) => (
              <Box
                key={i}
                sx={{
                  fontWeight: 600,
                  p: 1,
                  borderBottom: 1,
                  borderColor: 'divider',
                  bgcolor: 'background.default',
                  textAlign: i === 4 ? 'right' : 'left',
                }}
              >
                {h}
              </Box>
            ))}
            {rows.map((r) => (
              <React.Fragment key={r.balanceId}>
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
                  {r.warehouseCode ? `${r.warehouseCode}/${r.locationCode}` : (r.locationCode ?? '—')}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
                  {r.quantity.toFixed(2)} {r.uoMCode ?? ''}
                </Box>
                <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
                  <Chip
                    size="small"
                    label={t(QUALITY_LABEL_KEY[r.qualityStatus] ?? 'qualityStatus.ok')}
                    color={r.qualityStatus === 3 ? 'warning' : r.qualityStatus === 2 ? 'error' : 'default'}
                  />
                </Box>
                <Box sx={{ p: 0.5, borderBottom: 1, borderColor: 'divider', display: 'flex', gap: 0.5 }}>
                  <Button
                    size="small"
                    variant="contained"
                    color="success"
                    startIcon={<CheckCircleIcon />}
                    disabled={busyId === r.balanceId || r.qualityStatus === 1}
                    onClick={() => handlePass(r)}
                  >
                    {t('orders.qcDialog.pass')}
                  </Button>
                  <Button
                    size="small"
                    variant="outlined"
                    color="error"
                    startIcon={<CancelIcon />}
                    disabled={busyId === r.balanceId || r.qualityStatus === 2}
                    onClick={() => handleReject(r)}
                  >
                    {t('orders.qcDialog.reject')}
                  </Button>
                </Box>
              </React.Fragment>
            ))}
          </Box>
        )}
      </Box>
    </FormDialog>
  );
};

export default QcDialog;
