import React, { useEffect, useState } from 'react';
import { useParams, useNavigate, Link as RouterLink, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useSetAiContext } from '../../contexts/AiHelperContext';
import {
  Alert,
  Box,
  Button,
  Chip,
  Divider,
  Grid,
  LinearProgress,
  Link as MuiLink,
  Paper,
  Stack,
  Tab,
  Tabs,
  Tooltip,
  Typography,
} from '@mui/material';
import LocalShippingIcon from '@mui/icons-material/LocalShipping';
import InventoryIcon from '@mui/icons-material/Inventory';
import FactoryIcon from '@mui/icons-material/Factory';
import HandymanIcon from '@mui/icons-material/Handyman';
import CallSplitIcon from '@mui/icons-material/CallSplit';
import LocalAtmIcon from '@mui/icons-material/LocalAtm';
import FlightTakeoffIcon from '@mui/icons-material/FlightTakeoff';
import HistoryIcon from '@mui/icons-material/History';
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome';
import VerifiedIcon from '@mui/icons-material/Verified';
import { useQuery } from '@tanstack/react-query';
import {
  ClientOrderStatus,
  useClientOrder,
} from '../../hooks/queries/useClientOrders';
import { customsApi, productionApi, wmsApi } from '../../services/api';
import { formatDate } from '../../utils/format';
import ImDeclarationDialog from './ImDeclarationDialog';
import ReceiveDialog from './ReceiveDialog';
import BomDialog from './BomDialog';
import PodelbaDialog from './PodelbaDialog';
import IssueMaterialDialog from './IssueMaterialDialog';
import ProductionReceiptDialog from './ProductionReceiptDialog';
import ExDeclarationDialog from './ExDeclarationDialog';
import QcDialog from './QcDialog';
import CommercialInvoiceDialog from './CommercialInvoiceDialog';
import ReceiptIcon from '@mui/icons-material/ReceiptLong';
import { commercialInvoicesApi } from '../../services/api';

const STATUS_COLOR: Record<ClientOrderStatus, 'default' | 'info' | 'warning' | 'success' | 'error'> = {
  0: 'default',
  1: 'info',
  2: 'warning',
  3: 'success',
  4: 'success',
  99: 'error',
};

interface ActionDef {
  key: string;
  labelKey: string;
  icon: React.ReactNode;
  /** Reference back to the AGENT-PROMPTS task that wires this action. */
  wiresInTask: 'E3' | 'E4' | 'E5' | 'E6' | 'E7' | 'E8' | 'E8.5' | 'E9' | 'E10' | 'E13';
  /** When false, the button is disabled with the "Coming in §E…" tooltip. */
  enabled?: boolean;
}

const ACTIONS: ActionDef[] = [
  // Phase 17 §E5 — adds ClientOrderFinishedGood + optional ProductionOrder.
  { key: 'bom', labelKey: 'orders.actions.bom', icon: <FactoryIcon />, wiresInTask: 'E5', enabled: true },
  // Phase 17 §E3 — IM action launches an inline dialog (handled below).
  { key: 'imDeclaration', labelKey: 'orders.actions.imDeclaration', icon: <InventoryIcon />, wiresInTask: 'E3', enabled: true },
  // Phase 17 §E4 — Receive launches BulkReceiptFromDeclaration dialog.
  { key: 'receive', labelKey: 'orders.actions.receive', icon: <LocalShippingIcon />, wiresInTask: 'E4', enabled: true },
  // Phase 17 §E6 — Podelba launches the multi-balance, single-producer dialog.
  { key: 'podelba', labelKey: 'orders.actions.podelba', icon: <CallSplitIcon />, wiresInTask: 'E6', enabled: true },
  // Phase 17 §E7 — Issue all remaining materials for a PO (FEFO).
  { key: 'issueMaterial', labelKey: 'orders.actions.issueMaterial', icon: <HandymanIcon />, wiresInTask: 'E7', enabled: true },
  // Phase 17 §E7 — record finished-good production receipt against a PO.
  { key: 'productionReceipt', labelKey: 'orders.actions.productionReceipt', icon: <FactoryIcon />, wiresInTask: 'E7', enabled: true },
  // Phase 17 §E8 — atomic Shipment + EX customs declaration; stamps both
  // with ClientOrderId so hub Declarations + Shipments tabs filter cleanly.
  { key: 'exDeclaration', labelKey: 'orders.actions.exDeclaration', icon: <FlightTakeoffIcon />, wiresInTask: 'E8', enabled: true },
  // Phase 17 §E8 — quick-action QC dialog: pass non-OK FG balances to OK or
  // park as Blocked with a reason. Rework PO / waste declaration spawn lands
  // with BLUEPRINT §5.9.2 (post-v1 inspection entity).
  { key: 'qcPackaging', labelKey: 'orders.actions.qcPackaging', icon: <VerifiedIcon />, wiresInTask: 'E8', enabled: true },
  // Phase 17 §E8.5 — chain action after EX: draft a CommercialInvoice from
  // the most-recent Shipment on the ClientOrder. Opens immediately if a
  // shipment already exists; from the EX dialog onCreated callback if not.
  { key: 'commercialInvoice', labelKey: 'orders.actions.commercialInvoice', icon: <ReceiptIcon />, wiresInTask: 'E8.5', enabled: true },
  // Phase 17 §E9 — Razdolzuvanje view: IM vs EX/Waste/Return reconciliation
  // + per-line flag + GuaranteeBalanceSnapshot + auto-Close.
  { key: 'razdolzuvanje', labelKey: 'orders.actions.razdolzuvanje', icon: <LocalAtmIcon />, wiresInTask: 'E9', enabled: true },
  { key: 'audit', labelKey: 'orders.actions.audit', icon: <HistoryIcon />, wiresInTask: 'E13' },
  { key: 'ai', labelKey: 'orders.actions.ai', icon: <AutoAwesomeIcon />, wiresInTask: 'E10' },
];

