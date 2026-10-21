import { Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { StoreCard as StoreCardModel } from '../../core/catalog/catalog-api';
import { LanguageService } from '../../core/i18n/language.service';
import { LocalizedPipe } from '../i18n/localized';

/** Store tile of the shop lists. */
@Component({
  selector: 'app-store-card',
  imports: [RouterLink, TranslocoPipe, LocalizedPipe],
  template: `
    @let s = store();
    <a [routerLink]="['/stores', s.slug]" class="group card-interactive flex h-full flex-col overflow-hidden">
      <div class="h-24 overflow-hidden bg-linear-to-br from-brand-soft via-subtle to-brand-soft">
        @if (s.bannerUrl) { <img [src]="s.bannerUrl" alt="" loading="lazy" class="size-full object-cover transition-transform duration-500 group-hover:scale-105" /> }
      </div>
      <div class="-mt-8 flex flex-1 flex-col gap-1 px-5 pb-5">
        <div class="size-16 overflow-hidden rounded-xl bg-card shadow-card ring-4 ring-card">
          @if (s.logoUrl) {
            <img [src]="s.logoUrl" [alt]="s.name" class="size-full object-cover" />
          } @else {
            <span class="flex size-full items-center justify-center bg-brand-soft text-xl font-bold text-brand">{{ s.name.charAt(0).toUpperCase() }}</span>
          }
        </div>
        <p class="mt-2 font-semibold text-ink transition-colors group-hover:text-brand">{{ s.name }}</p>
        <p class="text-xs text-muted">{{ s.category.name | localized: language.lang() }} · {{ 'cities.' + s.city | transloco }}</p>
        <p class="mt-auto flex items-center justify-between pt-3 text-xs">
          <span class="pill pill-neutral">{{ 'shop.productCount' | transloco: { count: s.productCount } }}</span>
          @if (!s.isOpen) { <span class="pill pill-danger">{{ 'shop.storeClosed' | transloco }}</span> }
        </p>
      </div>
    </a>
  `,
})
export class StoreCard {
  readonly store = input.required<StoreCardModel>();
  protected readonly language = inject(LanguageService);
}
