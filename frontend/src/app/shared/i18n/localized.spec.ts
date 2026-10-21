import { pickName } from './localized';

describe('pickName', () => {
  const name = { fr: 'Parfums', ar: 'عطور', en: null };

  it('uses the active language', () => expect(pickName(name, 'ar')).toBe('عطور'));
  it('falls back to French when the language has no name', () => expect(pickName(name, 'en')).toBe('Parfums'));
});
