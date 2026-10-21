import { optionExists, selectedVariant } from './variant-picker';

const v = (id: string, taille: string, couleur: string, inStock = true) => ({ id, options: { taille, couleur }, price: 25, inStock });
const variants = [v('1', 's', 'noir'), v('2', 'm', 'noir'), v('3', 's', 'blanc', false)];
const axes = ['taille', 'couleur'];

describe('variant picker', () => {
  it('finds the variant once every axis is chosen', () => {
    expect(selectedVariant(variants, axes, { taille: 's' })).toBeNull();
    expect(selectedVariant(variants, axes, { taille: 's', couleur: 'blanc' })?.id).toBe('3');
    expect(selectedVariant(variants, axes, { taille: 'm', couleur: 'blanc' })).toBeNull();
  });

  it('offers only options that exist with the other choices', () => {
    expect(optionExists(variants, axes, {}, 'couleur', 'blanc')).toBe(true);
    expect(optionExists(variants, axes, { taille: 'm' }, 'couleur', 'blanc')).toBe(false);
    expect(optionExists(variants, axes, { taille: 'm' }, 'couleur', 'noir')).toBe(true);
    // Changing the axis itself is always possible when the option exists somewhere.
    expect(optionExists(variants, axes, { taille: 'm', couleur: 'noir' }, 'taille', 's')).toBe(true);
  });
});
