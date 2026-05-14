import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  Typography,
} from '@mui/material';
import RestoreIcon from '@mui/icons-material/Restore';
import DeleteForeverIcon from '@mui/icons-material/DeleteForever';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../services/api';
import { formatDate } from '../../utils/format';
import { translateError } from '../../utils/translateError';

/**
 * Phase 17 §E14 — admin recycle bin. v1 shows ClientOrder soft-deletes only.
 * Restore = un-soft-delete via POST /api/admin/recycle-bin/client-orders/{id}/restore.
 * Permanent delete = DELETE /api/admin/recycle-bin/client-orders/{id}/permanent.
 * The 90-day retention worker (LON.Worker.SoftDeleteRetentionJob) hard-deletes
 * older rows automatically; this UI is for sooner cleanup or recovery.
 */

interface RecycleBinRow {
  entityType: string;
  entityId: string;
  label: string;
  deletedAt: string | null;
  deletedBy: string | null;
  additionalInfo: string | null;
}

const RecycleBin: React.FC = () => {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(25);
  const [actionError, setActionError] = useState<string | null>(null);

  const { data, isLoading, error } = useQuery({
    queryKey: ['admin', 'recycle-bin', page, pageSize],
    queryFn: async () => {
      const resp = await api.get('/admin/recycle-bin', {
        params: { page: page + 1, pageSize },
      });
      const env = resp.data as { data?: RecycleBinRow[] };
      return env?.data ?? (resp.data as RecycleBinRow[]);
    },
  });

  const restoreMutation = useMutation({
    mutationFn: (id: string) =>
      api.post(`/admin/recycle-bin/client-orders/${id}/restore`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'recycle-bin'] }),
    onError: (err) => setActionError(translateError(err)),
  });

  const permanentDeleteMutation = useMutation({
    mutationFn: (id: string) =>
      api.delete(`/admin/recycle-bin/client-orders/${id}/permanent`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'recycle-bin'] }),
    onError: (err) => setActionError(translateError(err)),
  });

  const handleRestore = (id: string) => {
    setActionError(null);
    restoreMutation.mutate(id);
  };

  const handlePermanentDelete = (id: string, label: string) => {
    setActionError(null);
    // eslint-disable-next-line no-alert
    if (!window.confirm(t('recycleBin.confirmPermanent', { label }))) return;
    permanentDeleteMutation.mutate(id);
  };

  const rows = data ?? [];

  return (
    <Box p={3}>
      <Typography variant="h4" gutterBottom>{t('recycleBin.title')}</Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        {t('recycleBin.subtitle')}
      </Typography>

      {error && <Alert severity="error">{t('recycleBin.loadError')}</Alert>}
      {actionError && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setActionError(null)}>{actionError}</Alert>
      )}

      {isLoading ? (
        <Stack direction="row" alignItems="center" spacing={1}><CircularProgress size={20} /> <Typography>{t('common.loading')}</Typography></Stack>
      ) : rows.length === 0 ? (
        <Alert severity="success">{t('recycleBin.empty')}</Alert>
      ) : (
        <TableContainer>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>{t('recycleBin.col.type')}</TableCell>
                <TableCell>{t('recycleBin.col.label')}</TableCell>
                <TableCell>{t('recycleBin.col.deletedAt')}</TableCell>
                <TableCell>{t('recycleBin.col.deletedBy')}</TableCell>
                <TableCell>{t('recycleBin.col.reason')}</TableCell>
                <TableCell align="right">{t('common.actions')}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.map((row) => (
                <TableRow key={row.entityId} hover>
                  <TableCell>
                    <Chip size="small" label={row.entityType} variant="outlined" />
                  </TableCell>
                  <TableCell>{row.label}</TableCell>
                  <TableCell sx={{ whiteSpace: 'nowrap' }}>
                    {row.deletedAt ? `${formatDate(row.deletedAt)} ${new Date(row.deletedAt).toLocaleTimeString()}` : '—'}
                  </TableCell>
                  <TableCell>{row.deletedBy ?? '—'}</TableCell>
                  <TableCell>{row.additionalInfo ?? '—'}</TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={1} justifyContent="flex-end">
                      <Button
                        size="small"
                        startIcon={<RestoreIcon />}
                        onClick={() => handleRestore(row.entityId)}
                        disabled={restoreMutation.isPending}
                      >
                        {t('recycleBin.restore')}
                      </Button>
                      <Button
                        size="small"
                        color="error"
                        startIcon={<DeleteForeverIcon />}
                        onClick={() => handlePermanentDelete(row.entityId, row.label)}
                        disabled={permanentDeleteMutation.isPending}
                      >
                        {t('recycleBin.permanentDelete')}
                      </Button>
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <TablePagination
            component="div"
            count={-1}
            page={page}
            onPageChange={(_, p) => setPage(p)}
            rowsPerPage={pageSize}
            onRowsPerPageChange={(e) => { setPageSize(parseInt(e.target.value, 10)); setPage(0); }}
            rowsPerPageOptions={[10, 25, 50, 100]}
          />
        </TableContainer>
      )}
    </Box>
  );
};

export default RecycleBin;
