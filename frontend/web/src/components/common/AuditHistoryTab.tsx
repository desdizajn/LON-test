import React from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Link as MuiLink,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../services/api';
import { formatDate } from '../../utils/format';

interface AuditRow {
  id: string;
  entityType: string;
  entityId: string;
  action: string;
  changesJson: string;
  userId: string | null;
  userName: string | null;
  occurredAt: string;
}

interface AuditHistoryTabProps {
  /** EntityType label used by the AuditLogEntry (e.g. "ClientOrder"). */
  entityType: string;
  /** Subject entity id. */
  entityId: string;
  /** How many entries to show (default 20). */
  limit?: number;
}

/**
 * Phase 17 §E13 — per-entity Audit history strip. Reads the last
 * <c>limit</c> rows from /api/audit filtered by entityType+entityId.
 * Mounted by detail pages that want to surface "who changed what".
 *
 * The full history lives at /admin/audit-log; the "View full history"
 * link drops the user there with pre-filled filters.
 */
const ACTION_COLOR: Record<string, 'default' | 'success' | 'info' | 'warning' | 'error'> = {
  Create: 'success',
  Update: 'info',
  Delete: 'warning',
  HardDelete: 'error',
};

const AuditHistoryTab: React.FC<AuditHistoryTabProps> = ({ entityType, entityId, limit = 20 }) => {
  const { t } = useTranslation();

  const { data, isLoading, error } = useQuery({
    queryKey: ['audit', entityType, entityId, limit],
    queryFn: async () => {
      const resp = await api.get<AuditRow[]>('/audit', {
        params: { entityType, entityId, take: limit },
      });
      return resp.data ?? [];
    },
    enabled: !!entityId,
  });

  if (isLoading) {
    return (
      <Stack direction="row" spacing={1} alignItems="center" sx={{ py: 1 }}>
        <CircularProgress size={18} />
        <Typography>{t('common.loading')}</Typography>
      </Stack>
    );
  }
  if (error) {
    return <Alert severity="error">{t('audit.tab.error')}</Alert>;
  }
  if (!data || data.length === 0) {
    return <Alert severity="info">{t('audit.tab.empty')}</Alert>;
  }

  return (
    <Box>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
        <Typography variant="subtitle2">
          {t('audit.tab.heading', { count: data.length })}
        </Typography>
        <MuiLink
          component={RouterLink}
          to={`/admin/audit-log?entityType=${encodeURIComponent(entityType)}&entityId=${entityId}`}
          variant="caption"
        >
          {t('audit.tab.viewAll')} →
        </MuiLink>
      </Stack>
      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>{t('audit.tab.col.when')}</TableCell>
              <TableCell>{t('audit.tab.col.action')}</TableCell>
              <TableCell>{t('audit.tab.col.user')}</TableCell>
              <TableCell>{t('audit.tab.col.changes')}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data.map((row) => (
              <TableRow key={row.id} hover>
                <TableCell sx={{ whiteSpace: 'nowrap' }}>
                  {formatDate(row.occurredAt)} {new Date(row.occurredAt).toLocaleTimeString()}
                </TableCell>
                <TableCell>
                  <Chip
                    size="small"
                    label={row.action}
                    color={ACTION_COLOR[row.action] ?? 'default'}
                    variant="outlined"
                  />
                </TableCell>
                <TableCell>{row.userName ?? '—'}</TableCell>
                <TableCell sx={{ maxWidth: 480, overflow: 'hidden', textOverflow: 'ellipsis' }}>
                  <Typography
                    variant="caption"
                    component="pre"
                    sx={{ fontFamily: 'monospace', whiteSpace: 'pre-wrap', m: 0 }}
                  >
                    {prettyChanges(row.changesJson)}
                  </Typography>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
};

function prettyChanges(json: string | null | undefined): string {
  if (!json) return '';
  try {
    const parsed = JSON.parse(json);
    return JSON.stringify(parsed, null, 2);
  } catch {
    return json ?? '';
  }
}

export default AuditHistoryTab;
