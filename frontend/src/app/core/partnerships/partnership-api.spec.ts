import { OPEN_STATUSES, partnershipTone, shareLink } from './partnership-api';

describe('partnership helpers', () => {
  it('only requested and accepted partnerships block a new request', () =>
    expect([...OPEN_STATUSES].sort()).toEqual(['Accepted', 'Requested']));

  it('colours each status', () => {
    expect(partnershipTone('Accepted')).toBe('pill-success');
    expect(partnershipTone('Requested')).toBe('pill-warning');
    expect(partnershipTone('Refused')).toBe('pill-danger');
    expect(partnershipTone('Ended')).toBe('pill-neutral');
  });

  it('builds a share link carrying the code', () =>
    expect(shareLink('/products/musc', 'DRF-45')).toBe(`${location.origin}/products/musc?ref=DRF-45`));
});
