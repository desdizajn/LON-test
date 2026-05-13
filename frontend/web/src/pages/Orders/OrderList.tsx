import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import {
  Box,
  Button,
  Chip,
  Grid,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import { toast } from 'react-toastify';
import DataTable, { Column } from '../../components/common/DataTable';
import FormDialog from '../../components/common/FormDialog';
import FormInput from '../../components/forms/FormInput';
import FormSelect from '../../components/forms/FormSelect';
import {
  ClientOrderStatus,
  ClientOrderSummaryDto,
  useClientOrders,
  useCreateClientOrder,
} from '../../hooks/queries/useClientOrders';
import { customsApi, masterDataApi } from '../../services/api';
import { formatDate } from '../../utils/format';

interface PartnerOption {
  id: string;
  name: string;
  partnerType?: number | string | null;
}

interface AuthorizationOption {
  id: string;
  authorizationNumber: string;
  importerName?: string | null;
  isActive?: boolean;
}

interface CreateFormData {
  customerPartnerId: string;
  lonAuthorizationId: string;
  customerOrderReference: string;
  orderDate: string;
  requestedShipDate: string;
  notes: string;
}

const STATUS_COLOR: Record<ClientOrderStatus, 'default' | 'info' | 'warning' | 'success' | 'error'> = {
  0: 'default', // Draft
  1: 'info',    // Active
  2: 'warning', // Producing
  3: 'success', // Shipped
  4: 'success', // Closed
  99: 'error',  // Cancelled
};

const todayIso = () => new Date().toISOString().slice(0, 10);

const OrderList: React.FC = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();

  const [statusFilter, setStatusFilter] = useState<ClientOrderStatus | 'all'>('all');
  const [customerFilter, setCustomerFilter] = useState<string>('');
  const [fromDate, setFromDate] = useState<string>('');
  const [toDate, setToDate] = useState<string>('');
  const [createOpen, setCreateOpen] = useState(false);

  const [partners, setPartners] = useState<PartnerOption[]>([]);
  const [authorizations, setAuthorizations] = useState<AuthorizationOption[]>([]);

  const { data: orders = [], isLoading } = useClientOrders({
    status: statusFilter === 'all' ? undefined : statusFilter,
    customerPartnerId: customerFilter || undefined,
    fromDate: fromDate || undefined,
    toDate: toDate || undefined,
  });
  const createMut = useCreateClientOrder();

  useEffect(() => {
    masterDataApi
      .getPartners()
      .then((r) => setPartners((r.data?.data ?? r.data ?? []) as PartnerOption[]))
      .catch(() => setPartners([]));
    customsApi
      .getLONAuthorizations(true)
      .then((r) => setAuthorizations((r.data?.data ?? r.data ?? []) as AuthorizationOption[]))
      .catch(() => setAuthorizations([]));
  }, []);

  const { control, handleSubmit, reset, watch } = useForm<CreateFormData>({
    defaultValues: {
      customerPartnerId: '',
      lonAuthorizationId: '',
      customerOrderReference: '',
      orderDate: todayIso(),
      requestedShipDate: '',
      notes: '',
    },
  });

  const partnerOptions = useMemo(
    () => partners.map((p) => ({ value: p.id, label: p.name })),
    [partners],
  );
  const authOptions = useMemo(
    () =>
      authorizations.map((a) => ({
        value: a.id,
        label: a.importerName
          ? `${a.authorizationNumber} — ${a.importerName}`
          : a.authorizationNumber,
      })),
    [authorizations],
  );

  const selectedCustomerId = watch('customerPartnerId');
  const customerLabel = useMemo(
    () => partners.find((p) => p.id === selectedCustomerId)?.name ?? '',
    [partners, selectedCustomerId],
  );

  const onCreate = async (data: CreateFormData) => {
    try {
      const newId = await createMut.mutateAsync({
        customerPartnerId: data.customerPartnerId,
        lonAuthorizationId: data.lonAuthorizationId,
        customerOrderReference: data.customerOrderReference || null,
        orderDate: data.orderDate || null,
        requestedShipDate: data.requestedShipDate || null,
        notes: data.notes || null,
      });
      toast.success(t('orders.list.created') as string);
      setCreateOpen(false);
      reset();
      navigate(`/orders/${newId}`);
    } catch (err: any) {
      toast.error(err?.message || (t('orders.list.createFailed') as string));
    }
  };

  const columns: Column<ClientOrderSummaryDto>[] = [
    {
      id: 'orderNumber',
      label: t('orders.list.cols.orderNumber') as string,
      minWidth: 150,
      format: (val: string, row) => (
        <Box
          component="span"
          sx={{ color: 'primary.main', fontWeight: 600, cursor: 'pointer' }}
          onClick={() => navigate(`/orders/${row.id}`)}
        >
          {val}
        </Box>
      ),
    },
    {
      id: 'customerPartnerName',
      label: t('orders.list.cols.customer') as string,
      minWidth: 180,
      format: (v: string | null) => v || '—',
    },
    {
      id: 'status',
      label: t('orders.list.cols.status') as string,
      minWidth: 110,
      format: (_v, row) => (
        <Chip label={row.statusName} color={STATUS_COLOR[row.status]} size="small" />
      ),
    },
    {
      id: 'orderDate',
      label: t('orders.list.cols.orderDate') as string,
      minWidth: 110,
      format: (v: string) => formatDate(v),
    },
    {
      id: 'requestedShipDate',
      label: t('orders.list.cols.requestedShipDate') as string,
      minWidth: 130,
      format: (v: string | null) => (v ? formatDate(v) : '—'),
    },
    {
      id: 'producedPct',
      label: t('orders.list.cols.producedPct') as string,
      minWidth: 90,
      align: 'right',
      format: () => '0%', // E7 wires the real number
    },
    {
      id: 'guaranteePct',
      label: t('orders.list.cols.guaranteePct') as string,
      minWidth: 90,
      align: 'right',
      format: () => '0%', // E3 wires the real number from GuaranteeLedger
    },
    {
      id: 'declarationsCount',
      label: t('orders.list.cols.declarations') as string,
      minWidth: 80,
      align: 'right',
    },
    {
      id: 'productionOrdersCount',
      label: t('orders.list.cols.productionOrders') as string,
      minWidth: 80,
      align: 'right',
    },
  ];

  return (
    <Box p={3}>
      <Stack direction="row" justifyContent="space-between" alignItems="center" mb={2}>
        <Box>
          <Typography variant="h4">{t('orders.list.title')}</Typography>
          <Typography variant="body2" color="text.secondary">
            {t('orders.list.subtitle')}
          </Typography>
        </Box>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={() => setCreateOpen(true)}
        >
          {t('orders.list.newOrder')}
        </Button>
      </Stack>

      <Paper sx={{ p: 2, mb: 2 }}>
        <Grid container spacing={2}>
          <Grid item xs={12} sm={3}>
            <TextField
              select
              fullWidth
              size="small"
              label={t('orders.list.filters.status')}
              value={statusFilter}
              onChange={(e) => {
                const v = e.target.value;
                setStatusFilter(v === 'all' ? 'all' : (Number(v) as ClientOrderStatus));
              }}
            >
              <MenuItem value="all">{t('orders.list.filters.statusAll')}</MenuItem>
              <MenuItem value={0}>{t('orders.statusNames.draft')}</MenuItem>
              <MenuItem value={1}>{t('orders.statusNames.active')}</MenuItem>
              <MenuItem value={2}>{t('orders.statusNames.producing')}</MenuItem>
              <MenuItem value={3}>{t('orders.statusNames.shipped')}</MenuItem>
              <MenuItem value={4}>{t('orders.statusNames.closed')}</MenuItem>
            </TextField>
          </Grid>
          <Grid item xs={12} sm={3}>
            <TextField
              select
              fullWidth
              size="small"
              label={t('orders.list.filters.customer')}
              value={customerFilter}
              onChange={(e) => setCustomerFilter(e.target.value)}
            >
              <MenuItem value="">{t('orders.list.filters.customerAll')}</MenuItem>
              {partners.map((p) => (
                <MenuItem key={p.id} value={p.id}>
                  {p.name}
                </MenuItem>
              ))}
            </TextField>
          </Grid>
          <Grid item xs={6} sm={3}>
            <TextField
              type="date"
              fullWidth
              size="small"
              label={t('orders.list.filters.fromDate')}
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
              InputLabelProps={{ shrink: true }}
            />
          </Grid>
          <Grid item xs={6} sm={3}>
            <TextField
              type="date"
              fullWidth
              size="small"
              label={t('orders.list.filters.toDate')}
              value={toDate}
              onChange={(e) => setToDate(e.target.value)}
              InputLabelProps={{ shrink: true }}
            />
          </Grid>
        </Grid>
      </Paper>

      <DataTable<ClientOrderSummaryDto>
        columns={columns}
        data={orders}
        loading={isLoading}
        onView={(row) => navigate(`/orders/${row.id}`)}
        emptyMessage={t('orders.list.empty') as string}
        searchPlaceholder={t('orders.list.searchPlaceholder') as string}
      />

      <FormDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        title={t('orders.list.dialog.title') as string}
        submitText={t('common.save') as string}
        cancelText={t('common.cancel') as string}
        onSubmit={handleSubmit(onCreate)}
        isSubmitting={createMut.isPending}
        maxWidth="sm"
      >
        <Grid container spacing={2}>
          <Grid item xs={12}>
            <FormSelect
              name="customerPartnerId"
              control={control}
              label={t('orders.list.dialog.customer') as string}
              options={partnerOptions}
              rules={{ required: t('orders.list.dialog.customerRequired') as string }}
            />
            {customerLabel && (
              <Typography variant="caption" color="text.secondary">
                {customerLabel}
              </Typography>
            )}
          </Grid>
          <Grid item xs={12}>
            <FormSelect
              name="lonAuthorizationId"
              control={control}
              label={t('orders.list.dialog.authorization') as string}
              options={authOptions}
              rules={{ required: t('orders.list.dialog.authorizationRequired') as string }}
            />
          </Grid>
          <Grid item xs={12}>
            <FormInput
              name="customerOrderReference"
              control={control}
              label={t('orders.list.dialog.customerRef') as string}
              placeholder="(optional)"
            />
          </Grid>
          <Grid item xs={6}>
            <FormInput
              name="orderDate"
              control={control}
              label={t('orders.list.dialog.orderDate') as string}
              type="date"
              InputLabelProps={{ shrink: true }}
            />
          </Grid>
          <Grid item xs={6}>
            <FormInput
              name="requestedShipDate"
              control={control}
              label={t('orders.list.dialog.requestedShipDate') as string}
              type="date"
              InputLabelProps={{ shrink: true }}
            />
          </Grid>
          <Grid item xs={12}>
            <FormInput
              name="notes"
              control={control}
              label={t('orders.list.dialog.notes') as string}
              multiline
              rows={3}
            />
          </Grid>
        </Grid>
      </FormDialog>
    </Box>
  );
};

export default OrderList;