interface TimelineEvent {
  id: string;
  labelKey: string;
  /** ISO datetime — null means not-yet-happened (rendered as pending). */
  at: string | null;
}

const OrderHub: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  // Phase 17 §E10 — declare this page's entity so the AI helper drawer's
  // recommendations tab can light up with ClientOrder-scoped nudges.
  useSetAiContext('ClientOrder', id ?? null);

  const { data: order, isLoading, error } = useClientOrder(id);
  const [tab, setTab] = useState(0);
  const [imOpen, setImOpen] = useState(false);
  const [receiveOpen, setReceiveOpen] = useState(false);
  const [bomOpen, setBomOpen] = useState(false);
  const [podelbaOpen, setPodelbaOpen] = useState(false);
  const [issueOpen, setIssueOpen] = useState(false);
  const [receiptOpen, setReceiptOpen] = useState(false);
  const [exOpen, setExOpen] = useState(false);
  const [qcOpen, setQcOpen] = useState(false);
  const [ciOpen, setCiOpen] = useState(false);
  const [ciShipmentId, setCiShipmentId] = useState<string | null>(null);

  // Phase 17 §E7 — feed the produced-progress widget from real PO data.
  // Same query key as `ProductionOrdersTab` below — react-query dedupes.
  const { data: productionOrders = [] } = useQuery({
    queryKey: ['clientOrders', 'productionOrders', id ?? ''],
    queryFn: async () => {
      const resp = await productionApi.getOrders({ clientOrderId: id });
      return (resp.data ?? []) as Array<{ orderQuantity: number; producedQuantity: number }>;
    },
    enabled: !!id,
  });

  const handleActionClick = async (actionKey: string) => {
    if (actionKey === 'imDeclaration') {
      setImOpen(true);
    } else if (actionKey === 'receive') {
      setReceiveOpen(true);
    } else if (actionKey === 'bom') {
      setBomOpen(true);
    } else if (actionKey === 'podelba') {
      setPodelbaOpen(true);
    } else if (actionKey === 'issueMaterial') {
      setIssueOpen(true);
    } else if (actionKey === 'productionReceipt') {
      setReceiptOpen(true);
    } else if (actionKey === 'exDeclaration') {
      setExOpen(true);
    } else if (actionKey === 'qcPackaging') {
      setQcOpen(true);
    } else if (actionKey === 'razdolzuvanje') {
      navigate(`/orders/${order?.id}/razdolzuvanje`);
      return;
    } else if (actionKey === 'commercialInvoice') {
      // Pick the most-recent shipment on this ClientOrder; if none, prompt
      // the user to ship first. CI must have a parent shipment.
      try {
        const resp = await wmsApi.getShipments({ clientOrderId: order?.id, pageSize: 1 });
        const list = (resp.data ?? []) as Array<{ id: string }>;
        if (!list.length) {
          // eslint-disable-next-line no-alert
          alert(t('orders.ciDialog.errors.noShipment') as string);
          return;
        }
        setCiShipmentId(list[0].id);
        setCiOpen(true);
      } catch {
        // Fall back to opening with null — dialog shows its own error.
        setCiShipmentId(null);
        setCiOpen(true);
      }
    }
    // Other action keys are still disabled in this phase.
  };

  // Phase 17 §E10 — AI helper deep-links to the hub with ?action=<key>;
  // open the matching dialog and clear the query param so a reload doesn't
  // re-trigger.
  useEffect(() => {
    const action = searchParams.get('action');
    if (!action) return;
    const key = action.startsWith('orders.actions.') ? action.split('.').pop() ?? action : action;
    handleActionClick(key);
    const next = new URLSearchParams(searchParams);
    next.delete('action');
    setSearchParams(next, { replace: true });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams]);

  if (isLoading) {
    return (
      <Box p={3}>
        <LinearProgress />
      </Box>
    );
  }

  if (error || !order) {
    return (
      <Box p={3}>
        <Alert severity="error">
          {error instanceof Error ? error.message : t('orders.hub.notFound')}
        </Alert>
        <Button sx={{ mt: 2 }} onClick={() => navigate('/orders')}>
          {t('orders.hub.backToList')}
        </Button>
      </Box>
    );
  }

  // §E11 wires real domain events. Placeholder timeline below.
  const timeline: TimelineEvent[] = [
    { id: 'created', labelKey: 'orders.hub.timeline.created', at: order.createdAt },
    { id: 'firstDeclaration', labelKey: 'orders.hub.timeline.firstDeclaration', at: null },
    { id: 'lastShipped', labelKey: 'orders.hub.timeline.lastShipped', at: null },
  ];

  const daysToShip = order.requestedShipDate
    ? Math.ceil(
        (new Date(order.requestedShipDate).getTime() - new Date().getTime()) /
          (1000 * 60 * 60 * 24),
      )
    : null;

  // Phase 17 §E7 — `producedPct` is the order-level progress: Σ producedQty /
  // Σ orderQty across all linked POs. Falls to 0 when no POs exist.
  const totalOrderQty = productionOrders.reduce((s, p) => s + (p.orderQuantity ?? 0), 0);
  const totalProducedQty = productionOrders.reduce((s, p) => s + (p.producedQuantity ?? 0), 0);
  const producedPct = totalOrderQty > 0 ? Math.min(100, Math.round((totalProducedQty / totalOrderQty) * 100)) : 0;
  // §E9 wires real guarantee numbers; placeholder until then.
  const guaranteePct = 0;

  return (
    <Box p={3}>
      {/* ────────── Header ────────── */}
      <Paper sx={{ p: 3, mb: 3 }}>
        <Stack
          direction={{ xs: 'column', md: 'row' }}
          justifyContent="space-between"
          alignItems={{ xs: 'flex-start', md: 'center' }}
          spacing={2}
        >
          <Box>
            <Stack direction="row" spacing={2} alignItems="center" mb={1}>
              <Typography variant="h4">{order.orderNumber}</Typography>
              <Chip label={order.statusName} color={STATUS_COLOR[order.status]} />
            </Stack>
            <Stack direction="row" spacing={3} flexWrap="wrap">
              <Typography variant="body2" color="text.secondary">
                <strong>{t('orders.hub.header.customer')}:</strong>{' '}
                <MuiLink
                  component={RouterLink}
                  to={`/master-data/partners/${order.customerPartnerId}`}
                >
                  {order.customerPartnerName ?? '—'}
                </MuiLink>
              </Typography>
              <Typography variant="body2" color="text.secondary">
                <strong>{t('orders.hub.header.authorization')}:</strong>{' '}
                <MuiLink
                  component={RouterLink}
                  to={`/customs/authorizations`}
                >
                  {order.lonAuthorizationNumber ?? '—'}
                </MuiLink>
              </Typography>
              {order.customerOrderReference && (
                <Typography variant="body2" color="text.secondary">
                  <strong>{t('orders.hub.header.customerRef')}:</strong>{' '}
                  {order.customerOrderReference}
                </Typography>
              )}
              <Typography variant="body2" color="text.secondary">
                <strong>{t('orders.hub.header.orderDate')}:</strong>{' '}
                {formatDate(order.orderDate)}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                <strong>{t('orders.hub.header.requestedShip')}:</strong>{' '}
                {order.requestedShipDate ? formatDate(order.requestedShipDate) : '—'}
              </Typography>
            </Stack>
            {order.notes && (
              <Typography variant="caption" color="text.secondary" sx={{ mt: 1, display: 'block' }}>
                {order.notes}
              </Typography>
            )}
          </Box>
          <Button onClick={() => navigate('/orders')} size="small">
            {t('orders.hub.backToList')}
          </Button>
        </Stack>
      </Paper>

      <Grid container spacing={3}>
        {/* ────────── Left: Timeline ────────── */}
        <Grid item xs={12} md={3}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
              {t('orders.hub.timeline.title')}
            </Typography>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 2 }}>
              {t('orders.hub.timeline.placeholderHint')}
            </Typography>
            <Stack spacing={2}>
              {timeline.map((ev) => (
                <Box key={ev.id} sx={{ display: 'flex', gap: 1.5, alignItems: 'flex-start' }}>
                  <Box
                    sx={{
                      width: 10,
                      height: 10,
                      borderRadius: '50%',
                      bgcolor: ev.at ? 'primary.main' : 'grey.400',
                      mt: 0.6,
                      flexShrink: 0,
                    }}
                  />
                  <Box>
                    <Typography variant="body2" fontWeight={ev.at ? 600 : 400}>
                      {t(ev.labelKey)}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {ev.at ? formatDate(ev.at) : t('orders.hub.timeline.pending')}
                    </Typography>
                  </Box>
                </Box>
              ))}
            </Stack>
          </Paper>
        </Grid>

        {/* ────────── Center: Widgets + Tabs ────────── */}
        <Grid item xs={12} md={6}>
          <Grid container spacing={2} sx={{ mb: 3 }}>
            <Grid item xs={12} sm={4}>
              <Paper sx={{ p: 2, textAlign: 'center' }}>
                <Typography variant="caption" color="text.secondary">
                  {t('orders.hub.widgets.produced')}
                </Typography>
                <Typography variant="h4">{producedPct}%</Typography>
                <LinearProgress
                  variant="determinate"
                  value={producedPct}
                  sx={{ mt: 1 }}
                />
              </Paper>
            </Grid>
            <Grid item xs={12} sm={4}>
              <Paper sx={{ p: 2, textAlign: 'center' }}>
                <Typography variant="caption" color="text.secondary">
                  {t('orders.hub.widgets.guarantee')}
                </Typography>
                <Typography variant="h4">{guaranteePct}%</Typography>
                <LinearProgress
                  variant="determinate"
                  value={guaranteePct}
                  color="warning"
                  sx={{ mt: 1 }}
                />
              </Paper>
            </Grid>
            <Grid item xs={12} sm={4}>
              <Paper sx={{ p: 2, textAlign: 'center' }}>
                <Typography variant="caption" color="text.secondary">
                  {t('orders.hub.widgets.daysToShip')}
                </Typography>
                <Typography
                  variant="h4"
                  color={daysToShip !== null && daysToShip < 0 ? 'error.main' : 'text.primary'}
                >
                  {daysToShip ?? '—'}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {daysToShip !== null
                    ? daysToShip < 0
                      ? t('orders.hub.widgets.overdue')
                      : t('orders.hub.widgets.daysRemaining')
                    : t('orders.hub.widgets.noShipDate')}
                </Typography>
              </Paper>
            </Grid>
          </Grid>

          <Paper sx={{ p: 2 }}>
            <Tabs value={tab} onChange={(_, v) => setTab(v)} variant="scrollable">
              <Tab label={t('orders.hub.tabs.declarations')} />
              <Tab label={t('orders.hub.tabs.productionOrders')} />
              <Tab label={t('orders.hub.tabs.shipments')} />
              <Tab label={t('orders.hub.tabs.materials')} />
              <Tab label={t('orders.hub.tabs.receipts')} />
              <Tab label={t('orders.hub.tabs.commercialInvoices')} />
            </Tabs>
            <Divider sx={{ my: 2 }} />
            <Box minHeight={200}>
              {tab === 0 && <DeclarationsTab clientOrderId={order.id} />}
              {tab === 1 && <ProductionOrdersTab clientOrderId={order.id} />}
              {tab === 2 && <ShipmentsTab clientOrderId={order.id} />}
              {tab === 3 && <MaterialsTab clientOrderId={order.id} />}
              {tab === 4 && <ReceiptsTab clientOrderId={order.id} />}
              {tab === 5 && <CommercialInvoicesTab clientOrderId={order.id} />}
            </Box>
          </Paper>
        </Grid>

        {/* ────────── Right: Action Launcher ────────── */}
        <Grid item xs={12} md={3}>
          <Paper
            sx={{
              p: 2,
              position: { md: 'sticky' },
              top: { md: 80 },
            }}
          >
            <Typography variant="h6" gutterBottom>
              {t('orders.hub.actions.title')}
            </Typography>
            <Typography
              variant="caption"
              color="text.secondary"
              sx={{ display: 'block', mb: 2 }}
            >
              {t('orders.hub.actions.hint')}
            </Typography>
            <Stack spacing={1}>
              {ACTIONS.map((action) => {
                const button = (
                  <Button
                    fullWidth
                    variant="outlined"
                    startIcon={action.icon}
                    disabled={!action.enabled}
                    onClick={() => action.enabled && handleActionClick(action.key)}
                    sx={{ justifyContent: 'flex-start', textAlign: 'left' }}
                  >
                    {t(action.labelKey)}
                  </Button>
                );
                if (action.enabled) {
                  return <React.Fragment key={action.key}>{button}</React.Fragment>;
                }
                return (
                  <Tooltip
                    key={action.key}
                    title={t('orders.hub.actions.comingInTask', { task: action.wiresInTask }) as string}
                    arrow
                    placement="left"
                  >
                    <span>{button}</span>
                  </Tooltip>
                );
              })}
            </Stack>
          </Paper>
        </Grid>
      </Grid>

      {/* Phase 17 §E3 — IM declaration creation dialog. */}
      <ImDeclarationDialog
        open={imOpen}
        order={order}
        onClose={() => setImOpen(false)}
        onCreated={() => setImOpen(false)}
      />

      {/* Phase 17 §E4 — Receive into warehouse dialog. */}
      <ReceiveDialog
        open={receiveOpen}
        order={order}
        onClose={() => setReceiveOpen(false)}
        onCreated={() => setReceiveOpen(false)}
      />

      {/* Phase 17 §E5 — Add FG + optional ProductionOrder. */}
      <BomDialog
        open={bomOpen}
        order={order}
        onClose={() => setBomOpen(false)}
        onCreated={() => setBomOpen(false)}
      />

      {/* Phase 17 §E6 — distribute materials to a sub-contractor producer. */}
      <PodelbaDialog
        open={podelbaOpen}
        order={order}
        onClose={() => setPodelbaOpen(false)}
        onCreated={() => setPodelbaOpen(false)}
      />

      {/* Phase 17 §E7 — bulk-issue all remaining materials for a PO. */}
      <IssueMaterialDialog
        open={issueOpen}
        order={order}
        onClose={() => setIssueOpen(false)}
        onCreated={() => setIssueOpen(false)}
      />

      {/* Phase 17 §E7 — record finished-good production receipt against a PO. */}
      <ProductionReceiptDialog
        open={receiptOpen}
        order={order}
        onClose={() => setReceiptOpen(false)}
        onCreated={() => setReceiptOpen(false)}
      />

      {/* Phase 17 §E8 — atomic EX customs declaration + Shipment.
          On success, chains directly into the §E8.5 CommercialInvoice dialog
          with the just-created Shipment as the suggestion source. */}
      <ExDeclarationDialog
        open={exOpen}
        order={order}
        onClose={() => setExOpen(false)}
        onCreated={(chain) => {
          setExOpen(false);
          if (chain?.shipmentId) {
            setCiShipmentId(chain.shipmentId);
            setCiOpen(true);
          }
        }}
      />

      {/* Phase 17 §E8 — Pass / Reject FG quality. */}
      <QcDialog
        open={qcOpen}
        order={order}
        onClose={() => setQcOpen(false)}
        onCreated={() => setQcOpen(false)}
      />

      {/* Phase 17 §E8.5 — draft + create CommercialInvoice from a Shipment. */}
      <CommercialInvoiceDialog
        open={ciOpen}
        order={order}
        shipmentId={ciShipmentId}
        onClose={() => setCiOpen(false)}
        onCreated={() => setCiOpen(false)}
      />
    </Box>
  );
};

