import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, numberAttribute } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../../core/i18n/language.service';
import { orderStatusTone, StoreOrderStatus } from '../../../core/orders/order-api';
import { FilterChip, FilterChips, shortId } from '../../../shared/ui/filter-chips';
import { Pagination } from '../../../shared/ui/pagination';
import { AdminApi } from '../admin-api';

const PAGE_SIZE = 20;

/** `/admin/orders?status=&search=&page=` — every order of the platform (016); dashboard numbers (023) add store / affiliate / client / product / dates. */
@Component({
  selector: 'app-admin-order-list',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe, FilterChips, Pagination],
  template: `
    <header class="page-header">
      <div class="min-w-0">
        <h1 class="page-title">{{ 'adminOrders.title' | transloco }}</h1>
        <p class="page-subtitle">{{ 'adminOrders.subtitle' | transloco }}</p>
      </div>
    </header>

    <div class="card overflow-hidden">
      <div class="panel-toolbar">
        <input type="search" [value]="search() ?? ''" (input)="onSearch($any($event.target).value)"
          [placeholder]="'adminOrders.searchPlaceholder' | transloco" [attr.aria-label]="'adminOrders.searchPlaceholder' | transloco" class="field min-w-0 flex-1 basis-56" />
        <select [value]="status() ?? ''" (change)="apply({ status: $any($event.target).value || null, page: null })"
          [attr.aria-label]="'adminOrders.status' | transloco" class="field w-full sm:w-auto">
          <option value="">{{ 'adminOrders.allStatuses' | transloco }}</option>
          @for (s of statuses; track s) { <option [value]="s">{{ 'orders.status.' + s | transloco }}</option> }
        </select>
      </div>
      <app-filter-chips [chips]="drillDown()" (cleared)="apply($event)" />
      @if (orders.value(); as result) {
        @if (result.items.length === 0) {
          <p class="empty-state">{{ 'adminOrders.none' | transloco }}</p>
        } @else {
          <ul class="divide-y divide-line">
            @for (o of result.items; track o.id) {
              <li>
                <a [routerLink]="['/admin/orders', o.reference]" class="list-row row-hover">
                  <div class="min-w-0">
                    <p class="flex flex-wrap items-center gap-2">
                      <span class="font-mono font-semibold text-ink" dir="ltr">{{ o.reference }}</span>
                      @if (o.placedByAffiliate) { <span class="pill pill-brand">{{ 'orders.byAffiliate' | transloco }}</span> }
                    </p>
                    <p class="mt-1 flex flex-wrap gap-x-3 gap-y-0.5 text-sm text-muted">
                      <span class="min-w-0 truncate">{{ o.clientName }}</span>
                      <span dir="ltr">{{ o.clientPhone }}</span>
                      <span>{{ 'cities.' + o.city | transloco }}</span>
                      <span>{{ o.createdAt | date: 'short' : undefined : language.locale() }}</span>
                    </p>
                    <p class="mt-2 flex flex-wrap gap-1.5">
                      @for (s of o.storeOrders; track s.id) {
                        <span class="pill gap-1.5" [class]="tone(s.status)"><span class="font-semibold">{{ s.storeName }}</span><span>{{ 'orders.status.' + s.status | transloco }}</span></span>
                      }
                    </p>
                  </div>
                  <p class="figure shrink-0 text-lg text-ink" dir="ltr">{{ o.total | number: '1.0-3' : language.locale() }} TND</p>
                </a>
              </li>
            }
          </ul>
          <div class="border-t border-line bg-subtle/40 px-4 py-3 sm:px-5">
            <app-pagination [page]="result.page" [pageSize]="pageSize" [total]="result.total" (pageChange)="apply({ page: $event })" />
          </div>
        }
      } @else {
        <p class="py-14 text-center text-sm text-muted">{{ 'common.loading' | transloco }}</p>
      }
    </div>
  `,
})
export class AdminOrderList {
  private readonly api = inject(AdminApi);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly status = input<StoreOrderStatus | undefined>();
  readonly search = input<string | undefined>();
  readonly storeId = input<string | undefined>();
  readonly affiliateId = input<string | undefined>();
  readonly clientId = input<string | undefined>();
  readonly productId = input<string | undefined>();
  readonly from = input<string | undefined>();
  readonly to = input<string | undefined>();
  readonly dateField = input<'Placed' | 'Delivered' | undefined>();
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly statuses: StoreOrderStatus[] = ['Placed', 'Confirmed', 'Shipped', 'Delivered', 'NotDelivered', 'Cancelled'];
  protected readonly pageSize = PAGE_SIZE;
  protected readonly tone = orderStatusTone;
  protected readonly orders = rxResource({
    params: () => ({
      status: this.status(), search: this.search(), storeId: this.storeId(), affiliateId: this.affiliateId(), clientId: this.clientId(),
      productId: this.productId(), from: this.from(), to: this.to(), dateField: this.dateField(), page: this.page(), pageSize: PAGE_SIZE,
    }),
    stream: ({ params }) => this.api.orders(params),
  });
  /** Filters that have no control on this page (they come from a dashboard link), each with how to remove it. */
  protected readonly drillDown = computed(() => {
    const chips: FilterChip[] = [];
    if (this.storeId()) chips.push({ label: 'filters.store', value: shortId(this.storeId()!), clear: { storeId: null } });
    if (this.affiliateId()) chips.push({ label: 'filters.affiliate', value: shortId(this.affiliateId()!), clear: { affiliateId: null } });
    if (this.clientId()) chips.push({ label: 'filters.client', value: shortId(this.clientId()!), clear: { clientId: null } });
    if (this.productId()) chips.push({ label: 'filters.product', value: shortId(this.productId()!), clear: { productId: null } });
    if (this.from() || this.to())
      chips.push({
        label: this.dateField() === 'Delivered' ? 'filters.deliveredBetween' : 'filters.placedBetween',
        value: `${this.from() ?? '…'} → ${this.to() ?? '…'}`,
        clear: { from: null, to: null, dateField: null },
      });
    return chips;
  });
  private searchTimer?: ReturnType<typeof setTimeout>;

  protected apply(params: Record<string, string | number | null>): void {
    this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge' });
  }

  protected onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.apply({ search: value.trim() || null, page: null }), 300);
  }
}
