import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, numberAttribute, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { of } from 'rxjs';
import { CatalogApi, ProductSort } from '../../core/catalog/catalog-api';
import { LanguageService } from '../../core/i18n/language.service';
import { Seo } from '../../core/seo/seo';
import { SchemaField } from '../../core/stores/product-api';
import { ProductCard } from '../../shared/catalog/product-card';
import { StoreCard } from '../../shared/catalog/store-card';
import { LocalizedPipe, pickName } from '../../shared/i18n/localized';
import { Pagination } from '../../shared/ui/pagination';
import { filterableFields, formatAttr, parseAttr, positive, toggleAttr } from './catalog-filters';

const PAGE_SIZE = 24;
/** Stores shown above the products when searching; the rest are one click away on `/stores`. */
const STORE_HITS = 4;
export const SORTS: ProductSort[] = ['Newest', 'TopRated', 'PriceAsc', 'PriceDesc'];
export const MIN_RATINGS = [4, 3, 2];

/**
 * `/products?category=&search=&storeCategory=&minPrice=&maxPrice=&minRating=&attr=&sort=&page=` — the whole catalogue,
 * by product category (sub-categories included), and the search page (018): matching stores first, then products,
 * best rated first by default.
 */
@Component({
  selector: 'app-product-catalog',
  imports: [RouterLink, TranslocoPipe, LocalizedPipe, ProductCard, StoreCard, Pagination],
  templateUrl: './product-catalog.html',
})
export class ProductCatalog {
  private readonly api = inject(CatalogApi);
  private readonly router = inject(Router);
  private readonly seo = inject(Seo);
  protected readonly language = inject(LanguageService);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly category = input<string | undefined>();
  readonly search = input<string | undefined>();
  readonly sort = input<ProductSort | undefined>();
  readonly storeCategory = input<string | undefined>();
  readonly minPrice = input<string | undefined>();
  readonly maxPrice = input<string | undefined>();
  readonly minRating = input<string | undefined>();
  readonly attr = input<string | string[] | undefined>();
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly sorts = SORTS;
  protected readonly minRatings = MIN_RATINGS;
  protected readonly pageSize = PAGE_SIZE;
  protected readonly filtersOpen = signal(false);

  protected readonly searching = computed(() => !!this.search()?.trim());
  /** Searching sorts by rating first (018); browsing shows the newest. */
  protected readonly defaultSort = computed<ProductSort>(() => (this.searching() ? 'TopRated' : 'Newest'));
  protected readonly selection = computed(() => parseAttr(this.attr()));
  protected readonly priceFrom = computed(() => positive(this.minPrice()));
  protected readonly priceTo = computed(() => positive(this.maxPrice()));
  protected readonly stars = computed(() => {
    const n = positive(this.minRating());
    return n && n >= 1 && n <= 5 ? Math.floor(n) : undefined;
  });
  protected readonly activeFilters = computed(
    () =>
      (this.storeCategory() ? 1 : 0) +
      (this.priceFrom() !== undefined || this.priceTo() !== undefined ? 1 : 0) +
      (this.stars() ? 1 : 0) +
      Object.keys(this.selection()).length,
  );

  protected readonly categories = rxResource({ stream: () => this.api.productCategories() });
  protected readonly storeCategories = rxResource({ stream: () => this.api.storeCategories() });
  protected readonly schema = rxResource({
    params: () => this.category(),
    stream: ({ params }) => (params ? this.api.categorySchema(params) : of(undefined)),
  });
  protected readonly fields = computed(() => filterableFields(this.schema.value()));

  protected readonly products = rxResource({
    params: () => ({
      category: this.category(),
      search: this.search(),
      sort: this.sort(),
      storeCategory: this.storeCategory(),
      minPrice: this.priceFrom(),
      maxPrice: this.priceTo(),
      minRating: this.stars(),
      attr: formatAttr(this.selection()),
      page: this.page(),
      pageSize: PAGE_SIZE,
    }),
    stream: ({ params }) => this.api.products(params),
  });
  /** Stores whose name or description match, on the first page of a search only. */
  protected readonly stores = rxResource({
    params: () => (this.searching() && this.page() === 1 ? this.search()!.trim() : undefined),
    stream: ({ params }) => (params ? this.api.stores({ search: params, pageSize: STORE_HITS }) : of(undefined)),
  });

  protected readonly unknownCategory = computed(() => {
    const err = this.products.error();
    return err instanceof HttpErrorResponse && err.status === 404;
  });
  protected readonly current = computed(() => this.categories.value()?.find((c) => c.slug === this.category()));
  /** The current category's path, to show where we are (and its direct sub-categories to go deeper). */
  protected readonly children = computed(() => {
    const all = this.categories.value() ?? [];
    const parentId = this.current()?.id ?? null;
    return all.filter((c) => c.parentId === parentId);
  });
  protected readonly breadcrumb = computed(() => {
    const all = this.categories.value() ?? [];
    const path = [];
    for (let c = this.current(); c; c = all.find((x) => x.id === c!.parentId)) path.unshift(c);
    return path;
  });

  constructor() {
    effect(() => {
      const c = this.current();
      const lang = this.language.lang();
      if (c) this.seo.set(pickName(c.name, lang));
      else this.seo.setKey(this.searching() ? 'shop.searchTitle' : 'shop.allProducts');
    });
  }

  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  protected onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.apply({ search: value.trim() || null, page: null }), 300);
  }

  protected onSort(value: ProductSort): void {
    this.apply({ sort: value === this.defaultSort() ? null : value, page: null });
  }

  protected onPrice(which: 'minPrice' | 'maxPrice', value: string): void {
    this.apply({ [which]: positive(value) ?? null, page: null });
  }

  protected isChosen(field: SchemaField, value: string): boolean {
    return this.selection()[field.attribute.key]?.includes(value) ?? false;
  }

  protected toggle(field: SchemaField, value: string): void {
    const attr = formatAttr(toggleAttr(this.selection(), field, value));
    this.apply({ attr: attr.length ? attr : null, page: null });
  }

  protected clearFilters(): void {
    this.apply({ storeCategory: null, minPrice: null, maxPrice: null, minRating: null, attr: null, page: null });
  }

  /** Field filters belong to the category: changing it drops them. */
  protected categoryParams(slug: string | null): Record<string, string | null> {
    return { category: slug, attr: null, page: null };
  }

  protected apply(params: Record<string, string | number | string[] | null>): void {
    this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge' });
  }
}
