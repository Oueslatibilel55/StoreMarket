import { Component, inject } from '@angular/core';
import { LanguageService } from '../../core/i18n/language.service';
import { Lang } from '../../core/i18n/languages';

@Component({
  selector: 'app-language-switcher',
  template: `
    <div class="inline-flex rounded-full bg-subtle p-0.5 text-xs ring-1 ring-line-strong/60 ring-inset">
      @for (l of language.languages; track l.code) {
        <button
          type="button"
          class="rounded-full px-2 py-1 font-semibold transition-colors duration-150 xl:px-2.5"
          [class]="l.code === language.lang() ? 'bg-card text-ink shadow-card' : 'text-muted hover:text-ink'"
          [attr.aria-pressed]="l.code === language.lang()"
          (click)="select(l.code)"
        >
          <span class="xl:hidden" [attr.aria-label]="l.label">{{ l.short }}</span>
          <span class="hidden xl:inline">{{ l.label }}</span>
        </button>
      }
    </div>
  `,
})
export class LanguageSwitcher {
  protected readonly language = inject(LanguageService);

  protected select(lang: Lang): void {
    this.language.use(lang);
  }
}
