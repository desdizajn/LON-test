import React, { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { Box, Grid } from '@mui/material';
import FormDialog from '../../../components/common/FormDialog';
import FormInput from '../../../components/forms/FormInput';
import FormCheckbox from '../../../components/forms/FormCheckbox';
import { warehousesApi } from '../../../services/masterDataApi';
import { useMasterDataStore } from '../../../store/useMasterDataStore';
import { showSuccess, showError } from '../../../utils/toast';
import type { Warehouse, WarehouseFormData } from '../../../types/masterData';

interface WarehouseFormProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
  warehouse?: Warehouse | null;
}

const WarehouseForm: React.FC<WarehouseFormProps> = ({ open, onClose, onSuccess, warehouse }) => {
  const { addWarehouse, updateWarehouse } = useMasterDataStore();
  const [submitting, setSubmitting] = useState(false);

  const { control, handleSubmit, reset } = useForm<WarehouseFormData>({
    defaultValues: {
      code: warehouse?.code || '',
      name: warehouse?.name || '',
      description: warehouse?.description || '',
      address: warehouse?.address || '',
      isActive: warehouse?.isActive !== false,
    },
  });

  useEffect(() => {
    if (warehouse) {
      reset({
        code: warehouse.code,
        name: warehouse.name,
        description: warehouse.description || '',
        address: warehouse.address || '',
        isActive: warehouse.isActive,
      });
    }
  }, [warehouse, reset]);

  const onSubmit = async (data: WarehouseFormData) => {
    try {
      setSubmitting(true);
      if (warehouse) {
        const response = await warehousesApi.update(warehouse.id, data);
        updateWarehouse(warehouse.id, response.data);
        showSuccess('Warehouse updated successfully');
      } else {
        const response = await warehousesApi.create(data);
        addWarehouse(response.data);
        showSuccess('Warehouse created successfully');
      }
      onSuccess();
    } catch (err: any) {
      showError(err.response?.data?.message || 'Failed to save warehouse');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={warehouse ? 'Edit Warehouse' : 'New Warehouse'}
      onSubmit={handleSubmit(onSubmit)}
      isSubmitting={submitting}
      maxWidth="sm"
    >
      <Box>
        <Grid container spacing={2}>
          <Grid item xs={12}>
            <FormInput
              name="code"
              control={control}
              label="Warehouse Code"
              rules={{ required: 'Warehouse code is required' }}
            />
          </Grid>
          <Grid item xs={12}>
            <FormInput
              name="name"
              control={control}
              label="Warehouse Name"
              rules={{ required: 'Warehouse name is required' }}
            />
          </Grid>
          <Grid item xs={12}>
            <FormInput
              name="description"
              control={control}
              label="Description"
              multiline
              rows={2}
            />
          </Grid>
          <Grid item xs={12}>
            <FormInput name="address" control={control} label="Address" multiline rows={2} />
          </Grid>
          <Grid item xs={12}>
            <FormCheckbox name="isActive" control={control} label="Active" />
          </Grid>
        </Grid>
      </Box>
    </FormDialog>
  );
};

export default WarehouseForm;
