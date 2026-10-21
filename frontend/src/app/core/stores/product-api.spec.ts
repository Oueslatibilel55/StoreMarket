import { coinsPerItem, combinations } from './product-api';

describe('coinsPerItem', () => {
  it('is the commission in TND divided by the coin value', () => {
    expect(coinsPerItem(50, 10, 1)).toBe(5);
    expect(coinsPerItem(89.9, 15, 1)).toBe(13.485);
    expect(coinsPerItem(100, 10, 2)).toBe(5);
  });

  it('is 0 for incomplete input', () => {
    expect(coinsPerItem(0, 10, 1)).toBe(0);
    expect(coinsPerItem(50, 10, 0)).toBe(0);
  });
});

describe('combinations', () => {
  it('builds every combination of the axes', () => {
    expect(combinations([{ key: 'taille', options: ['s', 'm'] }, { key: 'couleur', options: ['noir', 'blanc'] }])).toEqual([
      { taille: 's', couleur: 'noir' },
      { taille: 's', couleur: 'blanc' },
      { taille: 'm', couleur: 'noir' },
      { taille: 'm', couleur: 'blanc' },
    ]);
  });

  it('is empty without axes or options', () => {
    expect(combinations([])).toEqual([]);
    expect(combinations([{ key: 'taille', options: [] }])).toEqual([]);
  });
});
