/**
 * Nav IA types — see docs/design/P6-37-ia.md for design rationale.
 *
 * The nav is organized by **job role + daily tasks + process flow**, not by
 * architectural modules. Each group corresponds to a person in the factory
 * (warehouse operator, customs officer, production planner, etc.) and lists
 * their daily views + critical decisions.
 */

export type NavGroupKey =
  | 'warehouse'
  | 'customs'
  | 'production'
  | 'finished-goods'
  | 'hr'
  | 'machines'
  | 'finance'
  | 'management'
  | 'settings';

/** Backend readiness for a specific view. */
export type BackendStatus = 'missing' | 'partial' | 'exists';

export interface NavItem {
  /** Stable identifier, used for `activeModule` matching. */
  key: string;
  /** i18n key for the label, e.g. `nav.warehouse.incoming`. */
  labelKey: string;
  /** Optional leading emoji/icon shown before the label. */
  icon?: string;
  /** React Router path. */
  path: string;
  /** Honest backend readiness for this view. */
  backendStatus: BackendStatus;
  /** Optional WORK_PLAN reference (e.g. `P2.3.4`). Required when status=missing. */
  workPlanRef?: string;
  /**
   * Short Macedonian sentence describing what this view will do when built.
   * Shown on the placeholder page. Not i18n-keyed on purpose — cheap placeholder
   * wiring; full copy gets localized when the real page lands.
   */
  plannedBehavior?: string;
  /** Short hint pointing to a related existing page / data source. */
  existingDataHint?: string;
}

export interface NavGroup {
  key: NavGroupKey;
  /** Leading emoji shown in sidebar group header. */
  icon: string;
  /** i18n key for the group title, e.g. `nav.groups.warehouse`. */
  labelKey: string;
  /**
   * Seeded role names (exact match against `user.roles`) allowed to see
   * this group. `Administrator` always sees every group regardless.
   */
  allowedRoles: string[];
  items: NavItem[];
}
