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
import type { Item, ItemFormData, UoM } from '../../../types/masterData';

const WasteTariffInfo = '🗑️ Waste configuration (legacy Otpad/Zaguba)';

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
  const [items, setItems] = useState<Item[]>([]);
  const [wasteOpen, setWasteOpen] = useState(false);

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
      partnerSKU: item?.partnerSKU || '',
      primaryWasteItemId: item?.primaryWasteItemId ?? null,
      primaryWastePercentage: item?.primaryWastePercentage ?? null,
      secondaryWasteItemId: item?.secondaryWasteItemId ?? null,
      secondaryWastePercentage: item?.secondaryWastePercentage ?? null,
      tertiaryWasteItemId: item?.tertiaryWasteItemId ?? null,
      tertiaryWastePercentage: item?.tertiaryWastePercentage ?? null,
      zagubaItemId: item?.zagubaItemId ?? null,
      zagubaPercentage: item?.zagubaPercentage ?? null,
      wasteTariffCode: item?.wasteTariffCode || '',
      isWasteCatalog: item?.isWasteCatalog || false,
    },
  });

  useEffect(() => {
    loadUoMs();
    // Load items once for the waste-slot pickers; keeps the modal snappy
    // for up to ~3k items (~200 KB payload). Heavier tenants should wire
    // the pickers to the /article-picker endpoint instead (P15.6.1).
    itemsApi
      .getAll()
      .then((r) => setItems(r.data || []))
      .catch(() => {});
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
        partnerSKU: item.partnerSKU || '',
        primaryWasteItemId: item.primaryWasteItemId ?? null,
        primaryWastePercentage: item.primaryWastePercentage ?? null,
        secondaryWasteItemId: item.secondaryWasteItemId ?? null,
        secondaryWastePercentage: item.secondaryWastePercentage ?? null,
        tertiaryWasteItemId: item.tertiaryWasteItemId ?? null,
        tertiaryWastePercentage: item.tertiaryWastePercentage ?? null,
        zagubaItemId: item.zagubaItemId ?? null,
        zagubaPercentage: item.zagubaPercentage ?? null,
        wasteTariffCode: item.wasteTariffCode || '',
        isWasteCatalog: item.isWasteCatalog || false,
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
          <Grid item xs={12} sm={6}>
            <FormInput
              name="partnerSKU"
              control={control}
              label="Partner SKU (ArtKatBrStara)"
              placeholder="Partner / supplier's own code"
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

          <Grid item xs={12}>
            <button
              type="button"
              onClick={() => setWasteOpen((v) => !v)}
              style={{
                marginTop: 10,
                padding: '8px 14px',
                border: '1px solid #ccc',
                borderRadius: 4,
                background: wasteOpen ? '#fff3e0' : '#f7f7f7',
                cursor: 'pointer',
                fontSize: 13,
              }}
            >
              {wasteOpen ? '▼' : '▶'} {WasteTariffInfo}
            </button>
          </Grid>

          {wasteOpen && (
            <>
              <Grid item xs={12}>
                <div style={{ fontSize: 12, color: '#888', marginBottom: 5 }}>
                  Up to 3 recoverable waste slots + Zaguba (non-recoverable loss). Percentages
                  are of the CONSUMED input material (legacy ArtKatBrMatOtpad/1/2 + ArtKatBrMatZaguba).
                </div>
              </Grid>

              {([
                { idKey: 'primaryWasteItemId', pctKey: 'primaryWastePercentage', label: 'Primary waste (Otpad)' },
                { idKey: 'secondaryWasteItemId', pctKey: 'secondaryWastePercentage', label: 'Secondary waste (Otpad1)' },
                { idKey: 'tertiaryWasteItemId', pctKey: 'tertiaryWastePercentage', label: 'Tertiary waste (Otpad2)' },
                { idKey: 'zagubaItemId', pctKey: 'zagubaPercentage', label: 'Zaguba (non-recoverable loss)' },
              ] as const).map((slot) => (
                <React.Fragment key={slot.idKey}>
                  <Grid item xs={12} sm={8}>
                    <FormAutocomplete
                      name={slot.idKey as any}
                      control={control}
                      label={slot.label + ' — catalog item'}
                      options={items.map((i) => ({ id: i.id, label: `${i.code} · ${i.name}` }))}
                    />
                  </Grid>
                  <Grid item xs={12} sm={4}>
                    <FormInput
                      name={slot.pctKey as any}
                      control={control}
                      label="%"
                      type="number"
                      placeholder="0 – 100"
                    />
                  </Grid>
                </React.Fragment>
              ))}

              <Grid item xs={12} sm={6}>
                <FormInput
                  name="wasteTariffCode"
                  control={control}
                  label="Waste tariff code (ArtOtpadTarBr)"
                  placeholder="10-digit HS — used when THIS item IS a waste catalog entry"
                />
              </Grid>
              <Grid item xs={12} sm={6}>
                <FormCheckbox
                  name="isWasteCatalog"
                  control={control}
                  label="Is waste catalog entry (ArtOtpadZao)"
                />
              </Grid>
            </>
          )}
        </Grid>
      </Box>
    </FormDialog>
  );
};

export default ItemForm;
