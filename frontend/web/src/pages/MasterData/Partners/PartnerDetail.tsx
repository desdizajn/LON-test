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
import { partnersApi } from '../../../services/masterDataApi';
import LoadingSpinner from '../../../components/common/LoadingSpinner';
import ErrorDisplay from '../../../components/common/ErrorDisplay';
import { showError } from '../../../utils/toast';
import type { Partner, PartnerType } from '../../../types/masterData';

const PartnerDetail: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [partner, setPartner] = useState<Partner | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadPartner();
  }, [id]);

  const loadPartner = async () => {
    if (!id) return;
    try {
      setLoading(true);
      setError(null);
      const response = await partnersApi.getById(id);
      setPartner(response.data);
    } catch (err: any) {
      const errorMsg = err.response?.data?.message || 'Failed to load partner';
      setError(errorMsg);
      showError(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  const getPartnerTypeLabel = (type: PartnerType): string => {
    const types: Record<PartnerType, string> = {
      1: 'Supplier',
      2: 'Customer',
      3: 'Carrier',
      4: 'Bank',
    };
    return types[type] || 'Unknown';
  };

  const getPartnerTypeColor = (type: PartnerType) => {
    const colors: Record<PartnerType, 'primary' | 'secondary' | 'info' | 'warning'> = {
      1: 'primary',
      2: 'secondary',
      3: 'info',
      4: 'warning',
    };
    return colors[type] || 'primary';
  };

  if (loading) {
    return <LoadingSpinner message="Loading partner details..." />;
  }

  if (error) {
    return (
      <ErrorDisplay
        message={error}
        onRetry={loadPartner}
        actionButton={
          <Button
            variant="outlined"
            startIcon={<ArrowBack />}
            onClick={() => navigate('/master-data/partners')}
          >
            Back to Partners
          </Button>
        }
      />
    );
  }

  if (!partner) {
    return <ErrorDisplay message="Partner not found" />;
  }

  return (
    <Box>
      <Box sx={{ mb: 3, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <Button
            variant="outlined"
            startIcon={<ArrowBack />}
            onClick={() => navigate('/master-data/partners')}
          >
            Back
          </Button>
          <Typography variant="h5">Partner Details</Typography>
        </Box>
        <Button
          variant="contained"
          startIcon={<Edit />}
          onClick={() => navigate(`/master-data/partners/${id}/edit`)}
        >
          Edit
        </Button>
      </Box>

      <Card>
        <CardContent>
          <Grid container spacing={3}>
            <Grid item xs={12}>
              <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', mb: 2 }}>
                <Typography variant="h6">{partner.name}</Typography>
                <Chip
                  label={getPartnerTypeLabel(partner.partnerType)}
                  color={getPartnerTypeColor(partner.partnerType)}
                  size="small"
                />
                <Chip
                  label={partner.isActive ? 'Active' : 'Inactive'}
                  color={partner.isActive ? 'success' : 'default'}
                  size="small"
                />
              </Box>
              <Divider />
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Partner Code
              </Typography>
              <Typography variant="body1">{partner.code}</Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Partner Type
              </Typography>
              <Typography variant="body1">{getPartnerTypeLabel(partner.partnerType)}</Typography>
            </Grid>

            <Grid item xs={12}>
              <Divider sx={{ my: 1 }} />
              <Typography variant="subtitle1" sx={{ fontWeight: 600, mt: 2, mb: 2 }}>
                Tax Information
              </Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography variant="subtitle2" color="text.secondary">
                Tax Number
              </Typography>
              <Typography variant="body1">{partner.taxNumber || '-'}</Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography variant="subtitle2" color="text.secondary">
                VAT Number
              </Typography>
              <Typography variant="body1">{partner.vatNumber || '-'}</Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography variant="subtitle2" color="text.secondary">
                EORI Number
              </Typography>
              <Typography variant="body1">{partner.eoriNumber || '-'}</Typography>
            </Grid>

            <Grid item xs={12}>
              <Divider sx={{ my: 1 }} />
              <Typography variant="subtitle1" sx={{ fontWeight: 600, mt: 2, mb: 2 }}>
                Address
              </Typography>
            </Grid>

            <Grid item xs={12}>
              <Typography variant="subtitle2" color="text.secondary">
                Street Address
              </Typography>
              <Typography variant="body1">{partner.address || '-'}</Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography variant="subtitle2" color="text.secondary">
                City
              </Typography>
              <Typography variant="body1">{partner.city || '-'}</Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography variant="subtitle2" color="text.secondary">
                Postal Code
              </Typography>
              <Typography variant="body1">{partner.postalCode || '-'}</Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography variant="subtitle2" color="text.secondary">
                Country
              </Typography>
              <Typography variant="body1">{partner.country || '-'}</Typography>
            </Grid>

            <Grid item xs={12}>
              <Divider sx={{ my: 1 }} />
              <Typography variant="subtitle1" sx={{ fontWeight: 600, mt: 2, mb: 2 }}>
                Contact Information
              </Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography variant="subtitle2" color="text.secondary">
                Contact Person
              </Typography>
              <Typography variant="body1">{partner.contactPerson || '-'}</Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography variant="subtitle2" color="text.secondary">
                Phone
              </Typography>
              <Typography variant="body1">{partner.phone || '-'}</Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography variant="subtitle2" color="text.secondary">
                Email
              </Typography>
              <Typography variant="body1">{partner.email || '-'}</Typography>
            </Grid>

            <Grid item xs={12}>
              <Divider sx={{ my: 1 }} />
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Created At
              </Typography>
              <Typography variant="body1">
                {new Date(partner.createdAt).toLocaleString()}
              </Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography variant="subtitle2" color="text.secondary">
                Last Modified
              </Typography>
              <Typography variant="body1">
                {partner.updatedAt ? new Date(partner.updatedAt).toLocaleString() : '-'}
              </Typography>
            </Grid>
          </Grid>
        </CardContent>
      </Card>
    </Box>
  );
};

export default PartnerDetail;
