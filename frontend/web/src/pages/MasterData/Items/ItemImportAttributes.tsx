import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Paper,
  Typography,
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableCell,
  Chip,
  Box,
  LinearProgress,
} from '@mui/material';
import { itemsAdminApi } from '../../../services/api';

/**
 * P6.31 consumer — drill-in panel shown inside ItemDetail.
 * Fetches distinct (tariff × country × supplier × rates) combinations across
 * active MRN declarations for one item, plus aggregated balance per combo.
 */

interface ImportAttrRow {
  tariffCode: string | null;
  countryOfOrigin: string | null;
  isPreferentialOrigin: boolean;
  supplierId: string | null;
  supplierCode: string | null;
  supplierName: string | null;
  dutyRate: number;
  vatRate: number;
  batchCount: number;
  availableQuantity: number;
}

interface ImportAttrResponse {
  itemId: string;
  itemCode: string | null;
  itemName: string | null;
  rows: ImportAttrRow[];
}

interface Props {
  itemId: string;
}

const ItemImportAttributes: React.FC<Props> = ({ itemId }) => {
  const { t } = useTranslation();
  const [data, setData] = useState<ImportAttrResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    itemsAdminApi
      .getImportAttributes(itemId)
      .then((resp) => {
        if (!cancelled) setData(resp.data as ImportAttrResponse);
      })
      .catch(() => {
        if (!cancelled) setError(t('itemAttributes.loadError'));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [itemId, t]);

  return (
    <Paper sx={{ p: 2, mt: 2 }}>
      <Typography variant="h6" gutterBottom>
        📊 {t('itemAttributes.title')}
      </Typography>
      <Typography variant="body2" color="text.secondary" paragraph>
        {t('itemAttributes.subtitle')}
      </Typography>

      {loading && <LinearProgress />}
      {error && (
        <Box sx={{ color: 'error.main', my: 2 }}>
          <Typography variant="body2">{error}</Typography>
        </Box>
      )}

      {!loading && !error && data && data.rows.length === 0 && (
        <Typography variant="body2" color="text.secondary">
          {t('itemAttributes.noData')}
        </Typography>
      )}

      {!loading && !error && data && data.rows.length > 0 && (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>{t('itemAttributes.tariffCode')}</TableCell>
              <TableCell>{t('itemAttributes.country')}</TableCell>
              <TableCell>{t('itemAttributes.preferential')}</TableCell>
              <TableCell>{t('itemAttributes.supplier')}</TableCell>
              <TableCell align="right">{t('itemAttributes.dutyRate')}</TableCell>
              <TableCell align="right">{t('itemAttributes.vatRate')}</TableCell>
              <TableCell align="right">{t('itemAttributes.batches')}</TableCell>
              <TableCell align="right">{t('itemAttributes.availableQty')}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data.rows.map((r, i) => (
              <TableRow key={i}>
                <TableCell sx={{ fontFamily: 'monospace' }}>{r.tariffCode ?? '—'}</TableCell>
                <TableCell>{r.countryOfOrigin ?? '—'}</TableCell>
                <TableCell>
                  {r.isPreferentialOrigin ? (
                    <Chip size="small" label="✓" color="success" />
                  ) : (
                    '—'
                  )}
                </TableCell>
                <TableCell>
                  {r.supplierCode ?? ''} {r.supplierName && `— ${r.supplierName}`}
                </TableCell>
                <TableCell align="right">{(r.dutyRate * 100).toFixed(2)}%</TableCell>
                <TableCell align="right">{(r.vatRate).toFixed(2)}%</TableCell>
                <TableCell align="right">{r.batchCount}</TableCell>
                <TableCell align="right">
                  <strong>{r.availableQuantity.toLocaleString(undefined, { maximumFractionDigits: 3 })}</strong>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </Paper>
  );
};

export default ItemImportAttributes;
