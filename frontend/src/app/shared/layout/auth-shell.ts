import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

/**
 * Sign-in / sign-up frame: from lg, a navy panel with the doorway arch (ADR 0005 motif) beside the form;
 * below lg, the form alone. The page title and subtitle come in as translation keys, the form in the slot.
 */
@Component({
  selector: 'app-auth-shell',
  imports: [RouterLink, TranslocoPipe],
  template: `
    <main class="grid min-h-[calc(100dvh-4rem)] lg:grid-cols-[0.9fr_1.1fr]">
      <aside class="relative hidden overflow-hidden bg-ink px-12 py-16 text-white lg:flex lg:flex-col lg:justify-between" aria-hidden="true">
        <p class="font-display text-2xl font-extrabold tracking-tight">{{ 'app.name' | transloco }}</p>
        <div class="arch animate-arch-open mx-auto h-80 w-60 border-8 border-brand bg-brand-hover xl:h-96 xl:w-72">
          <div class="grid size-full grid-cols-2 gap-1 bg-brand">
            <span class="bg-brand-hover"></span><span class="bg-brand-hover"></span>
            <span class="bg-brand-hover"></span><span class="bg-brand-hover"></span>
          </div>
        </div>
        <p class="max-w-sm text-lg leading-relaxed text-white/75">{{ 'app.tagline' | transloco }}</p>
      </aside>

      <div class="flex items-start justify-center px-4 py-10 sm:py-16 lg:items-center">
        <div class="w-full animate-pop-in" [class]="wide() ? 'max-w-lg' : 'max-w-md'">
          <a routerLink="/" [attr.aria-label]="'app.name' | transloco"
            class="arch flex h-12 w-9 items-end justify-center bg-brand pb-2 font-display text-lg font-extrabold text-white shadow-brand transition-transform duration-300 hover:-translate-y-0.5 lg:hidden">
            {{ ('app.name' | transloco).charAt(0) }}
          </a>
          <h1 class="mt-6 page-title lg:mt-0">{{ title() | transloco }}</h1>
          <p class="page-subtitle">{{ subtitle() | transloco }}</p>
          <div class="mt-8">
            <ng-content />
          </div>
        </div>
      </div>
    </main>
  `,
})
export class AuthShell {
  readonly title = input.required<string>();
  readonly subtitle = input.required<string>();
  /** Wider column for the longer sign-up form. */
  readonly wide = input(false);
}
