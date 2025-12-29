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
} from '@mui/material';
import { ArrowBack, Edit } from '@mui/icons-material';
import { itemsApi } from '../../../services/masterDataApi';
import LoadingSpinner from '../../../components/common/LoadingSpinner';
import ErrorDisplay from '../../../components/common/ErrorDisplay';
import { showError } from '../../../utils/toast';
import type { Item } from '../../../types/masterData';

const ItemDetail: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [item, setItem] = useState<Item | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadItem();
  }, [id]);

  const loadItem = async () => {
    if (!id) return;
    try {
      setLoading(true);
      setError(null);
      const response = await itemsApi.getById(id);
      setItem(response.data);
    } catch (err: any) {
      const errorMsg = err.response?.data?.message || 'Failed to load item';
      setError(errorMsg);
      showError(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  const getItemTypeLabel = (type: number): string => {
    const types: Record<number, string> = {
      1: 'Raw Material',
      2: 'Semi-Finished',
      3: 'Finished Good',
      4: 'Packaging',
    };
    return types[type] || 'Unknown';
  };

  const getItemTypeColor = (type: number) => {
    const colors: Record<number, 'default' | 'info' | 'success' | 'warning'> = {
      1: 'default',
      2: 'info',
      3: 'success',
      4: 'warning',
    };
    return colors[type] || 'default';
  };

  if (loading) {
    return <LoadingSpinner message="Loading item details..." />;
  }

  if (error) {
    return (
      <ErrorDisplay
        message={error}
        onRetry={loadItem}
        actionButton={
          <Button
            variant="outlined"
            startIcon={<ArrowBack />}
            onClick={() => navigate('/master-data/items')}
          >
            Back to Items
          </Button>
        }
      />
    );
  }

  if (!item) {
    return <ErrorDisplay message="Item not found" />;
  }

  return (
    <Box>
      <Box sx={{ mb: 3, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <Button
            variant="outlined"
            startIcon={<ArrowBack />}
            onClick={() => navigate('/master-data/items')}
          >
            Back
          </Button>
          <Typography variant="h5">Item Details</Typography>
        </Box>
        <Button
          variant="contained"
          startIcon={<Edit />}
          onClick={() => navigate(`/master-data/items/${id}/edit`)}
        >
          Edit
        </Button>
      </Box>

      <Card>
        <CardContent>
          <Grid container spacing={3}>
            <Grid item xs={12}>
              <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', mb: 2 }}>
                <Typography variant="h6">{item.name}</Typography>
                <Chip
                  label={getItemTypeLabel(item.itemType)}
                  color={getItemTypeColor(item.itemType)}
                  size="small"
                />
                <Chip
                  label={item.isActive ? 'Active' : 'Inactive'}
                  color={item.isActive ? 'success' : 'default'}
                  size="small"
                />
              </Box>
              <Divider />
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Item Code
              </Typography>
              <Typography variant="body1">{item.code}</Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Item Type
              </Typography>
              <Typography variant="body1">{getItemTypeLabel(item.itemType)}</Typography>
            </Grid>

            <Grid item xs={12}>
              <Typography variant="subtitle2" color="text.secondary">
                Description
              </Typography>
              <Typography variant="body1">{item.description || '-'}</Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Unit of Measure
              </Typography>
              <Typography variant="body1">{item.uoM?.code || '-'}</Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                HS Code
              </Typography>
              <Typography variant="body1">{item.hsCode || '-'}</Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Country of Origin
              </Typography>
              <Typography variant="body1">{item.countryOfOrigin || '-'}</Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Requirements
              </Typography>
              <Box sx={{ display: 'flex', gap: 1 }}>
                {item.isBatchRequired && (
                  <Chip label="Batch Required" color="primary" size="small" />
                )}
                {item.isMRNRequired && (
                  <Chip label="MRN Required" color="secondary" size="small" />
                )}
                {!item.isBatchRequired && !item.isMRNRequired && '-'}
              </Box>
            </Grid>

            <Grid item xs={12}>
              <Divider sx={{ my: 1 }} />
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Created At
              </Typography>
              <Typography variant="body1">
                {new Date(item.createdAt).toLocaleString()}
              </Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Last Modified
              </Typography>
              <Typography variant="body1">
                {item.updatedAt ? new Date(item.updatedAt).toLocaleString() : '-'}
              </Typography>
            </Grid>
          </Grid>
        </CardContent>
      </Card>
    </Box>
  );
};

export default ItemDetail;
