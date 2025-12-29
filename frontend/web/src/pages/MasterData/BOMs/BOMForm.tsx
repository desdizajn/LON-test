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
import { bomsApi, itemsApi, uomApi } from '../../../services/masterDataApi';
import { showSuccess, showError } from '../../../utils/toast';
import type { BOM, BOMFormData, BOMLineFormData, Item, UoM } from '../../../types/masterData';

interface BOMFormProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
  bom?: BOM | null;
}

const BOMForm: React.FC<BOMFormProps> = ({ open, onClose, onSuccess, bom }) => {
  const [submitting, setSubmitting] = useState(false);
  const [items, setItems] = useState<Item[]>([]);
  const [uoms, setUoms] = useState<UoM[]>([]);
  const [loadingItems, setLoadingItems] = useState(true);
  const [loadingUoMs, setLoadingUoMs] = useState(true);

  const { control, handleSubmit, reset, watch } = useForm<BOMFormData>({
    defaultValues: {
      itemId: bom?.itemId || '',
      version: bom?.version || '1.0',
      quantity: bom?.quantity || 1,
      uoMId: bom?.uoMId || '',
      validFrom: bom?.validFrom || '',
      validTo: bom?.validTo || '',
      notes: bom?.notes || '',
      isActive: bom?.isActive !== false,
      lines: bom?.lines || [],
    },
  });

  const { fields, append, remove } = useFieldArray({
    control,
    name: 'lines',
  });

  useEffect(() => {
    loadItems();
    loadUoMs();
  }, []);

  useEffect(() => {
    if (bom) {
      reset({
        itemId: bom.itemId,
        version: bom.version,
        quantity: bom.quantity,
        uoMId: bom.uoMId,
        validFrom: bom.validFrom || '',
        validTo: bom.validTo || '',
        notes: bom.notes || '',
        isActive: bom.isActive,
        lines: bom.lines || [],
      });
    }
  }, [bom, reset]);

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

  const onSubmit = async (data: BOMFormData) => {
    try {
      setSubmitting(true);
      if (bom) {
        await bomsApi.update(bom.id, data);
        showSuccess('BOM updated successfully');
      } else {
        await bomsApi.create(data);
        showSuccess('BOM created successfully');
      }
      onSuccess();
    } catch (err: any) {
      showError(err.response?.data?.message || 'Failed to save BOM');
    } finally {
      setSubmitting(false);
    }
  };

  const addLine = () => {
    append({
      componentItemId: '',
      quantity: 1,
      uoMId: '',
      scrapFactor: 0,
      sequenceNumber: fields.length + 1,
    });
  };

  const itemOptions = items.map((item) => ({
    id: item.id,
    label: `${item.code} - ${item.name}`,
  }));

  const uomOptions = uoms.map((uom) => ({
    id: uom.id,
    label: `${uom.code} - ${uom.name}`,
  }));

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={bom ? 'Edit BOM' : 'New BOM'}
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
              label="Finished Item"
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
          <Grid item xs={12} sm={4}>
            <FormInput
              name="quantity"
              control={control}
              label="Quantity"
              type="number"
              rules={{ required: 'Quantity is required', min: { value: 0.001, message: 'Must be greater than 0' } }}
            />
          </Grid>
          <Grid item xs={12} sm={4}>
            <FormAutocomplete
              name="uoMId"
              control={control}
              label="Unit of Measure"
              options={uomOptions}
              loading={loadingUoMs}
              rules={{ required: 'Unit of measure is required' }}
            />
          </Grid>
          <Grid item xs={12} sm={4}>
            <FormCheckbox name="isActive" control={control} label="Active" />
          </Grid>
          <Grid item xs={12} sm={6}>
            <FormInput name="validFrom" control={control} label="Valid From" type="date" />
          </Grid>
          <Grid item xs={12} sm={6}>
            <FormInput name="validTo" control={control} label="Valid To" type="date" />
          </Grid>
          <Grid item xs={12}>
            <FormInput name="notes" control={control} label="Notes" multiline rows={2} />
          </Grid>

          <Grid item xs={12}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mt: 2, mb: 1 }}>
              <Typography variant="h6">BOM Lines</Typography>
              <Button variant="outlined" startIcon={<Add />} onClick={addLine} size="small">
                Add Component
              </Button>
            </Box>
            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Component Item</TableCell>
                    <TableCell>Quantity</TableCell>
                    <TableCell>UoM</TableCell>
                    <TableCell>Scrap %</TableCell>
                    <TableCell>Sequence</TableCell>
                    <TableCell width="80">Actions</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {fields.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={6} align="center">
                        No components added
                      </TableCell>
                    </TableRow>
                  ) : (
                    fields.map((field, index) => (
                      <TableRow key={field.id}>
                        <TableCell>
                          <FormAutocomplete
                            name={`lines.${index}.componentItemId`}
                            control={control}
                            label=""
                            options={itemOptions}
                            loading={loadingItems}
                            rules={{ required: 'Component is required' }}
                          />
                        </TableCell>
                        <TableCell>
                          <FormInput
                            name={`lines.${index}.quantity`}
                            control={control}
                            label=""
                            type="number"
                            rules={{ required: true, min: 0.001 }}
                          />
                        </TableCell>
                        <TableCell>
                          <FormAutocomplete
                            name={`lines.${index}.uoMId`}
                            control={control}
                            label=""
                            options={uomOptions}
                            loading={loadingUoMs}
                            rules={{ required: true }}
                          />
                        </TableCell>
                        <TableCell>
                          <FormInput
                            name={`lines.${index}.scrapFactor`}
                            control={control}
                            label=""
                            type="number"
                            rules={{ min: 0, max: 100 }}
                          />
                        </TableCell>
                        <TableCell>
                          <FormInput
                            name={`lines.${index}.sequenceNumber`}
                            control={control}
                            label=""
                            type="number"
                            rules={{ required: true, min: 1 }}
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

export default BOMForm;
