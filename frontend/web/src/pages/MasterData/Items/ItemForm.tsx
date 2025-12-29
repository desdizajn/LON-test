import React, { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { Box, Grid } from '@mui/material';
import FormDialog from '../../../components/common/FormDialog';
import FormInput from '../../../components/forms/FormInput';
import FormSelect from '../../../components/forms/FormSelect';
import FormCheckbox from '../../../components/forms/FormCheckbox';
import FormAutocomplete from '../../../components/forms/FormAutocomplete';
import { itemsApi, uomApi } from '../../../services/masterDataApi';
import { useMasterDataStore } from '../../../store/useMasterDataStore';
import { showSuccess, showError } from '../../../utils/toast';
import type { Item, ItemFormData, ItemType, UoM } from '../../../types/masterData';

interface ItemFormProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
  item?: Item | null;
}

const ItemForm: React.FC<ItemFormProps> = ({ open, onClose, onSuccess, item }) => {
  const { addItem, updateItem } = useMasterDataStore();
  const [submitting, setSubmitting] = useState(false);
  const [uoms, setUoms] = useState<UoM[]>([]);
  const [loadingUoMs, setLoadingUoMs] = useState(true);

  const { control, handleSubmit, reset } = useForm<ItemFormData>({
    defaultValues: {
      code: item?.code || '',
      name: item?.name || '',
      description: item?.description || '',
      itemType: item?.itemType || 1,
      uoMId: item?.uoMId || '',
      isBatchRequired: item?.isBatchRequired || false,
      isMRNRequired: item?.isMRNRequired || false,
      countryOfOrigin: item?.countryOfOrigin || '',
      hsCode: item?.hsCode || '',
      isActive: item?.isActive !== false,
    },
  });

  useEffect(() => {
    loadUoMs();
  }, []);

  useEffect(() => {
    if (item) {
      reset({
        code: item.code,
        name: item.name,
        description: item.description || '',
        itemType: item.itemType,
        uoMId: item.uoMId,
        isBatchRequired: item.isBatchRequired,
        isMRNRequired: item.isMRNRequired,
        countryOfOrigin: item.countryOfOrigin || '',
        hsCode: item.hsCode || '',
        isActive: item.isActive,
      });
    }
  }, [item, reset]);

  const loadUoMs = async () => {
    try {
      setLoadingUoMs(true);
      const response = await uomApi.getAll();
      setUoms(response.data);
    } catch (err) {
      showError('Failed to load units of measure');
    } finally {
      setLoadingUoMs(false);
    }
  };

  const onSubmit = async (data: ItemFormData) => {
    try {
      setSubmitting(true);
      if (item) {
        const response = await itemsApi.update(item.id, data);
        updateItem(item.id, response.data);
        showSuccess('Item updated successfully');
      } else {
        const response = await itemsApi.create(data);
        addItem(response.data);
        showSuccess('Item created successfully');
      }
      onSuccess();
    } catch (err: any) {
      showError(err.response?.data?.message || 'Failed to save item');
    } finally {
      setSubmitting(false);
    }
  };

  const itemTypeOptions = [
    { value: 1, label: 'Raw Material' },
    { value: 2, label: 'Semi-Finished' },
    { value: 3, label: 'Finished Good' },
    { value: 4, label: 'Packaging' },
  ];

  const uomOptions = uoms.map((uom) => ({
    id: uom.id,
    label: `${uom.code} - ${uom.name}`,
  }));

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={item ? 'Edit Item' : 'New Item'}
      onSubmit={handleSubmit(onSubmit)}
      isSubmitting={submitting}
      maxWidth="md"
    >
      <Box>
        <Grid container spacing={2}>
          <Grid item xs={12} sm={6}>
            <FormInput
              name="code"
              control={control}
              label="Item Code"
              rules={{ required: 'Item code is required' }}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <FormInput
              name="name"
              control={control}
              label="Item Name"
              rules={{ required: 'Item name is required' }}
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
          <Grid item xs={12} sm={6}>
            <FormSelect
              name="itemType"
              control={control}
              label="Item Type"
              options={itemTypeOptions}
              rules={{ required: 'Item type is required' }}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <FormAutocomplete
              name="uoMId"
              control={control}
              label="Unit of Measure"
              options={uomOptions}
              loading={loadingUoMs}
              rules={{ required: 'Unit of measure is required' }}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <FormInput
              name="countryOfOrigin"
              control={control}
              label="Country of Origin"
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <FormInput
              name="hsCode"
              control={control}
              label="HS Code"
              placeholder="e.g. 8471.30.00.00"
            />
          </Grid>
          <Grid item xs={12} sm={4}>
            <FormCheckbox
              name="isBatchRequired"
              control={control}
              label="Batch Required"
            />
          </Grid>
          <Grid item xs={12} sm={4}>
            <FormCheckbox
              name="isMRNRequired"
              control={control}
              label="MRN Required"
            />
          </Grid>
          <Grid item xs={12} sm={4}>
            <FormCheckbox name="isActive" control={control} label="Active" />
          </Grid>
        </Grid>
      </Box>
    </FormDialog>
  );
};

export default ItemForm;
