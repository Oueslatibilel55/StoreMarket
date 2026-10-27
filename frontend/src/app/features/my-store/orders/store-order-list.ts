import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, input, numberAttribute } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../../core/i18n/language.service';
import { orderStatusTone, StoreOrderStatus } from '../../../core/orders/order-api';
import { StoreOrderApi } from '../../../core/orders/store-order-api';
import { Seo } from '../../../core/seo/seo';
import { Pagination } from '../../../shared/ui/pagination';
import { MyStoreTabs } from '../my-store-tabs';

const PAGE_SIZE = 20;
const STATUSES: StoreOrderStatus[] = ['Placed', 'Confirmed', 'Shipped', 'Delivered', 'NotDelivered', 'Cancelled'];

/** `/my-store/orders?status=&search=&page=` — the store's orders (016), newest first, filters in the URL. */
@Component({
  selector: 'app-store-order-list',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe, Pagination, MyStoreTabs],
  template: `
    <main class="page max-w-5xl animate-fade-in">
      <app-my-store-tabs />
      <header class="page-header">
        <div class="min-w-0">
          <h1 class="page-title">{{ 'storeOrders.title' | transloco }}</h1>
          <p class="page-subtitle">{{ 'storeOrders.subtitle' | transloco }}</p>
        </div>
      </header>

      <div class="card overflow-hidden">
        <nav class="tabs px-2 sm:px-3" [attr.aria-label]="'storeOrders.title' | transloco">
          <a [routerLink]="[]" [queryParams]="{ status: null, page: null }" queryParamsHandling="merge"
            class="tab" [class.tab-active]="!status()">
            {{ 'storeOrders.all' | transloco }}
          </a>
          @for (s of statuses; track s) {
            <a [routerLink]="[]" [queryParams]="{ status: s, page: null }" queryParamsHandling="merge"
              class="tab" [class.tab-active]="status() === s">
              {{ 'orders.status.' + s | transloco }}
              @if (count(s); as n) { <span class="ms-1 rounded-full bg-subtle px-1.5 text-xs font-semibold text-ink-soft tabular-nums" dir="ltr">{{ n }}</span> }
            </a>
          }
        </nav>
        <div class="panel-toolbar">
          <input type="search" [value]="search() ?? ''" (input)="onSearch($any($event.target).value)"
            [placeholder]="'storeOrders.searchPlaceholder' | transloco" [attr.aria-label]="'storeOrders.searchPlaceholder' | transloco" class="field" />
        </div>
        @if (orders.value(); as result) {
          @if (result.items.length === 0) {
            <p class="empty-state">{{ 'storeOrders.none' | transloco }}</p>
          } @else {
            <ul class="divide-y divide-line">
              @for (o of result.items; track o.id) {
                <li>
                  <a [routerLink]="['/my-store/orders', o.id]" class="list-row row-hover group">
                    <div class="min-w-0">
                      <p class="flex flex-wrap items-center gap-2">
                        <span class="font-mono font-semibold text-ink transition-colors group-hover:text-brand" dir="ltr">{{ o.reference }}</span>
                        <span class="pill" [class]="tone(o.status)">{{ 'orders.status.' + o.status | transloco }}</span>
                        @if (o.hasAffiliate) { <span class="pill pill-brand">{{ 'storeOrders.withCode' | transloco }}</span> }
                      </p>
                      <p class="mt-1 flex flex-wrap gap-x-3 gap-y-0.5 text-sm text-muted">
                        <span class="min-w-0 truncate">{{ o.clientName }}</span>
                        <span>{{ 'cities.' + o.city | transloco }}</span>
                        <span>{{ 'orders.itemCount' | transloco: { count: o.itemCount } }}</span>
                        <span>{{ o.createdAt | date: 'short' : undefined : language.locale() }}</span>
                      </p>
                    </div>
                    <p class="flex shrink-0 items-baseline justify-between gap-3 sm:block sm:text-end">
                      <span class="figure block text-ink" dir="ltr">{{ o.amountToCollect | number: '1.0-3' : language.locale() }} TND</span>
                      <span class="text-xs" [class]="o.paymentStatus === 'Collected' ? 'text-success' : 'text-muted'">{{ 'orders.payment.' + o.paymentStatus | transloco }}</span>
                    </p>
                  </a>
                </li>
              }
            </ul>
            <div class="border-t border-line bg-subtle/40 px-4 py-3 sm:px-5">
              <app-pagination [page]="result.page" [pageSize]="pageSize" [total]="result.total" (pageChange)="apply({ page: $event })" />
            </div>
          }
        } @else if (orders.error()) {
          <div class="card-body"><p role="alert" class="alert-danger">{{ 'errors.unknown' | transloco }}</p></div>
        } @else {
          <p class="empty-state">{{ 'common.loading' | transloco }}</p>
        }
      </div>
    </main>
  `,
})
export class StoreOrderList {
  private readonly api = inject(StoreOrderApi);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly status = input<StoreOrderStatus | undefined>();
  readonly search = input<string | undefined>();
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly statuses = STATUSES;
  protected readonly pageSize = PAGE_SIZE;
  protected readonly tone = orderStatusTone;
  protected readonly orders = rxResource({
    params: () => ({ status: this.status(), search: this.search(), page: this.page(), pageSize: PAGE_SIZE }),
    stream: ({ params }) => this.api.list(params),
  });
  private readonly counts = rxResource({ stream: () => this.api.counts() });
  protected readonly count = (s: StoreOrderStatus) => this.counts.value()?.byStatus[s] ?? 0;
  private searchTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    const seo = inject(Seo);
    effect(() => {
      this.language.lang();
      seo.setKey('storeOrders.title');
    });
  }

  protected apply(params: Record<string, string | number | null>): void {
    this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge' });
  }

  protected onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.apply({ search: value.trim() || null, page: null }), 300);
  }
}
