import { CategorySchema, SchemaField } from '../../core/stores/product-api';

/** Chosen values per field, from the `attr=field:choice,choice` query params (018). */
export type AttrSelection = Record<string, string[]>;

/** Router input binding gives one repeated query param as a string, several as an array. */
export function parseAttr(raw: string | string[] | undefined | null): AttrSelection {
  const selection: AttrSelection = {};
  for (const entry of raw == null ? [] : Array.isArray(raw) ? raw : [raw]) {
    const colon = entry.indexOf(':');
    if (colon <= 0) continue;
    const key = entry.slice(0, colon);
    const values = entry.slice(colon + 1).split(',').filter(Boolean);
    if (values.length && !selection[key]) selection[key] = values;
  }
  return selection;
}

export function formatAttr(selection: AttrSelection): string[] {
  return Object.entries(selection)
    .filter(([, values]) => values.length)
    .map(([key, values]) => `${key}:${values.join(',')}`);
}

/**
 * Adds or removes one value. A yes / no field holds one value at a time (choosing "yes" drops "no");
 * choices of one field add up (any of them matches).
 */
export function toggleAttr(selection: AttrSelection, field: SchemaField, value: string): AttrSelection {
  const key = field.attribute.key;
  const current = selection[key] ?? [];
  const next = current.includes(value)
    ? current.filter((v) => v !== value)
    : field.attribute.type === 'Boolean' ? [value] : [...current, value];
  const result = { ...selection };
  if (next.length) result[key] = next;
  else delete result[key];
  return result;
}

/** Fields the shop can filter on: marked filterable by the admin, with choices or yes / no. */
export function filterableFields(schema: CategorySchema | undefined): SchemaField[] {
  return (schema?.attributes ?? []).filter(
    (f) => f.attribute.filterable && ['SingleChoice', 'MultiChoice', 'Boolean'].includes(f.attribute.type),
  );
}

/** A positive number from the URL, else undefined (`?minPrice=abc` is ignored). */
export function positive(value: unknown): number | undefined {
  const n = Number(value);
  return value !== undefined && value !== null && value !== '' && Number.isFinite(n) && n >= 0 ? n : undefined;
}
