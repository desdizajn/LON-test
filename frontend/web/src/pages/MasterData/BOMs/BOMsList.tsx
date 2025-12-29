import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Box, Button, Chip, Typography } from '@mui/material';
import { Add } from '@mui/icons-material';
import DataTable from '../../../components/common/DataTable';
import ConfirmDialog from '../../../components/common/ConfirmDialog';
import BOMForm from './BOMForm';
import { bomsApi } from '../../../services/masterDataApi';
import { showSuccess, showError } from '../../../utils/toast';
import type { BOM } from '../../../types/masterData';

const BOMsList: React.FC = () => {
  const navigate = useNavigate();
  const [boms, setBoms] = useState<BOM[]>([]);
  const [loading, setLoading] = useState(true);
  const [formOpen, setFormOpen] = useState(false);
  const [selectedBOM, setSelectedBOM] = useState<BOM | null>(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [bomToDelete, setBOMToDelete] = useState<BOM | null>(null);

  useEffect(() => {
    loadBOMs();
  }, []);

  const loadBOMs = async () => {
    try {
      setLoading(true);
      const response = await bomsApi.getAll();
      setBoms(response.data);
    } catch (err: any) {
      showError(err.response?.data?.message || 'Failed to load BOMs');
    } finally {
      setLoading(false);
    }
  };

  const handleAdd = () => {
    setSelectedBOM(null);
    setFormOpen(true);
  };

  const handleEdit = (bom: BOM) => {
    setSelectedBOM(bom);
    setFormOpen(true);
  };

  const handleFormClose = () => {
    setFormOpen(false);
    setSelectedBOM(null);
  };

  const handleFormSuccess = () => {
    loadBOMs();
    handleFormClose();
  };

  const handleDelete = (bom: BOM) => {
    setBOMToDelete(bom);
    setDeleteDialogOpen(true);
  };

  const confirmDelete = async () => {
    if (!bomToDelete) return;
    try {
      await bomsApi.delete(bomToDelete.id);
      setBoms(boms.filter((b) => b.id !== bomToDelete.id));
      showSuccess('BOM deleted successfully');
      setDeleteDialogOpen(false);
      setBOMToDelete(null);
    } catch (err: any) {
      showError(err.response?.data?.message || 'Failed to delete BOM');
    }
  };

  const columns = [
    {
      id: 'itemCode',
      label: 'Item Code',
      sortable: true,
      render: (row: BOM) => row.item?.code || '-',
    },
    {
      id: 'itemName',
      label: 'Item Name',
      sortable: true,
      render: (row: BOM) => row.item?.name || '-',
    },
    {
      id: 'version',
      label: 'Version',
      sortable: true,
      render: (row: BOM) => row.version,
    },
    {
      id: 'quantity',
      label: 'Quantity',
      render: (row: BOM) => row.quantity,
    },
    {
      id: 'validFrom',
      label: 'Valid From',
      render: (row: BOM) => row.validFrom ? new Date(row.validFrom).toLocaleDateString() : '-',
    },
    {
      id: 'validTo',
      label: 'Valid To',
      render: (row: BOM) => row.validTo ? new Date(row.validTo).toLocaleDateString() : '-',
    },
    {
      id: 'isActive',
      label: 'Status',
      sortable: true,
      render: (row: BOM) => (
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
        <Typography variant="h5">Bills of Materials</Typography>
        <Button variant="contained" startIcon={<Add />} onClick={handleAdd}>
          Add BOM
        </Button>
      </Box>

      <DataTable
        columns={columns}
        data={boms}
        loading={loading}
        onEdit={handleEdit}
        onDelete={handleDelete}
        onView={(bom) => navigate(`/master-data/boms/${bom.id}`)}
        searchPlaceholder="Search BOMs by item code, item name..."
      />

      <BOMForm
        open={formOpen}
        onClose={handleFormClose}
        onSuccess={handleFormSuccess}
        bom={selectedBOM}
      />

      <ConfirmDialog
        open={deleteDialogOpen}
        onClose={() => setDeleteDialogOpen(false)}
        onConfirm={confirmDelete}
        title="Delete BOM"
        message={`Are you sure you want to delete BOM for "${bomToDelete?.item?.name}"?`}
      />
    </Box>
  );
};

export default BOMsList;
