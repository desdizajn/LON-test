import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Box, Button, Chip, Typography } from '@mui/material';
import { Add } from '@mui/icons-material';
import DataTable from '../../../components/common/DataTable';
import ConfirmDialog from '../../../components/common/ConfirmDialog';
import PartnerForm from './PartnerForm';
import { partnersApi } from '../../../services/masterDataApi';
import { useMasterDataStore } from '../../../store/useMasterDataStore';
import { showSuccess, showError } from '../../../utils/toast';
import type { Partner, PartnerType } from '../../../types/masterData';

const PartnersList: React.FC = () => {
  const navigate = useNavigate();
  const { partners, setPartners, removePartner } = useMasterDataStore();
  const [loading, setLoading] = useState(true);
  const [formOpen, setFormOpen] = useState(false);
  const [selectedPartner, setSelectedPartner] = useState<Partner | null>(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [partnerToDelete, setPartnerToDelete] = useState<Partner | null>(null);

  useEffect(() => {
    loadPartners();
  }, []);

  const loadPartners = async () => {
    try {
      setLoading(true);
      const response = await partnersApi.getAll();
      setPartners(response.data);
    } catch (err: any) {
      showError(err.response?.data?.message || 'Failed to load partners');
    } finally {
      setLoading(false);
    }
  };

  const handleAdd = () => {
    setSelectedPartner(null);
    setFormOpen(true);
  };

  const handleEdit = (partner: Partner) => {
    setSelectedPartner(partner);
    setFormOpen(true);
  };

  const handleFormClose = () => {
    setFormOpen(false);
    setSelectedPartner(null);
  };

  const handleFormSuccess = () => {
    loadPartners();
    handleFormClose();
  };

  const handleDelete = (partner: Partner) => {
    setPartnerToDelete(partner);
    setDeleteDialogOpen(true);
  };

  const confirmDelete = async () => {
    if (!partnerToDelete) return;
    try {
      await partnersApi.delete(partnerToDelete.id);
      removePartner(partnerToDelete.id);
      showSuccess('Partner deleted successfully');
      setDeleteDialogOpen(false);
      setPartnerToDelete(null);
    } catch (err: any) {
      showError(err.response?.data?.message || 'Failed to delete partner');
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

  const columns = [
    {
      id: 'code',
      label: 'Code',
      sortable: true,
      render: (row: Partner) => row.code,
    },
    {
      id: 'name',
      label: 'Name',
      sortable: true,
      render: (row: Partner) => row.name,
    },
    {
      id: 'partnerType',
      label: 'Type',
      sortable: true,
      render: (row: Partner) => (
        <Chip
          label={getPartnerTypeLabel(row.partnerType)}
          color={getPartnerTypeColor(row.partnerType)}
          size="small"
        />
      ),
    },
    {
      id: 'taxNumber',
      label: 'Tax Number',
      render: (row: Partner) => row.taxNumber || '-',
    },
    {
      id: 'eoriNumber',
      label: 'EORI',
      render: (row: Partner) => row.eoriNumber || '-',
    },
    {
      id: 'country',
      label: 'Country',
      render: (row: Partner) => row.country || '-',
    },
    {
      id: 'isActive',
      label: 'Status',
      sortable: true,
      render: (row: Partner) => (
        <Chip
          label={row.isActive ? 'Active' : 'Inactive'}
          color={row.isActive ? 'success' : 'default'}
          size="small"
        />
      ),
    },
  ];

  return (
    <Box>
      <Box sx={{ mb: 3, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h5">Partners</Typography>
        <Button variant="contained" startIcon={<Add />} onClick={handleAdd}>
          Add Partner
        </Button>
      </Box>

      <DataTable
        columns={columns}
        data={partners}
        loading={loading}
        onEdit={handleEdit}
        onDelete={handleDelete}
        onView={(partner) => navigate(`/master-data/partners/${partner.id}`)}
        searchPlaceholder="Search partners by code, name, tax number..."
      />

      <PartnerForm
        open={formOpen}
        onClose={handleFormClose}
        onSuccess={handleFormSuccess}
        partner={selectedPartner}
      />

      <ConfirmDialog
        open={deleteDialogOpen}
        onClose={() => setDeleteDialogOpen(false)}
        onConfirm={confirmDelete}
        title="Delete Partner"
        message={`Are you sure you want to delete partner "${partnerToDelete?.name}"?`}
      />
    </Box>
  );
};

export default PartnersList;
