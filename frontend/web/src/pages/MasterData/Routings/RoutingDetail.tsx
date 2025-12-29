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
import { routingsApi } from '../../../services/masterDataApi';
import LoadingSpinner from '../../../components/common/LoadingSpinner';
import ErrorDisplay from '../../../components/common/ErrorDisplay';
import { showError } from '../../../utils/toast';
import type { Routing } from '../../../types/masterData';

const RoutingDetail: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [routing, setRouting] = useState<Routing | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadRouting();
  }, [id]);

  const loadRouting = async () => {
    if (!id) return;
    try {
      setLoading(true);
      setError(null);
      const response = await routingsApi.getById(id);
      setRouting(response.data);
    } catch (err: any) {
      const errorMsg = err.response?.data?.message || 'Failed to load routing';
      setError(errorMsg);
      showError(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return <LoadingSpinner message="Loading routing details..." />;
  }

  if (error) {
    return (
      <ErrorDisplay
        message={error}
        onRetry={loadRouting}
        actionButton={
          <Button
            variant="outlined"
            startIcon={<ArrowBack />}
            onClick={() => navigate('/master-data/routings')}
          >
            Back to Routings
          </Button>
        }
      />
    );
  }

  if (!routing) {
    return <ErrorDisplay message="Routing not found" />;
  }

  return (
    <Box>
      <Box sx={{ mb: 3, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <Button
            variant="outlined"
            startIcon={<ArrowBack />}
            onClick={() => navigate('/master-data/routings')}
          >
            Back
          </Button>
          <Typography variant="h5">Routing Details</Typography>
        </Box>
        <Button
          variant="contained"
          startIcon={<Edit />}
          onClick={() => navigate(`/master-data/routings/${id}/edit`)}
        >
          Edit
        </Button>
      </Box>

      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Grid container spacing={3}>
            <Grid item xs={12}>
              <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', mb: 2 }}>
                <Typography variant="h6">{routing.item?.name || 'N/A'}</Typography>
                <Chip label={`Version ${routing.version}`} color="primary" size="small" />
                <Chip
                  label={routing.isActive ? 'Active' : 'Inactive'}
                  color={routing.isActive ? 'success' : 'default'}
                  size="small"
                />
              </Box>
              <Divider />
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Item Code
              </Typography>
              <Typography variant="body1">{routing.item?.code || '-'}</Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Item Name
              </Typography>
              <Typography variant="body1">{routing.item?.name || '-'}</Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Version
              </Typography>
              <Typography variant="body1">{routing.version}</Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Number of Operations
              </Typography>
              <Typography variant="body1">{routing.operations?.length || 0}</Typography>
            </Grid>

            {routing.description && (
              <Grid item xs={12}>
                <Typography variant="subtitle2" color="text.secondary">
                  Description
                </Typography>
                <Typography variant="body1">{routing.description}</Typography>
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
                {new Date(routing.createdAt).toLocaleString()}
              </Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Last Modified
              </Typography>
              <Typography variant="body1">
                {routing.updatedAt ? new Date(routing.updatedAt).toLocaleString() : '-'}
              </Typography>
            </Grid>
          </Grid>
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <Typography variant="h6" sx={{ mb: 2 }}>
            Operations
          </Typography>
          <TableContainer component={Paper} variant="outlined">
            <Table>
              <TableHead>
                <TableRow>
                  <TableCell>Op #</TableCell>
                  <TableCell>Operation Name</TableCell>
                  <TableCell>Work Center</TableCell>
                  <TableCell align="right">Setup Time (min)</TableCell>
                  <TableCell align="right">Standard Time (min)</TableCell>
                  <TableCell>Description</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {!routing.operations || routing.operations.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} align="center">
                      No operations defined
                    </TableCell>
                  </TableRow>
                ) : (
                  routing.operations
                    .sort((a, b) => a.operationNumber - b.operationNumber)
                    .map((operation) => (
                      <TableRow key={operation.id}>
                        <TableCell>{operation.operationNumber}</TableCell>
                        <TableCell>{operation.operationName}</TableCell>
                        <TableCell>
                          {operation.workCenter
                            ? `${operation.workCenter.code} - ${operation.workCenter.name}`
                            : '-'}
                        </TableCell>
                        <TableCell align="right">{operation.setupTime}</TableCell>
                        <TableCell align="right">{operation.standardTime}</TableCell>
                        <TableCell>{operation.description || '-'}</TableCell>
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

export default RoutingDetail;