/**
 * Phase 17 §E3 — list of declarations linked to this ClientOrder.
 * Re-fetches automatically via react-query when the IM dialog invalidates
 * `clientOrderKeys.detail(id)` after a successful create.
 */
interface DeclarationRow {
  id: string;
  declarationNumber: string;
  declarationDate: string;
  mrn: string;
  declarationType: string;
  procedureCode: string;
  status: number;
  totalCustomsValue: number;
  totalDuty: number;
  currency: string;
}

const DeclarationsTab: React.FC<{ clientOrderId: string }> = ({ clientOrderId }) => {
  const { t } = useTranslation();
  const { data: rows = [], isLoading } = useQuery({
    queryKey: ['clientOrders', 'declarations', clientOrderId],
    queryFn: async () => {
      const resp = await customsApi.getDeclarations({ clientOrderId });
      return (resp.data ?? []) as DeclarationRow[];
    },
    enabled: !!clientOrderId,
  });

  if (isLoading) {
    return <LinearProgress />;
  }
  if (rows.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary" align="center" sx={{ py: 4 }}>
        {t('orders.hub.tabs.declarationsEmpty')}
      </Typography>
    );
  }
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: '1.4fr 1fr 1.4fr 0.6fr 1fr 1fr', gap: 0, fontSize: 13 }}>
      <Box sx={{ fontWeight: 600, p: 1, borderBottom: 1, borderColor: 'divider' }}>
        {t('orders.hub.tabs.declCols.number')}
      </Box>
      <Box sx={{ fontWeight: 600, p: 1, borderBottom: 1, borderColor: 'divider' }}>
        {t('orders.hub.tabs.declCols.date')}
      </Box>
      <Box sx={{ fontWeight: 600, p: 1, borderBottom: 1, borderColor: 'divider' }}>
        {t('orders.hub.tabs.declCols.mrn')}
      </Box>
      <Box sx={{ fontWeight: 600, p: 1, borderBottom: 1, borderColor: 'divider' }}>
        {t('orders.hub.tabs.declCols.type')}
      </Box>
      <Box sx={{ fontWeight: 600, p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
        {t('orders.hub.tabs.declCols.customsValue')}
      </Box>
      <Box sx={{ fontWeight: 600, p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
        {t('orders.hub.tabs.declCols.duty')}
      </Box>
      {rows.map((r) => (
        <React.Fragment key={r.id}>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace' }}>
            {r.declarationNumber}
          </Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
            {formatDate(r.declarationDate)}
          </Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace', fontSize: 11 }}>
            {r.mrn}
          </Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
            {r.declarationType}
          </Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
            {r.totalCustomsValue?.toFixed?.(2) ?? '—'} {r.currency}
          </Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
            {r.totalDuty?.toFixed?.(2) ?? '—'} {r.currency}
          </Box>
        </React.Fragment>
      ))}
    </Box>
  );
};

/**
 * Phase 17 §E5 — production orders linked to this ClientOrder via the new
 * ProductionOrder.ClientOrderId FK. Backed by
 * `GET /api/Production/orders?clientOrderId=…`.
 */
interface ProductionOrderRow {
  id: string;
  orderNumber: string;
  status: number;
  orderQuantity: number;
  producedQuantity: number;
  plannedStartDate: string;
  plannedEndDate: string;
  item?: { code: string; name?: string } | null;
}

const PO_STATUS_LABEL: Record<number, string> = {
  1: 'Draft',
  2: 'Released',
  3: 'InProgress',
  4: 'Completed',
  5: 'Closed',
  6: 'Cancelled',
};

const ProductionOrdersTab: React.FC<{ clientOrderId: string }> = ({ clientOrderId }) => {
  const { t } = useTranslation();
  const { data: rows = [], isLoading } = useQuery({
    queryKey: ['clientOrders', 'productionOrders', clientOrderId],
    queryFn: async () => {
      const resp = await productionApi.getOrders({ clientOrderId });
      return (resp.data ?? []) as ProductionOrderRow[];
    },
    enabled: !!clientOrderId,
  });
  if (isLoading) return <LinearProgress />;
  if (rows.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary" align="center" sx={{ py: 4 }}>
        {t('orders.hub.tabs.productionOrdersEmpty')}
      </Typography>
    );
  }
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: '1.6fr 1.4fr 0.8fr 0.8fr 0.8fr 0.8fr', gap: 0, fontSize: 13 }}>
      {[
        t('orders.hub.tabs.poCols.number'),
        t('orders.hub.tabs.poCols.item'),
        t('orders.hub.tabs.poCols.status'),
        t('orders.hub.tabs.poCols.orderQty'),
        t('orders.hub.tabs.poCols.producedQty'),
        t('orders.hub.tabs.poCols.plannedEnd'),
      ].map((h, i) => (
        <Box key={i} sx={{ fontWeight: 600, p: 1, borderBottom: 1, borderColor: 'divider', textAlign: i >= 3 && i <= 4 ? 'right' : 'left' }}>
          {h}
        </Box>
      ))}
      {rows.map((r) => (
        <React.Fragment key={r.id}>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace' }}>{r.orderNumber}</Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
            {r.item ? (r.item.name ? `${r.item.code} — ${r.item.name}` : r.item.code) : '—'}
          </Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>{PO_STATUS_LABEL[r.status] ?? r.status}</Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>{r.orderQuantity?.toFixed?.(2)}</Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>{r.producedQuantity?.toFixed?.(2)}</Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>{formatDate(r.plannedEndDate)}</Box>
        </React.Fragment>
      ))}
    </Box>
  );
};

