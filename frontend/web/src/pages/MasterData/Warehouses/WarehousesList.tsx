import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Box, Button, Chip, Typography } from '@mui/material';
import { Add } from '@mui/icons-material';
import DataTable from '../../../components/common/DataTable';
import ConfirmDialog from '../../../components/common/ConfirmDialog';
import WarehouseForm from './WarehouseForm';
import { warehousesApi } from '../../../services/masterDataApi';
import { useMasterDataStore } from '../../../store/useMasterDataStore';
import { showSuccess, showError } from '../../../utils/toast';
import type { Warehouse } from '../../../types/masterData';

const WarehousesList: React.FC = () => {
  const navigate = useNavigate();
  const { warehouses, setWarehouses, removeWarehouse } = useMasterDataStore();
  const [loading, setLoading] = useState(true);
  const [formOpen, setFormOpen] = useState(false);
  const [selectedWarehouse, setSelectedWarehouse] = useState<Warehouse | null>(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [warehouseToDelete, setWarehouseToDelete] = useState<Warehouse | null>(null);

  useEffect(() => {
    loadWarehouses();
  }, []);

  const loadWarehouses = async () => {
    try {
      setLoading(true);
      const response = await warehousesApi.getAll();
      setWarehouses(response.data);
    } catch (err: any) {
      showError(err.response?.data?.message || 'Failed to load warehouses');
    } finally {
      setLoading(false);
    }
  };

  const handleAdd = () => {
    setSelectedWarehouse(null);
    setFormOpen(true);
  };

  const handleEdit = (warehouse: Warehouse) => {
    setSelectedWarehouse(warehouse);
    setFormOpen(true);
  };

  const handleFormClose = () => {
    setFormOpen(false);
    setSelectedWarehouse(null);
  };

  const handleFormSuccess = () => {
    loadWarehouses();
    handleFormClose();
  };

  const handleDelete = (warehouse: Warehouse) => {
    setWarehouseToDelete(warehouse);
    setDeleteDialogOpen(true);
  };

  const confirmDelete = async () => {
    if (!warehouseToDelete) return;
    try {
      await warehousesApi.delete(warehouseToDelete.id);
      removeWarehouse(warehouseToDelete.id);
      showSuccess('Warehouse deleted successfully');
      setDeleteDialogOpen(false);
      setWarehouseToDelete(null);
    } catch (err: any) {
      showError(err.response?.data?.message || 'Failed to delete warehouse');
    }
  };

  const columns = [
    {
      id: 'code',
      label: 'Code',
      sortable: true,
      render: (row: Warehouse) => row.code,
    },
    {
      id: 'name',
      label: 'Name',
      sortable: true,
      render: (row: Warehouse) => row.name,
    },
    {
      id: 'description',
      label: 'Description',
      render: (row: Warehouse) => row.description || '-',
    },
    {
      id: 'address',
      label: 'Address',
      render: (row: Warehouse) => row.address || '-',
    },
    {
      id: 'isActive',
      label: 'Status',
      sortable: true,
      render: (row: Warehouse) => (
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
        <Typography variant="h5">Warehouses</Typography>
        <Button variant="contained" startIcon={<Add />} onClick={handleAdd}>
          Add Warehouse
        </Button>
      </Box>

      <DataTable
        columns={columns}
        data={warehouses}
        loading={loading}
        onEdit={handleEdit}
        onDelete={handleDelete}
        searchPlaceholder="Search warehouses by code, name..."
      />

      <WarehouseForm
        open={formOpen}
        onClose={handleFormClose}
        onSuccess={handleFormSuccess}
        warehouse={selectedWarehouse}
      />

      <ConfirmDialog
        open={deleteDialogOpen}
        onClose={() => setDeleteDialogOpen(false)}
        onConfirm={confirmDelete}
        title="Delete Warehouse"
        message={`Are you sure you want to delete warehouse "${warehouseToDelete?.name}"?`}
      />
    </Box>
  );
};

export default WarehousesList;
