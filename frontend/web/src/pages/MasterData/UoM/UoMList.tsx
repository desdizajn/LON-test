import React, { useEffect, useState } from 'react';
import { Box, Button, Chip, Typography } from '@mui/material';
import { Add } from '@mui/icons-material';
import DataTable from '../../../components/common/DataTable';
import ConfirmDialog from '../../../components/common/ConfirmDialog';
import UoMForm from './UoMForm';
import { uomApi } from '../../../services/masterDataApi';
import { useMasterDataStore } from '../../../store/useMasterDataStore';
import { showSuccess, showError } from '../../../utils/toast';
import type { UoM } from '../../../types/masterData';

const UoMList: React.FC = () => {
  const { uoms, setUoMs, removeUoM } = useMasterDataStore();
  const [loading, setLoading] = useState(true);
  const [formOpen, setFormOpen] = useState(false);
  const [selectedUoM, setSelectedUoM] = useState<UoM | null>(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [uomToDelete, setUoMToDelete] = useState<UoM | null>(null);

  useEffect(() => {
    loadUoMs();
  }, []);

  const loadUoMs = async () => {
    try {
      setLoading(true);
      const response = await uomApi.getAll();
      setUoMs(response.data);
    } catch (err: any) {
      showError(err.response?.data?.message || 'Failed to load units of measure');
    } finally {
      setLoading(false);
    }
  };

  const handleAdd = () => {
    setSelectedUoM(null);
    setFormOpen(true);
  };

  const handleEdit = (uom: UoM) => {
    setSelectedUoM(uom);
    setFormOpen(true);
  };

  const handleFormClose = () => {
    setFormOpen(false);
    setSelectedUoM(null);
  };

  const handleFormSuccess = () => {
    loadUoMs();
    handleFormClose();
  };

  const handleDelete = (uom: UoM) => {
    setUoMToDelete(uom);
    setDeleteDialogOpen(true);
  };

  const confirmDelete = async () => {
    if (!uomToDelete) return;
    try {
      await uomApi.delete(uomToDelete.id);
      removeUoM(uomToDelete.id);
      showSuccess('Unit of measure deleted successfully');
      setDeleteDialogOpen(false);
      setUoMToDelete(null);
    } catch (err: any) {
      showError(err.response?.data?.message || 'Failed to delete unit of measure');
    }
  };

  const columns = [
    {
      id: 'code',
      label: 'Code',
      sortable: true,
      render: (row: UoM) => row.code,
    },
    {
      id: 'name',
      label: 'Name',
      sortable: true,
      render: (row: UoM) => row.name,
    },
    {
      id: 'description',
      label: 'Description',
      render: (row: UoM) => row.description || '-',
    },
    {
      id: 'isActive',
      label: 'Status',
      sortable: true,
      render: (row: UoM) => (
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
        <Typography variant="h5">Units of Measure</Typography>
        <Button variant="contained" startIcon={<Add />} onClick={handleAdd}>
          Add UoM
        </Button>
      </Box>

      <DataTable
        columns={columns}
        data={uoms}
        loading={loading}
        onEdit={handleEdit}
        onDelete={handleDelete}
        searchPlaceholder="Search units by code, name..."
      />

      <UoMForm
        open={formOpen}
        onClose={handleFormClose}
        onSuccess={handleFormSuccess}
        uom={selectedUoM}
      />

      <ConfirmDialog
        open={deleteDialogOpen}
        onClose={() => setDeleteDialogOpen(false)}
        onConfirm={confirmDelete}
        title="Delete Unit of Measure"
        message={`Are you sure you want to delete unit "${uomToDelete?.name}"?`}
      />
    </Box>
  );
};

export default UoMList;
