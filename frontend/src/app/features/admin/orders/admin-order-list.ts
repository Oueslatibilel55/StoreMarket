import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, input, numberAttribute } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../../core/i18n/language.service';
import { orderStatusTone, StoreOrderStatus } from '../../../core/orders/order-api';
import { Pagination } from '../../../shared/ui/pagination';
import { AdminApi } from '../admin-api';

const PAGE_SIZE = 20;

/** `/admin/orders?status=&search=&page=` — every order of the platform (016). */
@Component({
  selector: 'app-admin-order-list',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe, Pagination],
  template: `
    <header>
      <h1 class="page-title">{{ 'adminOrders.title' | transloco }}</h1>
      <p class="page-subtitle">{{ 'adminOrders.subtitle' | transloco }}</p>
    </header>

    <div class="card mt-6 overflow-hidden">
      <div class="grid gap-3 border-b border-line p-4 sm:grid-cols-[1fr_auto]">
        <input type="search" [value]="search() ?? ''" (input)="onSearch($any($event.target).value)"
          [placeholder]="'adminOrders.searchPlaceholder' | transloco" [attr.aria-label]="'adminOrders.searchPlaceholder' | transloco" class="field" />
        <select [value]="status() ?? ''" (change)="apply({ status: $any($event.target).value || null, page: null })"
          [attr.aria-label]="'adminOrders.status' | transloco" class="field w-auto">
          <option value="">{{ 'adminOrders.allStatuses' | transloco }}</option>
          @for (s of statuses; track s) { <option [value]="s">{{ 'orders.status.' + s | transloco }}</option> }
        </select>
      </div>
      @if (orders.value(); as result) {
        @if (result.items.length === 0) {
          <p class="empty-state">{{ 'adminOrders.none' | transloco }}</p>
        } @else {
          <ul class="divide-y divide-line">
            @for (o of result.items; track o.id) {
              <li>
                <a [routerLink]="['/admin/orders', o.reference]" class="row-hover flex flex-col gap-2 px-5 py-4 md:flex-row md:items-center md:justify-between">
                  <div class="min-w-0">
                    <p class="flex flex-wrap items-center gap-2">
                      <span class="font-mono font-semibold text-ink" dir="ltr">{{ o.reference }}</span>
                      @if (o.placedByAffiliate) { <span class="pill pill-brand">{{ 'orders.byAffiliate' | transloco }}</span> }
                    </p>
                    <p class="mt-1 truncate text-sm text-muted">
                      {{ o.clientName }} · <span dir="ltr">{{ o.clientPhone }}</span> · {{ 'cities.' + o.city | transloco }} · {{ o.createdAt | date: 'short' : undefined : language.locale() }}
                    </p>
                    <p class="mt-1.5 flex flex-wrap gap-1.5">
                      @for (s of o.storeOrders; track s.id) {
                        <span class="pill" [class]="tone(s.status)">{{ s.storeName }} · {{ 'orders.status.' + s.status | transloco }}</span>
                      }
                    </p>
                  </div>
                  <p class="shrink-0 font-bold text-ink" dir="ltr">{{ o.total | number: '1.0-3' : language.locale() }} TND</p>
                </a>
              </li>
            }
          </ul>
          <div class="border-t border-line bg-subtle/40 px-5 py-3">
            <app-pagination [page]="result.page" [pageSize]="pageSize" [total]="result.total" (pageChange)="apply({ page: $event })" />
          </div>
        }
      } @else {
        <p class="empty-state">{{ 'common.loading' | transloco }}</p>
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
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly statuses: StoreOrderStatus[] = ['Placed', 'Confirmed', 'Shipped', 'Delivered', 'NotDelivered', 'Cancelled'];
  protected readonly pageSize = PAGE_SIZE;
  protected readonly tone = orderStatusTone;
  protected readonly orders = rxResource({
    params: () => ({ status: this.status(), search: this.search(), page: this.page(), pageSize: PAGE_SIZE }),
    stream: ({ params }) => this.api.orders(params),
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
