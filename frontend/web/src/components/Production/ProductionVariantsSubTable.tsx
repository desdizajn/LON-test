import React from 'react';
import { Box } from '@mui/material';

interface ProductionVariantsSubTableProps {
  children: any[];
  variantBadges: (item: any) => React.ReactNode;
  getStatusBadge: (status: number) => React.ReactNode;
  renderActions: (o: any) => React.ReactNode;
}

/**
 * P16.B2 — Small inline sub-table rendering the variants of a Production
 * order group when its DataTable row is expanded. Kept narrow on purpose;
 * full DataTable for variants would add visual chrome (search bar, pagination)
 * that hurts the parent-child grouping UX.
 */
const ProductionVariantsSubTable: React.FC<ProductionVariantsSubTableProps> = ({
  children,
  variantBadges,
  getStatusBadge,
  renderActions,
}) => (
  <Box sx={{ pl: 6, pr: 2, py: 1, background: '#fafafa' }}>
    <table style={{ width: '100%', borderCollapse: 'collapse' }}>
      <tbody>
        {children.map((c: any) => (
          <tr key={c.id} style={{ borderBottom: '1px solid #eee' }}>
            <td style={{ padding: '6px 4px', color: '#4b5563', fontFamily: 'monospace' }}>
              ↳ {c.subOrderNumber || c.orderNumber}
            </td>
            <td style={{ padding: '6px 4px' }}>
              {c.item?.code}
              {variantBadges(c.item)}
            </td>
            <td style={{ padding: '6px 4px' }}>{c.orderQuantity.toFixed(2)}</td>
            <td style={{ padding: '6px 4px' }}>{c.producedQuantity.toFixed(2)}</td>
            <td style={{ padding: '6px 4px' }}>{c.scrapQuantity.toFixed(2)}</td>
            <td style={{ padding: '6px 4px' }}>{getStatusBadge(c.status)}</td>
            <td style={{ padding: '6px 4px' }}>{new Date(c.plannedStartDate).toLocaleDateString()}</td>
            <td style={{ padding: '6px 4px' }}>{new Date(c.plannedEndDate).toLocaleDateString()}</td>
            <td style={{ padding: '6px 4px' }}>{renderActions(c)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  </Box>
);

export default ProductionVariantsSubTable;
