import React, { useState } from 'react';
import { useParams, useNavigate, Link as RouterLink } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
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
import { useQuery } from '@tanstack/react-query';
import {
  ClientOrderStatus,
  useClientOrder,
} from '../../hooks/queries/useClientOrders';
import { customsApi } from '../../services/api';
import { formatDate } from '../../utils/format';
import ImDeclarationDialog from './ImDeclarationDialog';

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
  wiresInTask: 'E3' | 'E4' | 'E5' | 'E6' | 'E7' | 'E8' | 'E9' | 'E10' | 'E13';
  /** When false, the button is disabled with the "Coming in §E…" tooltip. */
  enabled?: boolean;
}

const ACTIONS: ActionDef[] = [
  { key: 'bom', labelKey: 'orders.actions.bom', icon: <FactoryIcon />, wiresInTask: 'E5' },
  // Phase 17 §E3 — IM action launches an inline dialog (handled below).
  { key: 'imDeclaration', labelKey: 'orders.actions.imDeclaration', icon: <InventoryIcon />, wiresInTask: 'E3', enabled: true },
  { key: 'receive', labelKey: 'orders.actions.receive', icon: <LocalShippingIcon />, wiresInTask: 'E4' },
  { key: 'podelba', labelKey: 'orders.actions.podelba', icon: <CallSplitIcon />, wiresInTask: 'E6' },
  { key: 'issueMaterial', labelKey: 'orders.actions.issueMaterial', icon: <HandymanIcon />, wiresInTask: 'E7' },
  { key: 'exDeclaration', labelKey: 'orders.actions.exDeclaration', icon: <FlightTakeoffIcon />, wiresInTask: 'E8' },
  { key: 'razdolzuvanje', labelKey: 'orders.actions.razdolzuvanje', icon: <LocalAtmIcon />, wiresInTask: 'E9' },
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

  const { data: order, isLoading, error } = useClientOrder(id);
  const [tab, setTab] = useState(0);
  const [imOpen, setImOpen] = useState(false);

  const handleActionClick = (actionKey: string) => {
    if (actionKey === 'imDeclaration') {
      setImOpen(true);
    }
    // Other action keys are still disabled in this phase.
  };

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

  // Real numbers wire in §E3/§E7. Placeholders for now.
  const producedPct = 0;
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
            </Tabs>
            <Divider sx={{ my: 2 }} />
            <Box minHeight={200}>
              {tab === 0 && <DeclarationsTab clientOrderId={order.id} />}
              {false && (
                <Typography variant="body2" color="text.secondary" align="center" sx={{ py: 4 }}>
                  {t('orders.hub.tabs.declarationsPlaceholder')}
                </Typography>
              )}
              {tab === 1 && (
                <Typography variant="body2" color="text.secondary" align="center" sx={{ py: 4 }}>
                  {t('orders.hub.tabs.productionOrdersPlaceholder')}
                </Typography>
              )}
              {tab === 2 && (
                <Typography variant="body2" color="text.secondary" align="center" sx={{ py: 4 }}>
                  {t('orders.hub.tabs.shipmentsPlaceholder')}
                </Typography>
              )}
              {tab === 3 && (
                <Typography variant="body2" color="text.secondary" align="center" sx={{ py: 4 }}>
                  {t('orders.hub.tabs.materialsPlaceholder')}
                </Typography>
              )}
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

export default OrderHub;
