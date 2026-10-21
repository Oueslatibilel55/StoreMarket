import { PublicProduct } from '../../core/catalog/catalog-api';

type Variant = PublicProduct['variants'][number];

/** The variant matching every chosen option, once one option per axis is chosen. */
export function selectedVariant(variants: Variant[], axes: string[], selection: Record<string, string>): Variant | null {
  if (axes.some((a) => !selection[a])) return null;
  return variants.find((v) => axes.every((a) => v.options[a] === selection[a])) ?? null;
}

/**
 * Can this option be picked, given what is already chosen on the other axes?
 * True when some variant has it together with those choices (in stock or not — out-of-stock shows as such).
 */
export function optionExists(variants: Variant[], axes: string[], selection: Record<string, string>, axis: string, option: string): boolean {
  return variants.some((v) => v.options[axis] === option && axes.every((a) => a === axis || !selection[a] || v.options[a] === selection[a]));
}
