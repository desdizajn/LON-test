import React, { useEffect, useState } from 'react';
import {
  Box,
  Button,
  Typography,
  Chip,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import DataTable, { Column } from '../../../components/common/DataTable';
import LoadingSpinner from '../../../components/common/LoadingSpinner';
import ErrorDisplay from '../../../components/common/ErrorDisplay';
import ConfirmDialog from '../../../components/common/ConfirmDialog';
import ItemForm from './ItemForm';
import { itemsApi } from '../../../services/masterDataApi';
import { useMasterDataStore } from '../../../store/useMasterDataStore';
import { showSuccess, showError } from '../../../utils/toast';
import type { Item, ItemType } from '../../../types/masterData';

const ItemsList: React.FC = () => {
  const { items, setItems, removeItem } = useMasterDataStore();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [selectedItem, setSelectedItem] = useState<Item | null>(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [itemToDelete, setItemToDelete] = useState<Item | null>(null);

  const loadItems = async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await itemsApi.getAll();
      setItems(response.data);
    } catch (err: any) {
      setError(err);
      showError('Failed to load items');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadItems();
  }, []);

  const handleEdit = (item: Item) => {
    setSelectedItem(item);
    setFormOpen(true);
  };

  const handleDelete = (item: Item) => {
    setItemToDelete(item);
    setDeleteDialogOpen(true);
  };

  const confirmDelete = async () => {
    if (!itemToDelete) return;
    try {
      await itemsApi.delete(itemToDelete.id);
      removeItem(itemToDelete.id);
      showSuccess('Item deleted successfully');
    } catch (err: any) {
      showError('Failed to delete item');
    } finally {
      setDeleteDialogOpen(false);
      setItemToDelete(null);
    }
  };

  const handleFormClose = () => {
    setFormOpen(false);
    setSelectedItem(null);
  };

  const handleFormSuccess = () => {
    loadItems();
    handleFormClose();
  };

  const getItemTypeLabel = (type: ItemType): string => {
    const labels: Record<ItemType, string> = {
      1: 'Raw Material',
      2: 'Semi-Finished',
      3: 'Finished Good',
      4: 'Packaging',
    };
    return labels[type] || 'Unknown';
  };

  const getItemTypeColor = (type: ItemType): 'default' | 'primary' | 'secondary' | 'success' | 'error' | 'info' | 'warning' => {
    const colors: Record<ItemType, any> = {
      1: 'default',
      2: 'info',
      3: 'success',
      4: 'warning',
    };
    return colors[type] || 'default';
  };

  const columns: Column<Item>[] = [
    { id: 'code', label: 'Code', minWidth: 120 },
    { id: 'name', label: 'Name', minWidth: 200 },
    {
      id: 'itemType',
      label: 'Type',
      minWidth: 150,
      format: (value: ItemType) => (
        <Chip
          label={getItemTypeLabel(value)}
          color={getItemTypeColor(value)}
          size="small"
        />
      ),
    },
    {
      id: 'uoM',
      label: 'UoM',
      minWidth: 100,
      format: (value) => value?.code || '-',
    },
    {
      id: 'isBatchRequired',
      label: 'Batch Required',
      minWidth: 120,
      align: 'center',
      format: (value: boolean) => (
        <Chip label={value ? 'Yes' : 'No'} color={value ? 'success' : 'default'} size="small" />
      ),
    },
    {
      id: 'isMRNRequired',
      label: 'MRN Required',
      minWidth: 120,
      align: 'center',
      format: (value: boolean) => (
        <Chip label={value ? 'Yes' : 'No'} color={value ? 'success' : 'default'} size="small" />
      ),
    },
    { id: 'hsCode', label: 'HS Code', minWidth: 120 },
    {
      id: 'isActive',
      label: 'Status',
      minWidth: 100,
      align: 'center',
      format: (value: boolean) => (
        <Chip label={value ? 'Active' : 'Inactive'} color={value ? 'success' : 'default'} size="small" />
      ),
    },
  ];

  if (loading) return <LoadingSpinner message="Loading items..." />;
  if (error) return <ErrorDisplay error={error} onRetry={loadItems} fullPage />;

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={3}>
        <Typography variant="h4">Items</Typography>
        <Button
          variant="contained"
          color="primary"
          startIcon={<AddIcon />}
          onClick={() => setFormOpen(true)}
        >
          New Item
        </Button>
      </Box>

      <DataTable
        columns={columns}
        data={items}
        onEdit={handleEdit}
        onDelete={handleDelete}
        searchPlaceholder="Search items by code, name, HS code..."
        emptyMessage="No items found. Click 'New Item' to create one."
      />

      {formOpen && (
        <ItemForm
          open={formOpen}
          onClose={handleFormClose}
          onSuccess={handleFormSuccess}
          item={selectedItem}
        />
      )}

      <ConfirmDialog
        open={deleteDialogOpen}
        onClose={() => setDeleteDialogOpen(false)}
        onConfirm={confirmDelete}
        title="Delete Item"
        message={`Are you sure you want to delete item "${itemToDelete?.name}"? This action cannot be undone.`}
        confirmText="Delete"
        confirmColor="error"
      />
    </Box>
  );
};

export default ItemsList;
