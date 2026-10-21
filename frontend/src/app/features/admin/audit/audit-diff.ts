import { OWNER_ROLE_REVOKED } from '../../../core/stores/store-api';

export interface FieldChange {
  field: string;
  before: unknown;
  after: unknown;
}

const IGNORED = new Set(['id', 'createdAt', 'lastLoginAt', 'imageId', 'parentId']);

const parse = (json: string | null): Record<string, unknown> => {
  if (!json) return {};
  try {
    const value: unknown = JSON.parse(json);
    return value && typeof value === 'object' ? (value as Record<string, unknown>) : {};
  } catch {
    return {};
  }
};

/** Fields whose value differs between the before/after JSON snapshots of an audit entry (raw values). */
export function changedFields(before: string | null, after: string | null): FieldChange[] {
  const a = parse(before);
  const b = parse(after);
  const fields = new Set([...Object.keys(a), ...Object.keys(b)]);
  return [...fields]
    .filter((f) => !IGNORED.has(f) && JSON.stringify(a[f] ?? null) !== JSON.stringify(b[f] ?? null))
    .map((field) => ({ field, before: a[field] ?? null, after: b[field] ?? null }));
}

/**
 * Human value for one side of a change: enums become translation keys, timestamps get formatted.
 * `translate` and `formatDate` are injected so this stays a pure, testable function.
 */
export function displayValue(
  field: string,
  value: unknown,
  translate: (key: string) => string,
  formatDate: (iso: string) => string,
): string {
  if (value === null || value === undefined || value === '') return '—';
  if (field === 'roles' && Array.isArray(value)) return value.map((r) => translate(`roles.${r}`)).join(', ');
  if (field === 'role') return translate(`roles.${value}`);
  if (field === 'type' && typeof value === 'string') return translate(`admin.productCategories.types.${value}`);
  // User, store and product statuses share admin.status.* (Active, Blocked, Disabled, Published…).
  if (field === 'status') return translate(`admin.status.${value}`);
  if (field === 'moderationReason' && value === OWNER_ROLE_REVOKED) return translate('moderation.ownerRoleRevoked');
  if (field.endsWith('At') && typeof value === 'string') return formatDate(value);
  if (typeof value === 'boolean') return translate(value ? 'common.yes' : 'common.no');
  // Localized names { fr, ar, en } and other small objects: their filled values.
  if (value && typeof value === 'object' && !Array.isArray(value)) return Object.values(value).filter((v) => v !== null && v !== '').join(' · ');
  // Lists of objects (attribute options { key, name }): their French name, else their key.
  if (Array.isArray(value)) return value.map((v) => (v && typeof v === 'object' ? (v.name?.fr ?? v.key ?? '') : String(v))).join(', ');
  return String(value);
}