/**
 * Phase 17 §E4 — receipts created against any declaration linked to this
 * ClientOrder. Backed by `GET /api/WMS/receipts?clientOrderId=…` (joins
 * receipt.lines.customsDeclaration.clientOrderId server-side).
 */
interface ReceiptRow {
  id: string;
  receiptNumber: string;
  receiptDate: string;
  referenceNumber?: string | null;
  lines?: Array<{ id: string; quantity: number; mrn?: string | null }>;
}

const ReceiptsTab: React.FC<{ clientOrderId: string }> = ({ clientOrderId }) => {
  const { t } = useTranslation();
  const { data: rows = [], isLoading } = useQuery({
    queryKey: ['clientOrders', 'receipts', clientOrderId],
    queryFn: async () => {
      const resp = await wmsApi.getReceipts({ clientOrderId, pageSize: 50 });
      return (resp.data ?? []) as ReceiptRow[];
    },
    enabled: !!clientOrderId,
  });

  if (isLoading) return <LinearProgress />;
  if (rows.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary" align="center" sx={{ py: 4 }}>
        {t('orders.hub.tabs.receiptsEmpty')}
      </Typography>
    );
  }
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: '1.6fr 1fr 1.2fr 0.6fr 0.8fr', gap: 0, fontSize: 13 }}>
      <Box sx={{ fontWeight: 600, p: 1, borderBottom: 1, borderColor: 'divider' }}>
        {t('orders.hub.tabs.recCols.number')}
      </Box>
      <Box sx={{ fontWeight: 600, p: 1, borderBottom: 1, borderColor: 'divider' }}>
        {t('orders.hub.tabs.recCols.date')}
      </Box>
      <Box sx={{ fontWeight: 600, p: 1, borderBottom: 1, borderColor: 'divider' }}>
        {t('orders.hub.tabs.recCols.reference')}
      </Box>
      <Box sx={{ fontWeight: 600, p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
        {t('orders.hub.tabs.recCols.linesCount')}
      </Box>
      <Box sx={{ fontWeight: 600, p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
        {t('orders.hub.tabs.recCols.totalQty')}
      </Box>
      {rows.map((r) => {
        const totalQty = (r.lines ?? []).reduce((s, l) => s + (l.quantity ?? 0), 0);
        return (
          <React.Fragment key={r.id}>
            <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace' }}>
              {r.receiptNumber}
            </Box>
            <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
              {formatDate(r.receiptDate)}
            </Box>
            <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
              {r.referenceNumber ?? '—'}
            </Box>
            <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
              {r.lines?.length ?? 0}
            </Box>
            <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
              {totalQty.toFixed(2)}
            </Box>
          </React.Fragment>
        );
      })}
    </Box>
  );
};

/**
 * Phase 17 §E8 — Shipments linked to this ClientOrder. Filled by the EX
 * declaration dialog (which fires `BulkShipmentFromFGCommand` with
 * `clientOrderId` set, stamping `Shipment.ClientOrderId`). Renders one row
 * per Shipment header with its line + qty totals.
 */
interface ShipmentRow {
  id: string;
  shipmentNumber: string;
  shipmentDate: string;
  status?: number;
  trackingNumber?: string | null;
  salesOrderNumber?: string | null;
  lines?: Array<{ id: string; quantity: number; mrn?: string | null }>;
}

const SHIPMENT_STATUS_LABEL: Record<number, string> = {
  1: 'Draft',
  2: 'PickingInProgress',
  3: 'Picked',
  4: 'Packed',
  5: 'Shipped',
  6: 'Delivered',
  7: 'Cancelled',
};

const ShipmentsTab: React.FC<{ clientOrderId: string }> = ({ clientOrderId }) => {
  const { t } = useTranslation();
  const { data: rows = [], isLoading } = useQuery({
    queryKey: ['clientOrders', 'shipments', clientOrderId],
    queryFn: async () => {
      const resp = await wmsApi.getShipments({ clientOrderId, pageSize: 100 });
      return (resp.data ?? []) as ShipmentRow[];
    },
    enabled: !!clientOrderId,
  });
  if (isLoading) return <LinearProgress />;
  if (rows.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary" align="center" sx={{ py: 4 }}>
        {t('orders.hub.tabs.shipmentsEmpty')}
      </Typography>
    );
  }
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: '1.4fr 1fr 1.2fr 0.7fr 0.8fr 0.8fr', gap: 0, fontSize: 13 }}>
      {[
        t('orders.hub.tabs.shipCols.number'),
        t('orders.hub.tabs.shipCols.date'),
        t('orders.hub.tabs.shipCols.reference'),
        t('orders.hub.tabs.shipCols.linesCount'),
        t('orders.hub.tabs.shipCols.totalQty'),
        t('orders.hub.tabs.shipCols.status'),
      ].map((h, i) => (
        <Box key={i} sx={{ fontWeight: 600, p: 1, borderBottom: 1, borderColor: 'divider', textAlign: i >= 3 && i <= 4 ? 'right' : 'left' }}>
          {h}
        </Box>
      ))}
      {rows.map((r) => {
        const totalQty = (r.lines ?? []).reduce((s, l) => s + (l.quantity ?? 0), 0);
        return (
          <React.Fragment key={r.id}>
            <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace' }}>{r.shipmentNumber}</Box>
            <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>{formatDate(r.shipmentDate)}</Box>
            <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>{r.salesOrderNumber ?? '—'}</Box>
            <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>{r.lines?.length ?? 0}</Box>
            <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>{totalQty.toFixed(2)}</Box>
            <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
              {r.status !== undefined ? (SHIPMENT_STATUS_LABEL[r.status] ?? r.status) : '—'}
            </Box>
          </React.Fragment>
        );
      })}
    </Box>
  );
};

