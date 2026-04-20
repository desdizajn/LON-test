import { filterNavGroupsByRoles } from './filterNavGroups';
import { NAV_GROUPS, SETTINGS_GROUP } from './navGroups';

/**
 * P6.37.13 — regression guard for the role × sidebar IA matrix.
 *
 * Seeded TEKSPORT test users (see SESSION_LOG 2026-04-19 — P6.37.14 / RoleTopUpSeed)
 * pair up 1-to-1 with the 8 non-admin roles below. Each test asserts that the
 * single-role user sees exactly the groups listed in docs/design/P6-37-ia.md.
 *
 * When a group's allowedRoles is edited, this test will catch drift before
 * VPS visual smoke.
 */

const groupKeys = (groups: { key: string }[]) => groups.map((g) => g.key);

describe('filterNavGroupsByRoles — P6.37.13 role × group matrix', () => {
  it('no roles → no groups (defence for logged-out bugs)', () => {
    expect(filterNavGroupsByRoles(NAV_GROUPS, SETTINGS_GROUP, [])).toEqual([]);
  });

  it('Administrator → all 8 top groups + Settings', () => {
    const visible = filterNavGroupsByRoles(NAV_GROUPS, SETTINGS_GROUP, ['Administrator']);
    // 8 nav groups plus the settings group
    expect(groupKeys(visible)).toEqual([
      'warehouse',
      'customs',
      'production',
      'finished-goods',
      'hr',
      'machines',
      'finance',
      'management',
      'settings',
    ]);
  });

  it('Customs Officer (tek-customs) → customs + finished-goods only', () => {
    const visible = filterNavGroupsByRoles(NAV_GROUPS, SETTINGS_GROUP, ['Customs Officer']);
    expect(groupKeys(visible)).toEqual(['customs', 'finished-goods']);
  });

  it('Warehouse Operator (tek-wh-op) → warehouse only', () => {
    const visible = filterNavGroupsByRoles(NAV_GROUPS, SETTINGS_GROUP, ['Warehouse Operator']);
    expect(groupKeys(visible)).toEqual(['warehouse']);
  });

  it('Production Operator (tek-operator) → production only', () => {
    const visible = filterNavGroupsByRoles(NAV_GROUPS, SETTINGS_GROUP, ['Production Operator']);
    expect(groupKeys(visible)).toEqual(['production']);
  });

  it('Quality Controller (tek-qc) → warehouse + production + finished-goods', () => {
    const visible = filterNavGroupsByRoles(NAV_GROUPS, SETTINGS_GROUP, ['Quality Controller']);
    expect(groupKeys(visible)).toEqual(['warehouse', 'production', 'finished-goods']);
  });

  it('HR Manager (tek-hr) → hr only', () => {
    const visible = filterNavGroupsByRoles(NAV_GROUPS, SETTINGS_GROUP, ['HR Manager']);
    expect(groupKeys(visible)).toEqual(['hr']);
  });

  it('Maintenance Tech (tek-maint) → machines only', () => {
    const visible = filterNavGroupsByRoles(NAV_GROUPS, SETTINGS_GROUP, ['Maintenance Tech']);
    expect(groupKeys(visible)).toEqual(['machines']);
  });

  it('Finance Clerk (tek-finance) → finance only', () => {
    const visible = filterNavGroupsByRoles(NAV_GROUPS, SETTINGS_GROUP, ['Finance Clerk']);
    expect(groupKeys(visible)).toEqual(['finance']);
  });

  it('Manager (tek-mgr) → all 8 top groups, NOT settings', () => {
    const visible = filterNavGroupsByRoles(NAV_GROUPS, SETTINGS_GROUP, ['Manager']);
    expect(groupKeys(visible)).toEqual([
      'warehouse',
      'customs',
      'production',
      'finished-goods',
      'hr',
      'machines',
      'finance',
      'management',
    ]);
  });

  it('Warehouse Manager + Quality Controller combo → still unique set, no duplicates', () => {
    const visible = filterNavGroupsByRoles(NAV_GROUPS, SETTINGS_GROUP, [
      'Warehouse Manager',
      'Quality Controller',
    ]);
    expect(groupKeys(visible)).toEqual(['warehouse', 'production', 'finished-goods']);
  });

  it('Unknown role → no groups (safe default for rogue JWT claim)', () => {
    const visible = filterNavGroupsByRoles(NAV_GROUPS, SETTINGS_GROUP, ['Janitor']);
    expect(visible).toEqual([]);
  });

  it('Settings group is Administrator-only — no non-admin single role surfaces it', () => {
    const nonAdminRoles = [
      'Customs Officer',
      'Warehouse Operator',
      'Warehouse Manager',
      'Production Operator',
      'Production Manager',
      'Quality Controller',
      'HR Manager',
      'Maintenance Tech',
      'Finance Clerk',
      'Manager',
      'Viewer',
    ];
    nonAdminRoles.forEach((role) => {
      const visible = filterNavGroupsByRoles(NAV_GROUPS, SETTINGS_GROUP, [role]);
      expect(groupKeys(visible)).not.toContain('settings');
    });
  });
});
