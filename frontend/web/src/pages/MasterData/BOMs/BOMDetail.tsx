import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  Box,
  Card,
  CardContent,
  Grid,
  Typography,
  Chip,
  Button,
  Divider,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
} from '@mui/material';
import { ArrowBack, Edit } from '@mui/icons-material';
import { bomsApi } from '../../../services/masterDataApi';
import LoadingSpinner from '../../../components/common/LoadingSpinner';
import ErrorDisplay from '../../../components/common/ErrorDisplay';
import { showError } from '../../../utils/toast';
import type { BOM } from '../../../types/masterData';

const BOMDetail: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [bom, setBom] = useState<BOM | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadBOM();
  }, [id]);

  const loadBOM = async () => {
    if (!id) return;
    try {
      setLoading(true);
      setError(null);
      const response = await bomsApi.getById(id);
      setBom(response.data);
    } catch (err: any) {
      const errorMsg = err.response?.data?.message || 'Failed to load BOM';
      setError(errorMsg);
      showError(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return <LoadingSpinner message="Loading BOM details..." />;
  }

  if (error) {
    return (
      <ErrorDisplay
        message={error}
        onRetry={loadBOM}
        actionButton={
          <Button
            variant="outlined"
            startIcon={<ArrowBack />}
            onClick={() => navigate('/master-data/boms')}
          >
            Back to BOMs
          </Button>
        }
      />
    );
  }

  if (!bom) {
    return <ErrorDisplay message="BOM not found" />;
  }

  return (
    <Box>
      <Box sx={{ mb: 3, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <Button
            variant="outlined"
            startIcon={<ArrowBack />}
            onClick={() => navigate('/master-data/boms')}
          >
            Back
          </Button>
          <Typography variant="h5">BOM Details</Typography>
        </Box>
        <Button
          variant="contained"
          startIcon={<Edit />}
          onClick={() => navigate(`/master-data/boms/${id}/edit`)}
        >
          Edit
        </Button>
      </Box>

      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Grid container spacing={3}>
            <Grid item xs={12}>
              <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', mb: 2 }}>
                <Typography variant="h6">{bom.item?.name || 'N/A'}</Typography>
                <Chip label={`Version ${bom.version}`} color="primary" size="small" />
                <Chip
                  label={bom.isActive ? 'Active' : 'Inactive'}
                  color={bom.isActive ? 'success' : 'default'}
                  size="small"
                />
              </Box>
              <Divider />
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Item Code
              </Typography>
              <Typography variant="body1">{bom.item?.code || '-'}</Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Item Name
              </Typography>
              <Typography variant="body1">{bom.item?.name || '-'}</Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography variant="subtitle2" color="text.secondary">
                Version
              </Typography>
              <Typography variant="body1">{bom.version}</Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography variant="subtitle2" color="text.secondary">
                Quantity
              </Typography>
              <Typography variant="body1">
                {bom.quantity} {bom.uoM?.code || ''}
              </Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography variant="subtitle2" color="text.secondary">
                Unit of Measure
              </Typography>
              <Typography variant="body1">{bom.uoM?.name || '-'}</Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Valid From
              </Typography>
              <Typography variant="body1">
                {bom.validFrom ? new Date(bom.validFrom).toLocaleDateString() : '-'}
              </Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Valid To
              </Typography>
              <Typography variant="body1">
                {bom.validTo ? new Date(bom.validTo).toLocaleDateString() : '-'}
              </Typography>
            </Grid>

            {bom.notes && (
              <Grid item xs={12}>
                <Typography variant="subtitle2" color="text.secondary">
                  Notes
                </Typography>
                <Typography variant="body1">{bom.notes}</Typography>
              </Grid>
            )}

            <Grid item xs={12}>
              <Divider sx={{ my: 1 }} />
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Created At
              </Typography>
              <Typography variant="body1">
                {new Date(bom.createdAt).toLocaleString()}
              </Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Last Modified
              </Typography>
              <Typography variant="body1">
                {bom.updatedAt ? new Date(bom.updatedAt).toLocaleString() : '-'}
              </Typography>
            </Grid>
          </Grid>
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <Typography variant="h6" sx={{ mb: 2 }}>
            BOM Components
          </Typography>
          <TableContainer component={Paper} variant="outlined">
            <Table>
              <TableHead>
                <TableRow>
                  <TableCell>Seq</TableCell>
                  <TableCell>Component Code</TableCell>
                  <TableCell>Component Name</TableCell>
                  <TableCell align="right">Quantity</TableCell>
                  <TableCell>UoM</TableCell>
                  <TableCell align="right">Scrap %</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {!bom.lines || bom.lines.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} align="center">
                      No components defined
                    </TableCell>
                  </TableRow>
                ) : (
                  bom.lines.map((line) => (
                    <TableRow key={line.id}>
                      <TableCell>{line.sequenceNumber}</TableCell>
                      <TableCell>{line.componentItem?.code || '-'}</TableCell>
                      <TableCell>{line.componentItem?.name || '-'}</TableCell>
                      <TableCell align="right">{line.quantity}</TableCell>
                      <TableCell>{line.uoM?.code || '-'}</TableCell>
                      <TableCell align="right">{line.scrapFactor || 0}%</TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </TableContainer>
        </CardContent>
      </Card>
    </Box>
  );
};

export default BOMDetail;
