import { orderStatusTone } from './order-api';

describe('orderStatusTone', () => {
  it('gives each lifecycle step its colour', () => {
    expect(orderStatusTone('Placed')).toBe('pill-warning');
    expect(orderStatusTone('Shipped')).toBe('pill-brand');
    expect(orderStatusTone('Delivered')).toBe('pill-success');
    expect(orderStatusTone('Cancelled')).toBe('pill-danger');
    expect(orderStatusTone('NotDelivered')).toBe('pill-danger');
  });
});
