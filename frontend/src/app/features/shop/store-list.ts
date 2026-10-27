import { Component, effect, inject, input, numberAttribute } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { CatalogApi } from '../../core/catalog/catalog-api';
import { LanguageService } from '../../core/i18n/language.service';
import { Seo } from '../../core/seo/seo';
import { CITIES } from '../../core/stores/cities';
import { StoreCard } from '../../shared/catalog/store-card';
import { LocalizedPipe } from '../../shared/i18n/localized';
import { Pagination } from '../../shared/ui/pagination';

const PAGE_SIZE = 24;

/** `/stores?category=&city=&search=&page=` — every store (open ones first). */
@Component({
  selector: 'app-store-list',
  imports: [TranslocoPipe, LocalizedPipe, StoreCard, Pagination],
  template: `
    <main class="page">
      <header class="page-header">
        <div class="min-w-0">
          <h1 class="page-title">{{ 'shop.stores' | transloco }}</h1>
        </div>
      </header>

      <div class="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1 sm:mx-0 sm:flex-wrap sm:overflow-visible sm:px-0 sm:pb-0 [&>*]:shrink-0">
        <button type="button" (click)="apply({ category: null, page: null })"
          class="cursor-pointer rounded-full border px-3.5 py-1.5 text-sm font-medium transition-colors duration-200"
          [class]="!category() ? 'border-brand bg-brand text-white' : 'border-line-strong bg-card text-ink-soft hover:border-brand hover:text-brand'">
          {{ 'shop.allCategories' | transloco }}
        </button>
        @for (c of categories.value() ?? []; track c.id) {
          <button type="button" (click)="apply({ category: c.slug, page: null })"
            class="flex cursor-pointer items-center gap-1.5 rounded-full border px-3.5 py-1.5 text-sm font-medium transition-colors duration-200"
            [class]="category() === c.slug ? 'border-brand bg-brand text-white' : 'border-line-strong bg-card text-ink-soft hover:border-brand hover:text-brand'">
            @if (c.imageUrl) { <img [src]="c.imageUrl" alt="" class="size-4 rounded object-cover" /> } @else if (c.icon) { <span aria-hidden="true">{{ c.icon }}</span> }
            {{ c.name | localized: language.lang() }}
          </button>
        }
      </div>

      <div class="card mt-5 flex flex-wrap items-center gap-3 p-3 sm:p-4">
        <input type="search" [value]="search() ?? ''" (input)="onSearch($any($event.target).value)"
          [placeholder]="'shop.searchStores' | transloco" [attr.aria-label]="'shop.searchStores' | transloco"
          class="field min-w-0 flex-1 basis-56" />
        <select [value]="city() ?? ''" (change)="apply({ city: $any($event.target).value || null, page: null })"
          [attr.aria-label]="'myStore.fields.city' | transloco" class="field w-auto max-w-full">
          <option value="">{{ 'shop.allCities' | transloco }}</option>
          @for (c of cities; track c) { <option [value]="c">{{ 'cities.' + c | transloco }}</option> }
        </select>
      </div>

      @if (stores.error()) {
        <p class="alert-danger mt-6">{{ 'errors.unknown' | transloco }}</p>
      } @else if (stores.value(); as result) {
        @if (result.items.length) {
          <ul class="mt-6 grid gap-4 sm:grid-cols-2 sm:gap-6 lg:grid-cols-4">
            @for (s of result.items; track s.id) { <li><app-store-card [store]="s" /></li> }
          </ul>
          <div class="mt-10">
            <app-pagination [page]="result.page" [pageSize]="pageSize" [total]="result.total" (pageChange)="apply({ page: $event })" />
          </div>
        } @else {
          <div class="card mt-6"><p class="empty-state">{{ 'shop.noStores' | transloco }}</p></div>
        }
      } @else {
        <p class="empty-state mt-6">{{ 'common.loading' | transloco }}</p>
      }
    </main>
  `,
})
export class StoreList {
  private readonly api = inject(CatalogApi);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly category = input<string | undefined>();
  readonly city = input<string | undefined>();
  readonly search = input<string | undefined>();
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly cities = CITIES;
  protected readonly pageSize = PAGE_SIZE;
  protected readonly categories = rxResource({ stream: () => this.api.storeCategories() });
  protected readonly stores = rxResource({
    params: () => ({ category: this.category(), city: this.city(), search: this.search(), page: this.page(), pageSize: PAGE_SIZE }),
    stream: ({ params }) => this.api.stores(params),
  });

  constructor() {
    const seo = inject(Seo);
    effect(() => {
      this.language.lang();
      seo.setKey('shop.stores');
    });
  }

  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  protected onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.apply({ search: value.trim() || null, page: null }), 300);
  }

  protected apply(params: Record<string, string | number | null>): void {
    this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge' });
  }
}
