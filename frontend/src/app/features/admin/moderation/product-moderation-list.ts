import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, numberAttribute, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { CatalogApi, ProductSort } from '../../../core/catalog/catalog-api';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { LocalizedPipe } from '../../../shared/i18n/localized';
import { Pagination } from '../../../shared/ui/pagination';
import { AdminProductListItem, AdminProductState, ModerationApi, PRODUCT_STATES } from './moderation-api';
import { ModerationAction, ModerationDialog } from './moderation-dialog';
import { StateBadge } from './state-badge';

const PAGE_SIZE = 20;

type Pending = { action: ModerationAction; product: AdminProductListItem } | { action: 'disable'; bulk: true };

/** Price as typed in a filter box: a positive number, or nothing. */
function priceParam(value: string): number | null {
  const n = Number(value.replace(',', '.'));
  return value.trim() && Number.isFinite(n) && n >= 0 ? n : null;
}

/**
 * Products of every store, to find fakes (absurd prices, no photo…) and disable or delete them, one by one
 * or several at once. Filters live in the URL (?search=&storeId=&category=&minPrice=&maxPrice=&state=&sort=&page=).
 */
@Component({
  selector: 'app-product-moderation-list',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe, LocalizedPipe, Pagination, ModerationDialog, StateBadge],
  templateUrl: './product-moderation-list.html',
})
export class ProductModerationList {
  private readonly api = inject(ModerationApi);
  private readonly catalog = inject(CatalogApi);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  protected readonly language = inject(LanguageService);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly search = input<string | undefined>();
  readonly storeId = input<string | undefined>();
  readonly category = input<string | undefined>();
  readonly minPrice = input<string | undefined>();
  readonly maxPrice = input<string | undefined>();
  readonly state = input<AdminProductState | undefined>();
  readonly sort = input<ProductSort | undefined>();
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly states = PRODUCT_STATES;
  protected readonly sorts: ProductSort[] = ['Newest', 'PriceDesc', 'PriceAsc'];
  protected readonly pageSize = PAGE_SIZE;
  protected readonly categories = rxResource({ stream: () => this.catalog.productCategories() });

  protected readonly products = rxResource({
    params: () => ({
      search: this.search(), storeId: this.storeId(), category: this.category(),
      minPrice: priceParam(this.minPrice() ?? '') ?? undefined, maxPrice: priceParam(this.maxPrice() ?? '') ?? undefined,
      state: this.state(), sort: this.sort(), page: this.page(), pageSize: PAGE_SIZE,
    }),
    stream: ({ params }) => this.api.products(params),
  });

  protected readonly hasFilters = computed(() =>
    !!(this.search() || this.storeId() || this.category() || this.minPrice() || this.maxPrice() || this.state() || this.sort()));
  /** Name of the store filtered on (the list only carries its id). */
  protected readonly storeName = computed(() => this.storeId() ? this.products.value()?.items[0]?.storeName ?? null : null);

  protected readonly selected = signal<ReadonlySet<string>>(new Set());
  protected readonly selectable = computed(() => (this.products.value()?.items ?? []).filter((p) => !p.isDeleted && !p.isDisabled));
  protected readonly allSelected = computed(() => {
    const rows = this.selectable();
    return rows.length > 0 && rows.every((p) => this.selected().has(p.id));
  });

  protected readonly pending = signal<Pending | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  protected onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.apply({ search: value.trim() || null, page: null }), 300);
  }

  protected onPrice(key: 'minPrice' | 'maxPrice', value: string): void {
    this.apply({ [key]: priceParam(value), page: null });
  }

  protected apply(params: Record<string, string | number | null>): void {
    this.selected.set(new Set());
    this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge' });
  }

  protected clear(): void {
    this.selected.set(new Set());
    this.router.navigate([], { queryParams: {} });
  }

  protected toggle(id: string): void {
    const next = new Set(this.selected());
    if (!next.delete(id)) next.add(id);
    this.selected.set(next);
  }

  protected toggleAll(): void {
    this.selected.set(this.allSelected() ? new Set() : new Set(this.selectable().map((p) => p.id)));
  }

  protected ask(pending: Pending): void {
    this.error.set(null);
    this.pending.set(pending);
  }

  protected title(pending: Pending): string {
    return 'bulk' in pending
      ? this.transloco.translate('moderation.products.confirm.bulk', { count: this.selected().size })
      : this.transloco.translate(`moderation.products.confirm.${pending.action}`, { name: pending.product.name });
  }

  protected confirm(reason: string): void {
    const pending = this.pending();
    if (!pending) return;

    let call: Observable<unknown>;
    if ('bulk' in pending) {
      call = this.api.disableProducts([...this.selected()], reason);
    } else {
      const id = pending.product.id;
      call = pending.action === 'disable' ? this.api.disableProduct(id, reason)
        : pending.action === 'enable' ? this.api.enableProduct(id)
        : this.api.deleteProduct(id, reason);
    }

    this.busy.set(true);
    call.subscribe({
      next: () => {
        this.busy.set(false);
        this.pending.set(null);
        this.selected.set(new Set());
        this.products.reload();
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.busy.set(false);
      },
    });
  }

  protected stateOf(p: AdminProductListItem): string {
    return p.isDeleted ? 'Deleted' : p.isDisabled ? 'Disabled' : p.status;
  }
}
