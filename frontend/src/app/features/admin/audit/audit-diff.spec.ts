import { changedFields, displayValue } from './audit-diff';

describe('changedFields', () => {
  it('lists only the fields that changed', () => {
    const before = JSON.stringify({ id: '1', status: 'Active', roles: ['Buyer'], email: 'a@b.tn', blockedReason: null });
    const after = JSON.stringify({ id: '1', status: 'Blocked', roles: ['Buyer'], email: 'a@b.tn', blockedReason: 'Fraud' });

    expect(changedFields(before, after)).toEqual([
      { field: 'status', before: 'Active', after: 'Blocked' },
      { field: 'blockedReason', before: null, after: 'Fraud' },
    ]);
  });

  it('ignores ids, timestamps and broken JSON', () => {
    expect(changedFields('{"id":"1","createdAt":"x"}', '{"id":"2","createdAt":"y"}')).toEqual([]);
    expect(changedFields('not json', null)).toEqual([]);
  });
});

describe('displayValue', () => {
  const t = (key: string) => `<${key}>`;
  const d = (iso: string) => `date(${iso})`;

  it('translates roles and statuses', () => {
    expect(displayValue('roles', ['Buyer', 'Admin'], t, d)).toBe('<roles.Buyer>, <roles.Admin>');
    expect(displayValue('status', 'Blocked', t, d)).toBe('<admin.status.Blocked>');
  });

  it('formats timestamps and shows a dash for empty values', () => {
    expect(displayValue('blockedAt', '2026-10-19T16:40:00Z', t, d)).toBe('date(2026-10-19T16:40:00Z)');
    expect(displayValue('blockedReason', null, t, d)).toBe('—');
    expect(displayValue('blockedReason', 'Fraud', t, d)).toBe('Fraud');
  });

  it('shows yes / no and localized names', () => {
    expect(displayValue('isActive', false, t, d)).toBe('<common.no>');
    expect(displayValue('name', { fr: 'Parfums', ar: 'عطور', en: null }, t, d)).toBe('Parfums · عطور');
    expect(displayValue('role', 'Affiliate', t, d)).toBe('<roles.Affiliate>');
  });

  it('translates the automatic moderation reason, keeps typed ones', () => {
    expect(displayValue('moderationReason', 'store.ownerRoleRevoked', t, d)).toBe('<moderation.ownerRoleRevoked>');
    expect(displayValue('moderationReason', 'Faux magasin', t, d)).toBe('Faux magasin');
    expect(displayValue('status', 'Disabled', t, d)).toBe('<admin.status.Disabled>');
  });

  it('shows attribute types and option lists', () => {
    expect(displayValue('type', 'SingleChoice', t, d)).toBe('<admin.productCategories.types.SingleChoice>');
    expect(displayValue('options', [{ key: 's', name: { fr: 'S' } }, { key: 'm', name: { fr: 'M' } }], t, d)).toBe('S, M');
  });
});
