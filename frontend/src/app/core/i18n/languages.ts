import { registerLocaleData } from '@angular/common';
import localeArTn from '@angular/common/locales/ar-TN';
import localeFrTn from '@angular/common/locales/fr-TN';

export type Lang = 'en' | 'fr' | 'ar';

export interface LanguageOption {
  code: Lang;
  label: string;
  /** Compact label for small screens. */
  short: string;
  dir: 'ltr' | 'rtl';
  /** Angular locale used by date / number / currency pipes (Tunisian variants: Western digits, TND). */
  locale: string;
}

export const LANGUAGES: readonly LanguageOption[] = [
  { code: 'fr', label: 'Français', short: 'FR', dir: 'ltr', locale: 'fr-TN' },
  { code: 'ar', label: 'العربية', short: 'ع', dir: 'rtl', locale: 'ar-TN' },
  { code: 'en', label: 'English', short: 'EN', dir: 'ltr', locale: 'en' },
];

export const DEFAULT_LANG: Lang = 'fr';

export const isLang = (value: unknown): value is Lang =>
  LANGUAGES.some((l) => l.code === value);

/** Pipes throw "Missing locale data" without this. `en` ships with Angular. */
registerLocaleData(localeFrTn);
registerLocaleData(localeArTn);