/**
 * Phase 17 §E6 — InventoryBalance scoped to this ClientOrder. Materials are
 * those referenced by any ProductionOrderMaterial on a ProductionOrder linked
 * to the order. Groups rows by producer assignment so the Podelba flow is
 * legible: unassigned (HQ pool) rendered first, then per-producer buckets.
 */
interface MaterialRow {
  id: string;
  itemId: string;
  batchNumber?: string | null;
  mrn?: string | null;
  quantity: number;
  qualityStatus: number;
  assignedProducerId?: string | null;
  assignedProducer?: { id: string; code: string; name?: string } | null;
  item?: { code: string; name?: string } | null;
  location?: { code: string; name?: string } | null;
  uoM?: { code: string } | null;
}

const MaterialsTab: React.FC<{ clientOrderId: string }> = ({ clientOrderId }) => {
  const { t } = useTranslation();
  const { data: rows = [], isLoading } = useQuery({
    queryKey: ['clientOrders', 'materials', clientOrderId],
    queryFn: async () => {
      const resp = await wmsApi.getInventory(undefined, undefined, { clientOrderId });
      return (resp.data ?? []) as MaterialRow[];
    },
    enabled: !!clientOrderId,
  });

  if (isLoading) return <LinearProgress />;

  const positive = rows.filter((r) => (r.quantity ?? 0) > 0);
  if (positive.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary" align="center" sx={{ py: 4 }}>
        {t('orders.hub.tabs.materialsEmpty')}
      </Typography>
    );
  }

  const unassigned = positive.filter((r) => !r.assignedProducerId);
  const byProducer = new Map<string, { name: string; code: string; rows: MaterialRow[] }>();
  for (const r of positive) {
    if (!r.assignedProducerId) continue;
    const key = r.assignedProducerId;
    if (!byProducer.has(key)) {
      byProducer.set(key, {
        code: r.assignedProducer?.code ?? '?',
        name: r.assignedProducer?.name ?? r.assignedProducer?.code ?? '?',
        rows: [],
      });
    }
    byProducer.get(key)!.rows.push(r);
  }

  const renderTable = (group: MaterialRow[]) => (
    <Box
      sx={{
        display: 'grid',
        gridTemplateColumns: '1.6fr 1fr 1.2fr 1fr 0.8fr',
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
        t('orders.hub.tabs.matCols.item'),
        t('orders.hub.tabs.matCols.batch'),
        t('orders.hub.tabs.matCols.mrn'),
        t('orders.hub.tabs.matCols.location'),
        t('orders.hub.tabs.matCols.quantity'),
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
      {group.map((r) => (
        <React.Fragment key={r.id}>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
            {r.item ? (r.item.name ? `${r.item.code} — ${r.item.name}` : r.item.code) : '—'}
          </Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace' }}>
            {r.batchNumber ?? '—'}
          </Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace', fontSize: 11 }}>
            {r.mrn ?? '—'}
          </Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
            {r.location?.code ?? '—'}
          </Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
            {r.quantity.toFixed(2)} {r.uoM?.code ?? ''}
          </Box>
        </React.Fragment>
      ))}
    </Box>
  );

  return (
    <Box>
      {unassigned.length > 0 && (
        <>
          <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 1 }}>
            <Typography variant="overline" color="text.secondary">
              {t('orders.hub.tabs.matGroup.unassigned')}
            </Typography>
            <Chip
              label={
                t('orders.hub.tabs.matGroup.count', {
                  count: unassigned.length,
                  qty: unassigned.reduce((s, r) => s + r.quantity, 0).toFixed(2),
                }) as string
              }
              size="small"
            />
          </Stack>
          {renderTable(unassigned)}
        </>
      )}
      {Array.from(byProducer.entries()).map(([producerId, group]) => (
        <Box key={producerId}>
          <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 1 }}>
            <Typography variant="overline" color="text.secondary">
              {group.code} — {group.name}
            </Typography>
            <Chip
              label={
                t('orders.hub.tabs.matGroup.count', {
                  count: group.rows.length,
                  qty: group.rows.reduce((s, r) => s + r.quantity, 0).toFixed(2),
                }) as string
              }
              size="small"
              color="primary"
              variant="outlined"
            />
          </Stack>
          {renderTable(group.rows)}
        </Box>
      ))}
    </Box>
  );
};

