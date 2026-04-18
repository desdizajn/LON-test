/**
 * Central re-exports of generated OpenAPI types under friendly names.
 * Add new command/DTO aliases here as you use them in forms or services.
 *
 * Regenerate on backend contract change: `./scripts/gen-api-types.sh`
 */
import type { components } from './schema';

export type Schemas = components['schemas'];

// WMS
export type CreateReceiptCommand = Schemas['CreateReceiptCommand'];
export type ReceiptLineDto = Schemas['ReceiptLineDto'];

// Auth
export type LoginRequest = Schemas['LoginRequest'];
export type LoginResponse = Schemas['LoginResponse'];

// Add more as refactors progress (Phase 6 Priority-B):
//   ItemRequest, PartnerRequest, CreateProductionOrderCommand, CreateCustomsDeclarationCommand, etc.
