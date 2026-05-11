import React from 'react';
import { render, screen, within, fireEvent } from '@testing-library/react';
import '@testing-library/jest-dom';
import DataTable, { Column } from './DataTable';

type Row = { id: string; name: string; qty: number };

const columns: Column<Row>[] = [
  { id: 'name', label: 'Name' },
  { id: 'qty', label: 'Quantity', align: 'right' },
];

const seedRows = (count: number, prefix = 'r'): Row[] =>
  Array.from({ length: count }, (_, i) => ({
    id: `${prefix}-${i}`,
    name: `Row ${String.fromCharCode(65 + (i % 26))}${Math.floor(i / 26) || ''}`,
    qty: count - i,
  }));

describe('DataTable', () => {
  it('renders empty state when data is empty', () => {
    render(<DataTable<Row> columns={columns} data={[]} emptyMessage="Nothing here" />);
    expect(screen.getByText('Nothing here')).toBeInTheDocument();
  });

  it('renders one row per data item', () => {
    const data = seedRows(3);
    render(<DataTable<Row> columns={columns} data={data} searchable={false} />);
    expect(screen.getByText('Row A')).toBeInTheDocument();
    expect(screen.getByText('Row B')).toBeInTheDocument();
    expect(screen.getByText('Row C')).toBeInTheDocument();
  });

  it('sorts ascending then descending when a column header is clicked', () => {
    const data: Row[] = [
      { id: '1', name: 'Charlie', qty: 5 },
      { id: '2', name: 'Alpha', qty: 7 },
      { id: '3', name: 'Bravo', qty: 3 },
    ];
    render(<DataTable<Row> columns={columns} data={data} searchable={false} />);
    const nameHeader = screen.getByText('Name');

    fireEvent.click(nameHeader);
    let bodyCells = screen.getAllByRole('cell').filter((c) => /^Alpha$|^Bravo$|^Charlie$/.test(c.textContent ?? ''));
    expect(bodyCells.map((c) => c.textContent)).toEqual(['Alpha', 'Bravo', 'Charlie']);

    fireEvent.click(nameHeader);
    bodyCells = screen.getAllByRole('cell').filter((c) => /^Alpha$|^Bravo$|^Charlie$/.test(c.textContent ?? ''));
    expect(bodyCells.map((c) => c.textContent)).toEqual(['Charlie', 'Bravo', 'Alpha']);
  });

  it('paginates: clicking next page reveals the next slice', () => {
    const data = seedRows(15);
    render(
      <DataTable<Row>
        columns={columns}
        data={data}
        searchable={false}
        rowsPerPageOptions={[10]}
      />
    );

    expect(screen.queryByText('Row K')).not.toBeInTheDocument();
    const nextBtn = screen.getByRole('button', { name: /next page/i });
    fireEvent.click(nextBtn);
    expect(screen.getByText('Row K')).toBeInTheDocument();
  });

  it('row selection: clicking the row checkbox fires onSelectionChange with the row id', () => {
    const data = seedRows(2);
    const onChange = jest.fn();
    render(
      <DataTable<Row>
        columns={columns}
        data={data}
        searchable={false}
        selectedIds={[]}
        onSelectionChange={onChange}
      />
    );
    const rowCheckbox = screen.getByRole('checkbox', { name: /select row r-0/ });
    fireEvent.click(rowCheckbox);
    expect(onChange).toHaveBeenCalledWith(['r-0']);
  });

  it('expandable rows: clicking toggle reveals renderExpanded content', () => {
    const data = seedRows(2);
    render(
      <DataTable<Row>
        columns={columns}
        data={data}
        searchable={false}
        renderExpanded={(row) => <div>details for {row.name}</div>}
      />
    );
    expect(screen.queryByText(/details for Row A/)).not.toBeInTheDocument();
    const toggle = screen.getAllByRole('button', { name: /expand row/i })[0];
    fireEvent.click(toggle);
    expect(screen.getByText(/details for Row A/)).toBeInTheDocument();
  });
});

// Suppress unused warning for `within` while keeping the import handy
// for follow-up tests (header-scoped, action-cell-scoped, etc).
void within;
