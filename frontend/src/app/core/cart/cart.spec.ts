import { normalizeCode } from './cart.service';
import { Referrals } from './referrals';

describe('normalizeCode', () => {
  it('accepts every spelling of a code', () => {
    expect(normalizeCode('#drf45')).toBe('DRF-45');
    expect(normalizeCode(' DRF-45 ')).toBe('DRF-45');
  });
  it('refuses what is not a code', () => {
    expect(normalizeCode('hello')).toBeNull();
    expect(normalizeCode('')).toBeNull();
    expect(normalizeCode(null)).toBeNull();
  });
});

describe('Referrals', () => {
  beforeEach(() => localStorage.clear());

  it('remembers a share-link code per store', () => {
    const referrals = new Referrals();
    referrals.remember('store-1', '#drf45');
    expect(referrals.codeFor('store-1')).toBe('DRF-45');
    expect(referrals.codeFor('store-2')).toBeNull();
  });

  it('ignores malformed codes and forgets after 30 days', () => {
    const referrals = new Referrals();
    referrals.remember('store-1', 'nope');
    expect(referrals.codeFor('store-1')).toBeNull();

    localStorage.setItem('webapp.referrals', JSON.stringify({ 'store-1': { code: 'DRF-45', at: Date.now() - 31 * 86_400_000 } }));
    expect(referrals.codeFor('store-1')).toBeNull();
  });
});