/**
 * Phase 17 §E8.5 — list of CommercialInvoices linked to this ClientOrder.
 * Re-fetches automatically via react-query when CommercialInvoiceDialog
 * invalidates `['clientOrders', 'commercialInvoices', id]`.
 */
interface CommercialInvoiceRow {
  id: string;
  number: string;
  invoiceDate: string;
  consigneeName?: string | null;
  consignorName?: string | null;
  currency: string;
  totalAmount: number;
  status: number;
  statusName: string;
  shipmentNumber?: string | null;
}

const CI_STATUS_LABEL: Record<number, string> = {
  1: 'Draft',
  2: 'Issued',
  3: 'Cancelled',
};

const CommercialInvoicesTab: React.FC<{ clientOrderId: string }> = ({ clientOrderId }) => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { data: rows = [], isLoading } = useQuery({
    queryKey: ['clientOrders', 'commercialInvoices', clientOrderId],
    queryFn: async () => {
      const resp = await commercialInvoicesApi.getList({ clientOrderId, pageSize: 100 });
      return (resp.data?.data ?? []) as CommercialInvoiceRow[];
    },
    enabled: !!clientOrderId,
  });

  if (isLoading) return <LinearProgress />;
  if (rows.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary" align="center" sx={{ py: 4 }}>
        {t('orders.hub.tabs.commercialInvoicesEmpty')}
      </Typography>
    );
  }
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: '1.2fr 1fr 1.4fr 1.4fr 1fr 0.7fr 0.7fr', gap: 0, fontSize: 13 }}>
      {[
        t('orders.hub.tabs.ciCols.number'),
        t('orders.hub.tabs.ciCols.date'),
        t('orders.hub.tabs.ciCols.consignor'),
        t('orders.hub.tabs.ciCols.consignee'),
        t('orders.hub.tabs.ciCols.shipment'),
        t('orders.hub.tabs.ciCols.total'),
        t('orders.hub.tabs.ciCols.status'),
      ].map((h, i) => (
        <Box
          key={i}
          sx={{
            fontWeight: 600,
            p: 1,
            borderBottom: 1,
            borderColor: 'divider',
            textAlign: i === 5 ? 'right' : 'left',
          }}
        >
          {h}
        </Box>
      ))}
      {rows.map((r) => (
        <React.Fragment key={r.id}>
          <Box
            sx={{
              p: 1,
              borderBottom: 1,
              borderColor: 'divider',
              fontFamily: 'monospace',
              cursor: 'pointer',
              color: 'primary.main',
            }}
            onClick={() => navigate(`/customs/commercial-invoices/${r.id}`)}
          >
            {r.number}
          </Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
            {formatDate(r.invoiceDate)}
          </Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
            {r.consignorName ?? '—'}
          </Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
            {r.consigneeName ?? '—'}
          </Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', fontFamily: 'monospace', fontSize: 12 }}>
            {r.shipmentNumber ?? '—'}
          </Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right' }}>
            {(r.totalAmount ?? 0).toFixed(2)} {r.currency}
          </Box>
          <Box sx={{ p: 1, borderBottom: 1, borderColor: 'divider' }}>
            {CI_STATUS_LABEL[r.status] ?? r.status}
          </Box>
        </React.Fragment>
      ))}
    </Box>
  );
};

export default OrderHub;
