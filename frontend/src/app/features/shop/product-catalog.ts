import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, numberAttribute } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { CatalogApi, ProductSort } from '../../core/catalog/catalog-api';
import { LanguageService } from '../../core/i18n/language.service';
import { Seo } from '../../core/seo/seo';
import { ProductCard } from '../../shared/catalog/product-card';
import { LocalizedPipe, pickName } from '../../shared/i18n/localized';
import { Pagination } from '../../shared/ui/pagination';

const PAGE_SIZE = 24;
export const SORTS: ProductSort[] = ['Newest', 'PriceAsc', 'PriceDesc'];

/** `/products?category=&search=&sort=&page=` — the whole catalogue, by product category (sub-categories included). */
@Component({
  selector: 'app-product-catalog',
  imports: [RouterLink, TranslocoPipe, LocalizedPipe, ProductCard, Pagination],
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
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly sorts = SORTS;
  protected readonly pageSize = PAGE_SIZE;
  protected readonly categories = rxResource({ stream: () => this.api.productCategories() });
  protected readonly products = rxResource({
    params: () => ({ category: this.category(), search: this.search(), sort: this.sort(), page: this.page(), pageSize: PAGE_SIZE }),
    stream: ({ params }) => this.api.products(params),
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
      else this.seo.setKey('shop.allProducts');
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
