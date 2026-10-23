import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Seo } from '../../../core/seo/seo';
import { PeriodQuery, StatsApi } from '../../../core/stats/stats-api';
import { PeriodPicker } from '../../../shared/stats/period-picker';
import { StoreStatsView } from '../../../shared/stats/store-stats-view';
import { MyStoreTabs } from '../my-store-tabs';

/**
 * `/my-store/stats?from=&to=` — the owner's dashboard (023): own sales, sales via affiliates and the coins given to
 * affiliates per product. Same numbers as the admin's view of the store, without the admin links.
 */
@Component({
  selector: 'app-store-dashboard',
  imports: [RouterLink, TranslocoPipe, MyStoreTabs, PeriodPicker, StoreStatsView],
  template: `
    <main class="page">
      <app-my-store-tabs />
      <header class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 class="page-title">{{ 'stats.storeDashboard' | transloco }}</h1>
          <p class="page-subtitle">{{ 'stats.storeDashboardHint' | transloco }}</p>
        </div>
        <app-period-picker [from]="from()" [to]="to()" (changed)="setPeriod($event)" />
      </header>

      @if (noStore()) {
        <div class="card mt-6 p-6 text-center">
          <p class="text-muted">{{ 'errors.store.notFound' | transloco }}</p>
          <a routerLink="/my-store" class="btn btn-primary mt-4">{{ 'myStore.createTitle' | transloco }}</a>
        </div>
      } @else if (stats.error()) {
        <p class="alert-danger mt-6">{{ 'errors.unknown' | transloco }}</p>
      } @else if (stats.value(); as s) {
        <div class="mt-6"><app-store-stats-view [stats]="s" storeId="" /></div>
      } @else {
        <p class="empty-state mt-6">{{ 'common.loading' | transloco }}</p>
      }
    </main>
  `,
})
export class StoreDashboard {
  private readonly api = inject(StatsApi);
  private readonly router = inject(Router);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly from = input<string | undefined>();
  readonly to = input<string | undefined>();

  protected readonly stats = rxResource({
    params: () => ({ from: this.from(), to: this.to() }),
    stream: ({ params }) => this.api.myStore(params),
  });
  protected readonly noStore = computed(() => {
    const err = this.stats.error();
    return err instanceof HttpErrorResponse && err.status === 404;
  });

  constructor() {
    const seo = inject(Seo);
    effect(() => seo.setKey('stats.storeDashboard'));
  }

  protected setPeriod(q: PeriodQuery): void {
    this.router.navigate([], { queryParams: { from: q.from ?? null, to: q.to ?? null }, queryParamsHandling: 'merge' });
  }
}
