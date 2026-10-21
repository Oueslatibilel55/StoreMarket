import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, numberAttribute } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { CatalogApi } from '../../../core/catalog/catalog-api';
import { LanguageService } from '../../../core/i18n/language.service';
import { CITIES } from '../../../core/stores/cities';
import { StoreStatus } from '../../../core/stores/store-api';
import { LocalizedPipe } from '../../../shared/i18n/localized';
import { ModerationReason } from '../../../shared/ui/moderation-reason';
import { Pagination } from '../../../shared/ui/pagination';
import { ModerationApi, STORE_STATUSES } from './moderation-api';
import { StateBadge } from './state-badge';

const PAGE_SIZE = 20;

/** Every store, to spot fakes. Filters live in the URL (?search=&status=&category=&city=&from=&to=&deleted=&page=). */
@Component({
  selector: 'app-store-moderation-list',
  imports: [DatePipe, RouterLink, TranslocoPipe, LocalizedPipe, Pagination, StateBadge, ModerationReason],
  templateUrl: './store-moderation-list.html',
})
export class StoreModerationList {
  private readonly api = inject(ModerationApi);
  private readonly catalog = inject(CatalogApi);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly search = input<string | undefined>();
  readonly status = input<StoreStatus | undefined>();
  readonly category = input<string | undefined>();
  readonly city = input<string | undefined>();
  readonly from = input<string | undefined>();
  readonly to = input<string | undefined>();
  readonly deleted = input<string | undefined>();
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly statuses = STORE_STATUSES;
  protected readonly cities = CITIES;
  protected readonly pageSize = PAGE_SIZE;
  protected readonly categories = rxResource({ stream: () => this.catalog.storeCategories() });

  protected readonly stores = rxResource({
    params: () => ({
      search: this.search(), status: this.status(), category: this.category(), city: this.city(),
      createdFrom: this.from(), createdTo: this.to(), deleted: this.deleted() === 'true' ? true : undefined,
      page: this.page(), pageSize: PAGE_SIZE,
    }),
    stream: ({ params }) => this.api.stores(params),
  });

  protected readonly hasFilters = computed(() =>
    !!(this.search() || this.status() || this.category() || this.city() || this.from() || this.to() || this.deleted()));

  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  protected onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.apply({ search: value.trim() || null, page: null }), 300);
  }

  /** "Deleted" is not a status but a separate list (soft-deleted stores). */
  protected onStatus(value: string): void {
    if (value === 'deleted') this.apply({ deleted: 'true', status: null, page: null });
    else this.apply({ deleted: null, status: value || null, page: null });
  }

  protected apply(params: Record<string, string | number | null>): void {
    this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge' });
  }

  protected clear(): void {
    this.router.navigate([], { queryParams: {} });
  }
}
