import { formatRib, isOpenPayout, payoutTone } from './payout-api';

describe('payout helpers', () => {
  it('groups a RIB as bank · branch · account · key', () => {
    expect(formatRib('08123456789012345678')).toBe('08 123 4567890123456 78');
    expect(formatRib('1234')).toBe('1234');
    expect(formatRib(null)).toBe('');
  });

  it('knows which requests are still open', () => {
    expect(['Requested', 'Verified', 'Scheduled'].every((s) => isOpenPayout(s as never))).toBe(true);
    expect(['Paid', 'Rejected', 'Cancelled'].some((s) => isOpenPayout(s as never))).toBe(false);
  });

  it('colours paid green and rejected red', () => {
    expect(payoutTone('Paid')).toBe('pill-success');
    expect(payoutTone('Rejected')).toBe('pill-danger');
  });
});
