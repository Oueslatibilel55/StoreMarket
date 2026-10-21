import { AdminProductCategory, depthOf, pathOf } from './product-category-api';

const node = (id: string, parentId: string | null): AdminProductCategory => ({
  id, parentId, slug: id, name: { fr: id, ar: null, en: null }, sortOrder: 1, isActive: true, attributes: [],
});

describe('category tree helpers', () => {
  const clothes = node('vetements', null);
  const women = node('femme', 'vetements');
  const dresses = node('robes', 'femme');
  const all = [clothes, women, dresses];

  it('walks the path from the root', () => {
    expect(pathOf(all, dresses).map((c) => c.id)).toEqual(['vetements', 'femme', 'robes']);
    expect(depthOf(all, dresses)).toBe(3);
    expect(depthOf(all, clothes)).toBe(1);
  });
});
