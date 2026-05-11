import React, { useState } from 'react';
import {
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TableSortLabel,
  TextField,
  Box,
  IconButton,
  Tooltip,
  Checkbox,
} from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import VisibilityIcon from '@mui/icons-material/Visibility';
import KeyboardArrowRightIcon from '@mui/icons-material/KeyboardArrowRight';
import KeyboardArrowDownIcon from '@mui/icons-material/KeyboardArrowDown';
import LoadingSpinner from './LoadingSpinner';

export interface Column<T> {
  id: keyof T | string;
  label: string;
  minWidth?: number;
  align?: 'left' | 'right' | 'center';
  format?: (value: any, row: T) => React.ReactNode;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  data: T[];
  loading?: boolean;
  onEdit?: (row: T) => void;
  onDelete?: (row: T) => void;
  onView?: (row: T) => void;
  searchable?: boolean;
  searchPlaceholder?: string;
  emptyMessage?: string;
  rowsPerPageOptions?: number[];
  /**
   * P16.B2 — multi-select. Controlled API: pass `selectedIds` + `onSelectionChange`.
   * Omitting both disables the checkbox column entirely.
   */
  selectedIds?: string[];
  onSelectionChange?: (selectedIds: string[]) => void;
  /**
   * P16.B2 — render-prop for an expandable detail panel below each row.
   * When supplied, a leading toggle column is rendered with ▶ / ▼ icons.
   */
  renderExpanded?: (row: T) => React.ReactNode;
  /** Optional row-level CSS class hook (e.g. parent-row highlight in Production grid). */
  rowClassName?: (row: T) => string | undefined;
}

