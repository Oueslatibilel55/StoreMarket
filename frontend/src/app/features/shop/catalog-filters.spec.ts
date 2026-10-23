import { SchemaField } from '../../core/stores/product-api';
import { filterableFields, formatAttr, parseAttr, positive, toggleAttr } from './catalog-filters';

const field = (key: string, type: SchemaField['attribute']['type'], filterable = true): SchemaField => ({
  attribute: { id: key, key, name: { fr: key, ar: null, en: null }, type, unit: null, required: false, filterable, options: [] },
  definedById: 'c',
  definedBySlug: 'c',
  definedByName: { fr: 'c', ar: null, en: null },
});

describe('catalog filters', () => {
  it('reads attr params given once or repeated, skipping malformed ones', () => {
    expect(parseAttr(undefined)).toEqual({});
    expect(parseAttr('couleur:noir,rouge')).toEqual({ couleur: ['noir', 'rouge'] });
    expect(parseAttr(['couleur:noir', 'bio:true', 'junk', ':x', 'taille:', 'couleur:bleu'])).toEqual({ couleur: ['noir'], bio: ['true'] });
  });

  it('writes one param per field and round-trips', () => {
    const attr = formatAttr({ couleur: ['noir', 'rouge'], bio: ['true'], vide: [] });
    expect(attr).toEqual(['couleur:noir,rouge', 'bio:true']);
    expect(parseAttr(attr)).toEqual({ couleur: ['noir', 'rouge'], bio: ['true'] });
  });

  it('adds up choices, keeps one yes / no value, drops an emptied field', () => {
    const color = field('couleur', 'MultiChoice');
    const bio = field('bio', 'Boolean');
    let s = toggleAttr({}, color, 'noir');
    s = toggleAttr(s, color, 'rouge');
    expect(s).toEqual({ couleur: ['noir', 'rouge'] });
    s = toggleAttr(s, bio, 'true');
    s = toggleAttr(s, bio, 'false');
    expect(s['bio']).toEqual(['false']);
    s = toggleAttr(toggleAttr(s, color, 'noir'), color, 'rouge');
    expect(s).toEqual({ bio: ['false'] });
  });

  it('offers filterable choice and yes / no fields only', () => {
    const schema = {
      category: { id: 'c', parentId: null, slug: 'c', name: { fr: 'c', ar: null, en: null }, depth: 1 },
      path: [],
      attributes: [field('couleur', 'SingleChoice'), field('bio', 'Boolean'), field('poids', 'Number'), field('matiere', 'MultiChoice', false)],
    };
    expect(filterableFields(schema).map((f) => f.attribute.key)).toEqual(['couleur', 'bio']);
    expect(filterableFields(undefined)).toEqual([]);
  });

  it('keeps only usable numbers from the URL', () => {
    expect(positive('12.5')).toBe(12.5);
    expect(positive('0')).toBe(0);
    expect(positive('abc')).toBeUndefined();
    expect(positive('-3')).toBeUndefined();
    expect(positive('')).toBeUndefined();
    expect(positive(undefined)).toBeUndefined();
  });
});
