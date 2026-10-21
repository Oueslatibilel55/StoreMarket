import { safeReturnUrl } from './login';

describe('safeReturnUrl', () => {
  it('keeps same-site paths', () => expect(safeReturnUrl('/account')).toBe('/account'));
  it('rejects absolute and protocol-relative URLs', () => {
    expect(safeReturnUrl('https://evil.example')).toBe('/account');
    expect(safeReturnUrl('//evil.example')).toBe('/account');
  });
  it('defaults to the account page', () => expect(safeReturnUrl(undefined)).toBe('/account'));
});
