import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, effect, inject, input, numberAttribute } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../core/i18n/language.service';
import { OrderApi, orderStatusTone } from '../../core/orders/order-api';
import { Seo } from '../../core/seo/seo';
import { Pagination } from '../../shared/ui/pagination';

/** `/orders` — the orders I placed (as a client, or for clients in affiliate mode), newest first. */
@Component({
  selector: 'app-order-list',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe, Pagination],
  template: `
    <main class="page-narrow animate-fade-in">
      <header class="page-header">
        <div class="min-w-0">
          <h1 class="page-title">{{ 'orders.mine' | transloco }}</h1>
          <p class="page-subtitle">{{ 'orders.mineHint' | transloco }}</p>
        </div>
        <a routerLink="/reviews" class="btn btn-secondary">{{ 'reviews.mine' | transloco }}</a>
      </header>

      @if (orders.value(); as result) {
        @if (result.items.length === 0) {
          <div class="card empty-state">
            <p class="font-semibold text-ink">{{ 'orders.none' | transloco }}</p>
            <a routerLink="/products" class="btn btn-primary mt-3">{{ 'shop.allProducts' | transloco }}</a>
          </div>
        } @else {
          <ul class="space-y-4">
            @for (o of result.items; track o.id) {
              <li>
                <a [routerLink]="['/orders', o.reference]" class="group card-interactive block overflow-hidden">
                  <div class="card-header bg-subtle/50">
                    <p class="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1.5">
                      <span class="code-chip text-sm" dir="ltr">{{ o.reference }}</span>
                      <span class="text-sm text-muted">{{ o.createdAt | date: 'mediumDate' : undefined : language.locale() }}</span>
                    </p>
                    <p class="flex flex-wrap gap-1.5">
                      @for (s of o.statuses; track $index) { <span class="pill" [class]="tone(s)">{{ 'orders.status.' + s | transloco }}</span> }
                    </p>
                  </div>
                  <div class="flex flex-col gap-3 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6">
                    <div class="min-w-0">
                      <p class="truncate font-semibold text-ink transition-colors group-hover:text-brand">{{ o.storeNames.join(', ') }}</p>
                      <p class="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-muted">
                        <span>{{ 'orders.itemCount' | transloco: { count: o.itemCount } }}</span>
                        @if (o.placedByAffiliate) { <span class="pill pill-brand">{{ 'orders.forClientNamed' | transloco: { name: o.clientName } }}</span> }
                      </p>
                    </div>
                    <p class="figure shrink-0 text-xl text-ink" dir="ltr">{{ o.total | number: '1.0-3' : language.locale() }} <span class="text-xs font-semibold text-muted">TND</span></p>
                  </div>
                </a>
              </li>
            }
          </ul>
          <div class="mt-6"><app-pagination [page]="result.page" [pageSize]="result.pageSize" [total]="result.total" (pageChange)="go($event)" /></div>
        }
      } @else {
        <p class="card empty-state">{{ 'common.loading' | transloco }}</p>
      }
    </main>
  `,
})
export class OrderList {
  private readonly api = inject(OrderApi);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });
  protected readonly tone = orderStatusTone;
  protected readonly orders = rxResource({ params: () => this.page(), stream: ({ params }) => this.api.mine(params) });

  constructor() {
    const seo = inject(Seo);
    effect(() => {
      this.language.lang();
      seo.setKey('orders.mine');
    });
  }

  protected go(page: number): void {
    this.router.navigate([], { queryParams: { page }, queryParamsHandling: 'merge' });
  }
}