function DataTable<T extends { id: string }>({
  columns,
  data,
  loading = false,
  onEdit,
  onDelete,
  onView,
  searchable = true,
  searchPlaceholder = 'Search...',
  emptyMessage = 'No data available',
  rowsPerPageOptions = [10, 25, 50, 100],
  selectedIds,
  onSelectionChange,
  renderExpanded,
  rowClassName,
}: DataTableProps<T>) {
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(rowsPerPageOptions[0]);
  const [searchTerm, setSearchTerm] = useState('');
  const [orderBy, setOrderBy] = useState<keyof T | string>('');
  const [order, setOrder] = useState<'asc' | 'desc'>('asc');
  const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set());

  const selectionEnabled = !!onSelectionChange;
  const selected = new Set(selectedIds ?? []);

  const handleChangePage = (_event: unknown, newPage: number) => setPage(newPage);

  const handleChangeRowsPerPage = (event: React.ChangeEvent<HTMLInputElement>) => {
    setRowsPerPage(parseInt(event.target.value, 10));
    setPage(0);
  };

  const handleSort = (columnId: keyof T | string) => {
    const isAsc = orderBy === columnId && order === 'asc';
    setOrder(isAsc ? 'desc' : 'asc');
    setOrderBy(columnId);
  };

  const filteredData = searchable
    ? data.filter((row) =>
        Object.values(row as any).some((value) =>
          String(value).toLowerCase().includes(searchTerm.toLowerCase())
        )
      )
    : data;

  const sortedData = [...filteredData].sort((a, b) => {
    if (!orderBy) return 0;
    const aValue = (a as any)[orderBy];
    const bValue = (b as any)[orderBy];
    if (aValue < bValue) return order === 'asc' ? -1 : 1;
    if (aValue > bValue) return order === 'asc' ? 1 : -1;
    return 0;
  });

  const paginatedData = sortedData.slice(
    page * rowsPerPage,
    page * rowsPerPage + rowsPerPage
  );

  const pageIds = paginatedData.map((r) => r.id);
  const pageSelectedCount = pageIds.filter((id) => selected.has(id)).length;
  const allPageSelected = pageIds.length > 0 && pageSelectedCount === pageIds.length;
  const somePageSelected = pageSelectedCount > 0 && !allPageSelected;

  const toggleRow = (id: string) => {
    if (!onSelectionChange) return;
    const next = new Set(selected);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    onSelectionChange(Array.from(next));
  };

  const toggleAllOnPage = () => {
    if (!onSelectionChange) return;
    const next = new Set(selected);
    if (allPageSelected) {
      pageIds.forEach((id) => next.delete(id));
    } else {
      pageIds.forEach((id) => next.add(id));
    }
    onSelectionChange(Array.from(next));
  };

  const toggleExpand = (id: string) => {
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const hasActions = onEdit || onDelete || onView;
  const expandable = !!renderExpanded;
  const colSpan =
    columns.length + (hasActions ? 1 : 0) + (selectionEnabled ? 1 : 0) + (expandable ? 1 : 0);

  if (loading) {
    return <LoadingSpinner />;
  }

  return (
    <Paper sx={{ width: '100%', overflow: 'hidden' }}>
      {searchable && (
        <Box p={2}>
          <TextField
            fullWidth
            size="small"
            placeholder={searchPlaceholder}
            value={searchTerm}
            onChange={(e) => {
              setSearchTerm(e.target.value);
              setPage(0);
            }}
            inputProps={{ 'aria-label': 'search' }}
          />
        </Box>
      )}
      <TableContainer sx={{ maxHeight: 600 }}>
        <Table stickyHeader>
          <TableHead>
            <TableRow>
              {expandable && <TableCell padding="checkbox" />}
              {selectionEnabled && (
                <TableCell padding="checkbox">
                  <Checkbox
                    checked={allPageSelected}
                    indeterminate={somePageSelected}
                    onChange={toggleAllOnPage}
                    inputProps={{ 'aria-label': 'select all' }}
                  />
                </TableCell>
              )}
              {columns.map((column) => (
                <TableCell
                  key={String(column.id)}
                  align={column.align || 'left'}
                  style={{ minWidth: column.minWidth }}
                >
                  <TableSortLabel
                    active={orderBy === column.id}
                    direction={orderBy === column.id ? order : 'asc'}
                    onClick={() => handleSort(column.id)}
                  >
                    {column.label}
                  </TableSortLabel>
                </TableCell>
              ))}
              {hasActions && (
                <TableCell align="center" style={{ minWidth: 120 }}>
                  Actions
                </TableCell>
              )}
            </TableRow>
          </TableHead>
          <TableBody>
            {paginatedData.length === 0 ? (
              <TableRow>
                <TableCell colSpan={colSpan} align="center">
                  {emptyMessage}
                </TableCell>
              </TableRow>
            ) : (
              paginatedData.map((row) => {
                const isSelected = selected.has(row.id);
                const isExpanded = expandedIds.has(row.id);
                const customClass = rowClassName?.(row);
                return (
                  <React.Fragment key={row.id}>
                    <TableRow
                      hover
                      selected={isSelected}
                      className={customClass}
                    >
                      {expandable && (
                        <TableCell padding="checkbox">
                          <IconButton
                            size="small"
                            onClick={() => toggleExpand(row.id)}
                            aria-label={isExpanded ? 'collapse row' : 'expand row'}
                          >
                            {isExpanded ? (
                              <KeyboardArrowDownIcon fontSize="small" />
                            ) : (
                              <KeyboardArrowRightIcon fontSize="small" />
                            )}
                          </IconButton>
                        </TableCell>
                      )}
                      {selectionEnabled && (
                        <TableCell padding="checkbox">
                          <Checkbox
                            checked={isSelected}
                            onChange={() => toggleRow(row.id)}
                            inputProps={{ 'aria-label': `select row ${row.id}` }}
                          />
                        </TableCell>
                      )}
                      {columns.map((column) => {
                        const value = (row as any)[column.id];
                        return (
                          <TableCell key={String(column.id)} align={column.align || 'left'}>
                            {column.format ? column.format(value, row) : value}
                          </TableCell>
                        );
                      })}
                      {hasActions && (
                        <TableCell align="center">
                          <Box display="flex" justifyContent="center" gap={1}>
                            {onView && (
                              <Tooltip title="View">
                                <IconButton size="small" onClick={() => onView(row)}>
                                  <VisibilityIcon fontSize="small" />
                                </IconButton>
                              </Tooltip>
                            )}
                            {onEdit && (
                              <Tooltip title="Edit">
                                <IconButton size="small" color="primary" onClick={() => onEdit(row)}>
                                  <EditIcon fontSize="small" />
                                </IconButton>
                              </Tooltip>
                            )}
                            {onDelete && (
                              <Tooltip title="Delete">
                                <IconButton size="small" color="error" onClick={() => onDelete(row)}>
                                  <DeleteIcon fontSize="small" />
                                </IconButton>
                              </Tooltip>
                            )}
                          </Box>
                        </TableCell>
                      )}
                    </TableRow>
                    {expandable && isExpanded && (
                      <TableRow>
                        <TableCell colSpan={colSpan} sx={{ p: 0, borderBottom: 'unset' }}>
                          {renderExpanded!(row)}
                        </TableCell>
                      </TableRow>
                    )}
                  </React.Fragment>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>
      <TablePagination
        rowsPerPageOptions={rowsPerPageOptions}
        component="div"
        count={filteredData.length}
        rowsPerPage={rowsPerPage}
        page={page}
        onPageChange={handleChangePage}
        onRowsPerPageChange={handleChangeRowsPerPage}
      />
    </Paper>
  );
}

export default DataTable;
