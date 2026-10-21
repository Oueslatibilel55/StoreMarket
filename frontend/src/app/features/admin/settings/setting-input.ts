import { AdminSetting } from '../admin.models';

const MAX_DECIMALS = 3;

/**
 * Same checks as the backend (SettingDefinition.Validate), so the admin sees the problem
 * before saving. Returns the error code (translated as `errors.<code>`) or null.
 */
export function settingError(setting: AdminSetting, raw: string): string | null {
  const value = raw.trim();
  if (!value) return 'setting.valueRequired';
  if (setting.type === 'Text') return null;

  const pattern = setting.type === 'WholeNumber' ? /^-?\d+$/ : new RegExp(`^-?\\d+(\\.\\d{1,${MAX_DECIMALS}})?$`);
  if (!pattern.test(value)) return setting.type === 'WholeNumber' ? 'setting.integerRequired' : 'setting.numberRequired';

  const number = Number(value);
  if ((setting.min !== null && number < setting.min) || (setting.max !== null && number > setting.max)) return 'setting.outOfRange';
  return null;
}

/** "80.50" and "80.5" are the same value: compare numbers, not strings. */
export function sameValue(setting: AdminSetting, raw: string): boolean {
  const value = raw.trim();
  return setting.type === 'Text' ? value === setting.value : Number(value) === Number(setting.value);
}
