import { Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { StoreCard as StoreCardModel } from '../../core/catalog/catalog-api';
import { LanguageService } from '../../core/i18n/language.service';
import { LocalizedPipe } from '../i18n/localized';
import { StarRating } from '../reviews/star-rating';

/** Store tile of the shop lists. */
@Component({
  selector: 'app-store-card',
  imports: [RouterLink, TranslocoPipe, LocalizedPipe, StarRating],
  template: `
    @let s = store();
    <a [routerLink]="['/stores', s.slug]" class="group card-interactive flex h-full flex-col overflow-hidden">
      <div class="h-24 overflow-hidden bg-brand-soft">
        @if (s.bannerUrl) { <img [src]="s.bannerUrl" alt="" loading="lazy" class="size-full object-cover transition-transform duration-500 group-hover:scale-105" /> }
      </div>
      <div class="-mt-10 flex flex-1 flex-col gap-1 px-5 pb-5">
        <!-- A store's logo sits in an arch: its door (ADR 0005 motif). -->
        <div class="arch h-18 w-15 overflow-hidden bg-card shadow-card ring-4 ring-card">
          @if (s.logoUrl) {
            <img [src]="s.logoUrl" [alt]="s.name" class="size-full object-cover" />
          } @else {
            <span class="flex size-full items-center justify-center bg-brand pt-3 font-display text-xl font-extrabold text-white">{{ s.name.charAt(0).toUpperCase() }}</span>
          }
        </div>
        <p class="mt-2 font-display text-lg font-bold tracking-tight text-ink transition-colors group-hover:text-brand">{{ s.name }}</p>
        <p class="text-xs text-muted">{{ s.category.name | localized: language.lang() }} · {{ 'cities.' + s.city | transloco }}</p>
        @if (s.rating.count) {
          <app-star-rating [value]="s.rating.average" [count]="s.rating.count" [showValue]="true" size="sm" />
        }
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
