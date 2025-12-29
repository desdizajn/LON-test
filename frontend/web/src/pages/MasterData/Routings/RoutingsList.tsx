import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Box, Button, Chip, Typography } from '@mui/material';
import { Add } from '@mui/icons-material';
import DataTable from '../../../components/common/DataTable';
import ConfirmDialog from '../../../components/common/ConfirmDialog';
import RoutingForm from './RoutingForm';
import { routingsApi } from '../../../services/masterDataApi';
import { showSuccess, showError } from '../../../utils/toast';
import type { Routing } from '../../../types/masterData';

const RoutingsList: React.FC = () => {
  const navigate = useNavigate();
  const [routings, setRoutings] = useState<Routing[]>([]);
  const [loading, setLoading] = useState(true);
  const [formOpen, setFormOpen] = useState(false);
  const [selectedRouting, setSelectedRouting] = useState<Routing | null>(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [routingToDelete, setRoutingToDelete] = useState<Routing | null>(null);

  useEffect(() => {
    loadRoutings();
  }, []);

  const loadRoutings = async () => {
    try {
      setLoading(true);
      const response = await routingsApi.getAll();
      setRoutings(response.data);
    } catch (err: any) {
      showError(err.response?.data?.message || 'Failed to load routings');
    } finally {
      setLoading(false);
    }
  };

  const handleAdd = () => {
    setSelectedRouting(null);
    setFormOpen(true);
  };

  const handleEdit = (routing: Routing) => {
    setSelectedRouting(routing);
    setFormOpen(true);
  };

  const handleFormClose = () => {
    setFormOpen(false);
    setSelectedRouting(null);
  };

  const handleFormSuccess = () => {
    loadRoutings();
    handleFormClose();
  };

  const handleDelete = (routing: Routing) => {
    setRoutingToDelete(routing);
    setDeleteDialogOpen(true);
  };

  const confirmDelete = async () => {
    if (!routingToDelete) return;
    try {
      await routingsApi.delete(routingToDelete.id);
      setRoutings(routings.filter((r) => r.id !== routingToDelete.id));
      showSuccess('Routing deleted successfully');
      setDeleteDialogOpen(false);
      setRoutingToDelete(null);
    } catch (err: any) {
      showError(err.response?.data?.message || 'Failed to delete routing');
    }
  };

  const columns = [
    {
      id: 'itemCode',
      label: 'Item Code',
      sortable: true,
      render: (row: Routing) => row.item?.code || '-',
    },
    {
      id: 'itemName',
      label: 'Item Name',
      sortable: true,
      render: (row: Routing) => row.item?.name || '-',
    },
    {
      id: 'version',
      label: 'Version',
      sortable: true,
      render: (row: Routing) => row.version,
    },
    {
      id: 'operations',
      label: 'Operations',
      render: (row: Routing) => row.operations?.length || 0,
    },
    {
      id: 'description',
      label: 'Description',
      render: (row: Routing) => row.description || '-',
    },
    {
      id: 'isActive',
      label: 'Status',
      sortable: true,
      render: (row: Routing) => (
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
        <Typography variant="h5">Routings</Typography>
        <Button variant="contained" startIcon={<Add />} onClick={handleAdd}>
          Add Routing
        </Button>
      </Box>

      <DataTable
        columns={columns}
        data={routings}
        loading={loading}
        onEdit={handleEdit}
        onDelete={handleDelete}
        onView={(routing) => navigate(`/master-data/routings/${routing.id}`)}
        searchPlaceholder="Search routings by item code, item name..."
      />

      <RoutingForm
        open={formOpen}
        onClose={handleFormClose}
        onSuccess={handleFormSuccess}
        routing={selectedRouting}
      />

      <ConfirmDialog
        open={deleteDialogOpen}
        onClose={() => setDeleteDialogOpen(false)}
        onConfirm={confirmDelete}
        title="Delete Routing"
        message={`Are you sure you want to delete routing for "${routingToDelete?.item?.name}"?`}
      />
    </Box>
  );
};

export default RoutingsList;
