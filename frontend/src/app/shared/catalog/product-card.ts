import { DecimalPipe } from '@angular/common';
import { Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ProductCard as ProductCardModel } from '../../core/catalog/catalog-api';
import { LanguageService } from '../../core/i18n/language.service';
import { StarRating } from '../reviews/star-rating';

/** Product tile of the shop grids. */
@Component({
  selector: 'app-product-card',
  imports: [DecimalPipe, RouterLink, TranslocoPipe, StarRating],
  template: `
    @let p = product();
    <a [routerLink]="['/products', p.slug]" class="group card-interactive flex h-full flex-col overflow-hidden">
      <div class="relative aspect-square overflow-hidden bg-subtle">
        @if (p.imageUrl) {
          <img [src]="p.imageUrl" [alt]="p.name" loading="lazy" class="absolute inset-0 size-full object-cover transition-transform duration-500 ease-out group-hover:scale-105" />
        } @else {
          <span class="flex size-full items-center justify-center text-4xl text-muted/60" aria-hidden="true">🖼️</span>
        }
        @if (!p.inStock) {
          <span class="pill pill-dark absolute start-3 top-3">{{ 'shop.outOfStock' | transloco }}</span>
        } @else if (p.compareAtPrice && p.compareAtPrice > p.price) {
          <span class="pill absolute start-3 top-3 bg-danger font-semibold text-white ring-transparent" dir="ltr">
            −{{ (1 - p.price / p.compareAtPrice) * 100 | number: '1.0-0' }} %
          </span>
        }
      </div>
      <div class="flex flex-1 flex-col gap-1 p-4">
        <p class="text-xs font-medium text-muted">
          {{ p.storeName }}
          @if (!p.storeOpen) { · <span class="text-danger">{{ 'shop.storeClosed' | transloco }}</span> }
        </p>
        <p class="line-clamp-2 text-sm font-semibold text-ink transition-colors group-hover:text-brand">{{ p.name }}</p>
        @if (p.rating.count) {
          <app-star-rating [value]="p.rating.average" [count]="p.rating.count" size="sm" />
        }
        <p class="mt-auto flex flex-wrap items-baseline gap-x-1.5 pt-2">
          @if (p.minPrice < p.price) { <span class="text-xs text-muted">{{ 'products.from' | transloco }}</span> }
          <span class="text-base font-bold tracking-tight text-ink" dir="ltr">{{ p.minPrice | number: '1.0-3' : language.locale() }} <span class="text-xs font-semibold text-muted">TND</span></span>
          @if (p.compareAtPrice && p.compareAtPrice > p.price) {
            <span class="text-xs text-muted line-through" dir="ltr">{{ p.compareAtPrice | number: '1.0-3' : language.locale() }}</span>
          }
        </p>
      </div>
    </a>
  `,
})
export class ProductCard {
  readonly product = input.required<ProductCardModel>();
  protected readonly language = inject(LanguageService);
}
