import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, numberAttribute } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../../core/i18n/language.service';
import { PRODUCT_STATUSES, ProductApi, ProductStatus } from '../../../core/stores/product-api';
import { LocalizedPipe } from '../../../shared/i18n/localized';
import { Pagination } from '../../../shared/ui/pagination';
import { MyStoreTabs } from '../my-store-tabs';

const PAGE_SIZE = 20;

/** The owner's products. Filters live in the URL (?search=&status=&page=). */
@Component({
  selector: 'app-product-list',
  imports: [DecimalPipe, RouterLink, TranslocoPipe, LocalizedPipe, Pagination, MyStoreTabs],
  templateUrl: './product-list.html',
})
export class ProductList {
  private readonly api = inject(ProductApi);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly search = input<string | undefined>();
  readonly status = input<ProductStatus | undefined>();
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly statuses = PRODUCT_STATUSES;
  protected readonly pageSize = PAGE_SIZE;
  protected readonly products = rxResource({
    params: () => ({ search: this.search(), status: this.status(), page: this.page(), pageSize: PAGE_SIZE }),
    stream: ({ params }) => this.api.list(params),
  });
  /** 404 store.notFound: the owner has to create their store first. */
  protected readonly noStore = computed(() => {
    const err = this.products.error();
    return err instanceof HttpErrorResponse && err.status === 404;
  });

  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  protected onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.apply({ search: value.trim() || null, page: null }), 300);
  }

  protected apply(params: Record<string, string | number | null>): void {
    this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge' });
  }
}
