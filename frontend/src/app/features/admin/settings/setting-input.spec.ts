import { AdminSetting } from '../admin.models';
import { sameValue, settingError } from './setting-input';

const setting = (patch: Partial<AdminSetting>): AdminSetting => ({
  key: 'payout.minimumCoins', group: 'coins', type: 'Number', value: '80', defaultValue: '80',
  min: 0, max: 1000000, unit: 'coins', editable: true, updatedAt: null, updatedByEmail: null, ...patch,
});

describe('settingError', () => {
  it('accepts values within the limits', () => {
    expect(settingError(setting({}), ' 100.5 ')).toBeNull();
    expect(settingError(setting({ type: 'WholeNumber', min: 1, max: 28 }), '10')).toBeNull();
  });

  it('mirrors the backend error codes', () => {
    expect(settingError(setting({}), '')).toBe('setting.valueRequired');
    expect(settingError(setting({}), '1,5')).toBe('setting.numberRequired');
    expect(settingError(setting({}), '0.0001')).toBe('setting.numberRequired');
    expect(settingError(setting({ type: 'WholeNumber' }), '2.5')).toBe('setting.integerRequired');
    expect(settingError(setting({ type: 'WholeNumber', min: 1, max: 28 }), '29')).toBe('setting.outOfRange');
    expect(settingError(setting({}), '-1')).toBe('setting.outOfRange');
  });
});

describe('sameValue', () => {
  it('compares numbers, not strings', () => {
    expect(sameValue(setting({ value: '80.5' }), '80.50')).toBe(true);
    expect(sameValue(setting({ value: '80' }), '81')).toBe(false);
  });
});
