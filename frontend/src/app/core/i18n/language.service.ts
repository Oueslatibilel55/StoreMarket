import { DOCUMENT } from '@angular/common';
import { computed, inject, Injectable, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { DEFAULT_LANG, isLang, Lang, LANGUAGES } from './languages';

const STORAGE_KEY = 'lang';

/** Current UI language. Keeps Transloco, <html lang> and <html dir> (RTL for Arabic) in sync. */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);
  private readonly document = inject(DOCUMENT);

  private readonly current = signal<Lang>(DEFAULT_LANG);

  readonly lang = this.current.asReadonly();
  private readonly option = computed(() => LANGUAGES.find((l) => l.code === this.current()) ?? LANGUAGES[0]);

  readonly dir = computed(() => this.option().dir);
  /** Locale id for Angular pipes, e.g. `{{ date | date: 'longDate' : undefined : language.locale() }}`. */
  readonly locale = computed(() => this.option().locale);
  readonly languages = LANGUAGES;

  /** Saved choice → browser language → default. */
  init(): void {
    const browser = this.document.defaultView?.navigator.language.slice(0, 2);
    this.use(this.readSaved() ?? (isLang(browser) ? browser : DEFAULT_LANG));
  }

  use(lang: Lang): void {
    this.current.set(lang);
    this.transloco.setActiveLang(lang);
    const html = this.document.documentElement;
    html.lang = lang;
    html.dir = this.dir();
    this.save(lang);
  }

  private readSaved(): Lang | null {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      return isLang(saved) ? saved : null;
    } catch {
      return null;
    }
  }

  private save(lang: Lang): void {
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      // Storage unavailable (private mode): the choice just isn't remembered.
    }
  }
}
