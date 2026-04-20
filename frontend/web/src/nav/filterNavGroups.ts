import { NavGroup } from './types';

/**
 * Pure filter: given the full list of NAV_GROUPS (plus an optional admin-only
 * SETTINGS_GROUP) and the role names that belong to a user, return the groups
 * that user is allowed to see. Extracted from useNavForRoles so it can be
 * exercised by unit tests without React/authService.
 *
 * Rules (matching the IA design in docs/design/P6-37-ia.md):
 *  - Empty roles array → empty result.
 *  - `Administrator` → all groups + settings group appended.
 *  - Otherwise → groups whose allowedRoles intersect the user roles.
 */
export function filterNavGroupsByRoles(
  groups: NavGroup[],
  settingsGroup: NavGroup | null,
  roles: readonly string[]
): NavGroup[] {
  if (roles.length === 0) return [];

  if (roles.includes('Administrator')) {
    return settingsGroup ? [...groups, settingsGroup] : [...groups];
  }

  return groups.filter((group) =>
    group.allowedRoles.some((allowed) => roles.includes(allowed))
  );
}
