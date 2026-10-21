import { Pipe, PipeTransform } from '@angular/core';
import { Lang } from '../../core/i18n/languages';

/** A name typed by the admin per language. French is always there; the others may be missing. */
export interface LocalizedName {
  fr: string;
  ar: string | null;
  en: string | null;
}

export function pickName(name: LocalizedName, lang: Lang): string {
  return name[lang] || name.fr;
}

/** `{{ category.name | localized: language.lang() }}` — falls back to French. */
@Pipe({ name: 'localized' })
export class LocalizedPipe implements PipeTransform {
  transform(name: LocalizedName, lang: Lang): string {
    return pickName(name, lang);
  }
}
