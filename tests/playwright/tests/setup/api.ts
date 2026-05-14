/**
 * Phase 17 §E15 — thin API client for Playwright setup hooks.
 *
 * Tests use this for low-level seeding (login, fetch master-data ids,
 * create a ClientOrder) so the UI part of the test focuses on the
 * user-visible flow and stays readable.
 */
import { APIRequestContext, request } from '@playwright/test';

export const API_URL = process.env.API_URL || (process.env.BASE_URL || 'http://localhost:3000') + '/api';

export interface Tokens {
  accessToken: string;
  refreshToken?: string;
}

export async function login(api: APIRequestContext, username = 'admin', password = 'Admin123!'): Promise<string> {
  const resp = await api.post(`${API_URL}/auth/login`, {
    data: { username, password },
  });
  if (!resp.ok()) throw new Error(`Login failed: ${resp.status()} ${await resp.text()}`);
  const body = (await resp.json()) as Tokens;
  if (!body.accessToken) throw new Error('Login response missing accessToken');
  return body.accessToken;
}

export async function getFirstCustomerPartnerId(api: APIRequestContext, token: string): Promise<string> {
  const resp = await api.get(`${API_URL}/MasterData/partners`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!resp.ok()) throw new Error(`Partners list failed: ${resp.status()}`);
  const body = await resp.json();
  const rows = Array.isArray(body) ? body : body?.data ?? [];
  if (!rows.length) throw new Error('No partners in the system. Seed the database first.');
  return rows[0].id;
}

export async function getFirstLonAuthorizationId(api: APIRequestContext, token: string): Promise<string> {
  const resp = await api.get(`${API_URL}/Customs/lon-authorizations`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!resp.ok()) throw new Error(`LON authorizations list failed: ${resp.status()}`);
  const body = await resp.json();
  const rows = Array.isArray(body) ? body : body?.data ?? [];
  if (!rows.length) throw new Error('No LON authorizations. Seed the database first.');
  return rows[0].id;
}

export async function createClientOrder(
  api: APIRequestContext,
  token: string,
  payload: { customerPartnerId: string; lonAuthorizationId: string; customerOrderReference?: string }
): Promise<string> {
  const resp = await api.post(`${API_URL}/clientorders`, {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      customerPartnerId: payload.customerPartnerId,
      lonAuthorizationId: payload.lonAuthorizationId,
      customerOrderReference: payload.customerOrderReference || `E2E-${Date.now()}`,
      orderDate: new Date().toISOString().slice(0, 10),
    },
  });
  if (!resp.ok()) throw new Error(`Create ClientOrder failed: ${resp.status()} ${await resp.text()}`);
  const body = await resp.json();
  const id = body?.data ?? body?.id;
  if (!id) throw new Error('CreateClientOrder response missing id');
  return id;
}

export async function newApiContext(): Promise<APIRequestContext> {
  return await request.newContext();
}
