import React, { useEffect, useState } from 'react';
import { useForm, useFieldArray } from 'react-hook-form';
import {
  Box,
  Grid,
  Button,
  IconButton,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Typography,
} from '@mui/material';
import { Add, Delete } from '@mui/icons-material';
import FormDialog from '../../../components/common/FormDialog';
import FormInput from '../../../components/forms/FormInput';
import FormCheckbox from '../../../components/forms/FormCheckbox';
import FormAutocomplete from '../../../components/forms/FormAutocomplete';
import { routingsApi, itemsApi, workCentersApi } from '../../../services/masterDataApi';
import { showSuccess, showError } from '../../../utils/toast';
import type { Routing, RoutingFormData, Item, WorkCenter } from '../../../types/masterData';

interface RoutingFormProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
  routing?: Routing | null;
}

const RoutingForm: React.FC<RoutingFormProps> = ({ open, onClose, onSuccess, routing }) => {
  const [submitting, setSubmitting] = useState(false);
  const [items, setItems] = useState<Item[]>([]);
  const [workCenters, setWorkCenters] = useState<WorkCenter[]>([]);
  const [loadingItems, setLoadingItems] = useState(true);
  const [loadingWorkCenters, setLoadingWorkCenters] = useState(true);

  const { control, handleSubmit, reset } = useForm<RoutingFormData>({
    defaultValues: {
      itemId: routing?.itemId || '',
      version: routing?.version || '1.0',
      description: routing?.description || '',
      isActive: routing?.isActive !== false,
      operations: routing?.operations || [],
    },
  });

  const { fields, append, remove } = useFieldArray({
    control,
    name: 'operations',
  });

  useEffect(() => {
    loadItems();
    loadWorkCenters();
  }, []);

  useEffect(() => {
    if (routing) {
      reset({
        itemId: routing.itemId,
        version: routing.version,
        description: routing.description || '',
        isActive: routing.isActive,
        operations: routing.operations || [],
      });
    }
  }, [routing, reset]);

  const loadItems = async () => {
    try {
      setLoadingItems(true);
      const response = await itemsApi.getAll();
      setItems(response.data);
    } catch (err) {
      showError('Failed to load items');
    } finally {
      setLoadingItems(false);
    }
  };

  const loadWorkCenters = async () => {
    try {
      setLoadingWorkCenters(true);
      const response = await workCentersApi.getAll();
      setWorkCenters(response.data);
    } catch (err) {
      showError('Failed to load work centers');
    } finally {
      setLoadingWorkCenters(false);
    }
  };

  const onSubmit = async (data: RoutingFormData) => {
    try {
      setSubmitting(true);
      if (routing) {
        await routingsApi.update(routing.id, data);
        showSuccess('Routing updated successfully');
      } else {
        await routingsApi.create(data);
        showSuccess('Routing created successfully');
      }
      onSuccess();
    } catch (err: any) {
      showError(err.response?.data?.message || 'Failed to save routing');
    } finally {
      setSubmitting(false);
    }
  };

  const addOperation = () => {
    append({
      operationNumber: fields.length + 10,
      workCenterId: '',
      operationName: '',
      standardTime: 0,
      setupTime: 0,
      description: '',
    });
  };

  const itemOptions = items.map((item) => ({
    id: item.id,
    label: `${item.code} - ${item.name}`,
  }));

  const workCenterOptions = workCenters.map((wc) => ({
    id: wc.id,
    label: `${wc.code} - ${wc.name}`,
  }));

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={routing ? 'Edit Routing' : 'New Routing'}
      onSubmit={handleSubmit(onSubmit)}
      isSubmitting={submitting}
      maxWidth="lg"
    >
      <Box>
        <Grid container spacing={2}>
          <Grid item xs={12} sm={6}>
            <FormAutocomplete
              name="itemId"
              control={control}
              label="Item"
              options={itemOptions}
              loading={loadingItems}
              rules={{ required: 'Item is required' }}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <FormInput
              name="version"
              control={control}
              label="Version"
              rules={{ required: 'Version is required' }}
            />
          </Grid>
          <Grid item xs={12} sm={9}>
            <FormInput name="description" control={control} label="Description" multiline rows={2} />
          </Grid>
          <Grid item xs={12} sm={3}>
            <FormCheckbox name="isActive" control={control} label="Active" />
          </Grid>

          <Grid item xs={12}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mt: 2, mb: 1 }}>
              <Typography variant="h6">Operations</Typography>
              <Button variant="outlined" startIcon={<Add />} onClick={addOperation} size="small">
                Add Operation
              </Button>
            </Box>
            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Op #</TableCell>
                    <TableCell>Operation Name</TableCell>
                    <TableCell>Work Center</TableCell>
                    <TableCell>Setup Time (min)</TableCell>
                    <TableCell>Standard Time (min)</TableCell>
                    <TableCell>Description</TableCell>
                    <TableCell width="80">Actions</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {fields.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={7} align="center">
                        No operations added
                      </TableCell>
                    </TableRow>
                  ) : (
                    fields.map((field, index) => (
                      <TableRow key={field.id}>
                        <TableCell>
                          <FormInput
                            name={`operations.${index}.operationNumber`}
                            control={control}
                            label=""
                            type="number"
                            rules={{ required: true, min: 1 }}
                          />
                        </TableCell>
                        <TableCell>
                          <FormInput
                            name={`operations.${index}.operationName`}
                            control={control}
                            label=""
                            rules={{ required: true }}
                          />
                        </TableCell>
                        <TableCell>
                          <FormAutocomplete
                            name={`operations.${index}.workCenterId`}
                            control={control}
                            label=""
                            options={workCenterOptions}
                            loading={loadingWorkCenters}
                            rules={{ required: true }}
                          />
                        </TableCell>
                        <TableCell>
                          <FormInput
                            name={`operations.${index}.setupTime`}
                            control={control}
                            label=""
                            type="number"
                            rules={{ required: true, min: 0 }}
                          />
                        </TableCell>
                        <TableCell>
                          <FormInput
                            name={`operations.${index}.standardTime`}
                            control={control}
                            label=""
                            type="number"
                            rules={{ required: true, min: 0 }}
                          />
                        </TableCell>
                        <TableCell>
                          <FormInput
                            name={`operations.${index}.description`}
                            control={control}
                            label=""
                          />
                        </TableCell>
                        <TableCell>
                          <IconButton size="small" color="error" onClick={() => remove(index)}>
                            <Delete fontSize="small" />
                          </IconButton>
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          </Grid>
        </Grid>
      </Box>
    </FormDialog>
  );
};

export default RoutingForm;
