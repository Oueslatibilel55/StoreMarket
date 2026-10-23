import { BillingStatus, bannerOf, invoiceTone, paymentTone, remaining } from './billing-api';

const status = (over: Partial<BillingStatus> = {}): BillingStatus => ({
  late: false, daysLeft: null, overdue: false, waitingForApproval: false, suspended: false, paidUntil: '2026-11-22',
  commissionsDue: 0, nextDue: null, lastRejectionReason: null, ...over,
});

describe('billing helpers', () => {
  it('shows the most urgent banner', () => {
    expect(bannerOf(undefined)).toBeNull();
    expect(bannerOf(status())).toBeNull();
    expect(bannerOf(status({ commissionsDue: 10 }))).toBe('due');
    expect(bannerOf(status({ commissionsDue: 10, late: true, daysLeft: 2 }))).toBe('countdown');
    expect(bannerOf(status({ late: true, overdue: true }))).toBe('overdue');
    expect(bannerOf(status({ late: true, daysLeft: 1, waitingForApproval: true }))).toBe('waiting');
    expect(bannerOf(status({ waitingForApproval: true, lastRejectionReason: 'x' }))).toBe('rejected');
    expect(bannerOf(status({ suspended: true, waitingForApproval: true }))).toBe('suspended');
  });

  it('colours invoices and payments by status', () => {
    expect(invoiceTone('Paid', true)).toBe('pill-success');
    expect(invoiceTone('Issued', true)).toBe('pill-danger');
    expect(invoiceTone('PartiallyPaid')).toBe('pill-warning');
    expect(paymentTone('Declared')).toBe('pill-warning');
    expect(paymentTone('Rejected')).toBe('pill-danger');
  });

  it('computes what is left without float noise', () => {
    expect(remaining({ total: 10.3, paidAmount: 0.1 } as never)).toBe(10.2);
  });
});
