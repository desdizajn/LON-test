import React, { useEffect, useState, useCallback } from 'react';
import {
  Box,
  Button,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  IconButton,
  CircularProgress,
  SelectChangeEvent,
} from '@mui/material';
import { Add, Edit, Delete, Save, Cancel } from '@mui/icons-material';
import { api, knowledgeBaseApi } from '../services/api';
import { showSuccess, showError } from '../utils/toast';

interface CodeListItem {
  id: string;
  listType: string;
  code: string;
  descriptionMK: string;
  descriptionEN: string;
  sortOrder: number;
}

interface FormData {
  listType: string;
  code: string;
  descriptionMK: string;
  descriptionEN: string;
  sortOrder: number;
}

const emptyForm: FormData = {
  listType: '',
  code: '',
  descriptionMK: '',
  descriptionEN: '',
  sortOrder: 0,
};

const CodeListManagement: React.FC = () => {
  const [listTypes, setListTypes] = useState<string[]>([]);
  const [selectedListType, setSelectedListType] = useState<string>('');
  const [items, setItems] = useState<CodeListItem[]>([]);
  const [loading, setLoading] = useState(true);

  // Inline form state
  const [showAddForm, setShowAddForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [formData, setFormData] = useState<FormData>({ ...emptyForm });
  const [saving, setSaving] = useState(false);

  const loadCodeLists = useCallback(async (listType?: string) => {
    try {
      setLoading(true);
      const response = await knowledgeBaseApi.getCodeLists(listType || undefined);
      const data = response.data;

      if (data.listTypes) {
        setListTypes(data.listTypes);
      }

      // Flatten items from the grouped object
      const allItems: CodeListItem[] = [];
      if (data.items) {
        Object.keys(data.items).forEach((key) => {
          const group = data.items[key];
          if (Array.isArray(group)) {
            group.forEach((item: any) => allItems.push(item));
          }
        });
      }
      setItems(allItems);
    } catch (err: any) {
      showError(err.response?.data?.message || 'Грешка при вчитување на шифрарници');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadCodeLists();
  }, [loadCodeLists]);

  const handleListTypeChange = (event: SelectChangeEvent<string>) => {
    const value = event.target.value;
    setSelectedListType(value);
    loadCodeLists(value || undefined);
    setShowAddForm(false);
    setEditingId(null);
  };

  const handleFormChange = (field: keyof FormData, value: string | number) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
  };

  const handleAddClick = () => {
    setFormData({
      ...emptyForm,
      listType: selectedListType || '',
    });
    setShowAddForm(true);
    setEditingId(null);
  };

  const handleEditClick = (item: CodeListItem) => {
    setFormData({
      listType: item.listType,
      code: item.code,
      descriptionMK: item.descriptionMK,
      descriptionEN: item.descriptionEN,
      sortOrder: item.sortOrder,
    });
    setEditingId(item.id);
    setShowAddForm(false);
  };

  const handleCancelForm = () => {
    setShowAddForm(false);
    setEditingId(null);
    setFormData({ ...emptyForm });
  };

  const handleSaveNew = async () => {
    if (!formData.listType || !formData.code) {
      showError('Полињата Тип на листа и Код се задолжителни');
      return;
    }
    try {
      setSaving(true);
      await api.post('/KnowledgeBase/code-lists/items', formData);
      showSuccess('Ставката е успешно додадена');
      setShowAddForm(false);
      setFormData({ ...emptyForm });
      loadCodeLists(selectedListType || undefined);
    } catch (err: any) {
      showError(err.response?.data?.message || 'Грешка при додавање на ставка');
    } finally {
      setSaving(false);
    }
  };

  const handleSaveEdit = async () => {
    if (!editingId) return;
    if (!formData.listType || !formData.code) {
      showError('Полињата Тип на листа и Код се задолжителни');
      return;
    }
    try {
      setSaving(true);
      await api.put(`/KnowledgeBase/code-lists/items/${editingId}`, formData);
      showSuccess('Ставката е успешно ажурирана');
      setEditingId(null);
      setFormData({ ...emptyForm });
      loadCodeLists(selectedListType || undefined);
    } catch (err: any) {
      showError(err.response?.data?.message || 'Грешка при ажурирање на ставка');
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (item: CodeListItem) => {
    if (!window.confirm(`Дали сте сигурни дека сакате да ја избришете ставката "${item.code}"?`)) {
      return;
    }
    try {
      await api.delete(`/KnowledgeBase/code-lists/items/${item.id}`);
      showSuccess('Ставката е успешно избришана');
      loadCodeLists(selectedListType || undefined);
    } catch (err: any) {
      showError(err.response?.data?.message || 'Грешка при бришење на ставка');
    }
  };

  const renderFormRow = (isNew: boolean) => (
    <TableRow>
      <TableCell>
        <TextField
          size="small"
          fullWidth
          value={formData.code}
          onChange={(e) => handleFormChange('code', e.target.value)}
          placeholder="Код"
        />
      </TableCell>
      <TableCell>
        <TextField
          size="small"
          fullWidth
          value={formData.descriptionMK}
          onChange={(e) => handleFormChange('descriptionMK', e.target.value)}
          placeholder="Опис (МК)"
        />
      </TableCell>
      <TableCell>
        <TextField
          size="small"
          fullWidth
          value={formData.descriptionEN}
          onChange={(e) => handleFormChange('descriptionEN', e.target.value)}
          placeholder="Опис (EN)"
        />
      </TableCell>
      <TableCell>
        <TextField
          size="small"
          type="number"
          value={formData.sortOrder}
          onChange={(e) => handleFormChange('sortOrder', parseInt(e.target.value, 10) || 0)}
          sx={{ width: 80 }}
        />
      </TableCell>
      <TableCell>
        <Box sx={{ display: 'flex', gap: 0.5 }}>
          <IconButton
            color="primary"
            size="small"
            onClick={isNew ? handleSaveNew : handleSaveEdit}
            disabled={saving}
          >
            <Save fontSize="small" />
          </IconButton>
          <IconButton
            color="default"
            size="small"
            onClick={handleCancelForm}
            disabled={saving}
          >
            <Cancel fontSize="small" />
          </IconButton>
        </Box>
      </TableCell>
    </TableRow>
  );

  return (
    <Box>
      <Box sx={{ mb: 3, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h5">Управување со шифрарници</Typography>
        <Button
          variant="contained"
          startIcon={<Add />}
          onClick={handleAddClick}
          disabled={!selectedListType}
        >
          Додади ставка
        </Button>
      </Box>

      <Box sx={{ mb: 3, display: 'flex', gap: 2, alignItems: 'center' }}>
        <FormControl sx={{ minWidth: 300 }} size="small">
          <InputLabel>Тип на листа</InputLabel>
          <Select
            value={selectedListType}
            label="Тип на листа"
            onChange={handleListTypeChange}
          >
            <MenuItem value="">
              <em>Сите</em>
            </MenuItem>
            {listTypes.map((lt) => (
              <MenuItem key={lt} value={lt}>
                {lt}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      </Box>

      {loading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
          <CircularProgress />
        </Box>
      ) : (
        <TableContainer component={Paper}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontWeight: 'bold' }}>Код</TableCell>
                <TableCell sx={{ fontWeight: 'bold' }}>Опис (МК)</TableCell>
                <TableCell sx={{ fontWeight: 'bold' }}>Опис (EN)</TableCell>
                <TableCell sx={{ fontWeight: 'bold' }}>Редослед</TableCell>
                <TableCell sx={{ fontWeight: 'bold' }}>Акции</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {showAddForm && renderFormRow(true)}
              {items.length === 0 && !showAddForm ? (
                <TableRow>
                  <TableCell colSpan={5} align="center" sx={{ py: 3 }}>
                    <Typography color="text.secondary">
                      Нема пронајдени ставки
                    </Typography>
                  </TableCell>
                </TableRow>
              ) : (
                items.map((item) =>
                  editingId === item.id ? (
                    <React.Fragment key={item.id}>
                      {renderFormRow(false)}
                    </React.Fragment>
                  ) : (
                    <TableRow key={item.id} hover>
                      <TableCell>{item.code}</TableCell>
                      <TableCell>{item.descriptionMK}</TableCell>
                      <TableCell>{item.descriptionEN}</TableCell>
                      <TableCell>{item.sortOrder}</TableCell>
                      <TableCell>
                        <Box sx={{ display: 'flex', gap: 0.5 }}>
                          <IconButton
                            color="primary"
                            size="small"
                            onClick={() => handleEditClick(item)}
                          >
                            <Edit fontSize="small" />
                          </IconButton>
                          <IconButton
                            color="error"
                            size="small"
                            onClick={() => handleDelete(item)}
                          >
                            <Delete fontSize="small" />
                          </IconButton>
                        </Box>
                      </TableCell>
                    </TableRow>
                  )
                )
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
};

export default CodeListManagement;
