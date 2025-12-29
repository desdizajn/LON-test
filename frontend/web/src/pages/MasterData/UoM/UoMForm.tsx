import React, { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { Box, Grid } from '@mui/material';
import FormDialog from '../../../components/common/FormDialog';
import FormInput from '../../../components/forms/FormInput';
import FormCheckbox from '../../../components/forms/FormCheckbox';
import { uomApi } from '../../../services/masterDataApi';
import { useMasterDataStore } from '../../../store/useMasterDataStore';
import { showSuccess, showError } from '../../../utils/toast';
import type { UoM, UoMFormData } from '../../../types/masterData';

interface UoMFormProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
  uom?: UoM | null;
}

const UoMForm: React.FC<UoMFormProps> = ({ open, onClose, onSuccess, uom }) => {
  const { addUoM, updateUoM } = useMasterDataStore();
  const [submitting, setSubmitting] = useState(false);

  const { control, handleSubmit, reset } = useForm<UoMFormData>({
    defaultValues: {
      code: uom?.code || '',
      name: uom?.name || '',
      description: uom?.description || '',
      isActive: uom?.isActive !== false,
    },
  });

  useEffect(() => {
    if (uom) {
      reset({
        code: uom.code,
        name: uom.name,
        description: uom.description || '',
        isActive: uom.isActive,
      });
    }
  }, [uom, reset]);

  const onSubmit = async (data: UoMFormData) => {
    try {
      setSubmitting(true);
      if (uom) {
        const response = await uomApi.update(uom.id, data);
        updateUoM(uom.id, response.data);
        showSuccess('Unit of measure updated successfully');
      } else {
        const response = await uomApi.create(data);
        addUoM(response.data);
        showSuccess('Unit of measure created successfully');
      }
      onSuccess();
    } catch (err: any) {
      showError(err.response?.data?.message || 'Failed to save unit of measure');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={uom ? 'Edit Unit of Measure' : 'New Unit of Measure'}
      onSubmit={handleSubmit(onSubmit)}
      isSubmitting={submitting}
      maxWidth="xs"
    >
      <Box>
        <Grid container spacing={2}>
          <Grid item xs={12}>
            <FormInput
              name="code"
              control={control}
              label="UoM Code"
              rules={{ required: 'UoM code is required' }}
            />
          </Grid>
          <Grid item xs={12}>
            <FormInput
              name="name"
              control={control}
              label="UoM Name"
              rules={{ required: 'UoM name is required' }}
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
            <FormCheckbox name="isActive" control={control} label="Active" />
          </Grid>
        </Grid>
      </Box>
    </FormDialog>
  );
};

export default UoMForm;
