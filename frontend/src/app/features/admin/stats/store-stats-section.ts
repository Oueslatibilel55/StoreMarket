import { Component, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { PeriodQuery, StatsApi } from '../../../core/stats/stats-api';
import { PeriodPicker } from '../../../shared/stats/period-picker';
import { StoreStatsView } from '../../../shared/stats/store-stats-view';

/** A store's results on its admin page (023), with links to its orders, products, ledger entries and partners. */
@Component({
  selector: 'app-store-stats-section',
  imports: [RouterLink, TranslocoPipe, PeriodPicker, StoreStatsView],
  template: `
    <section class="mt-8">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 class="section-title">{{ 'stats.storeTitle' | transloco }}</h2>
          <p class="mt-1 flex flex-wrap gap-x-4 gap-y-1 text-sm">
            <a routerLink="/admin/products" [queryParams]="{ storeId: storeId() }" class="link">{{ 'stats.seeProducts' | transloco }}</a>
            <a routerLink="/admin/orders" [queryParams]="{ storeId: storeId() }" class="link">{{ 'stats.seeOrders' | transloco }}</a>
          </p>
        </div>
        <app-period-picker [from]="period().from" [to]="period().to" (changed)="period.set($event)" />
      </div>
      @if (stats.error()) {
        <p class="alert-danger mt-4">{{ 'errors.unknown' | transloco }}</p>
      } @else if (stats.value(); as s) {
        <div class="mt-4"><app-store-stats-view [stats]="s" [storeId]="storeId()" [admin]="true" /></div>
      } @else {
        <p class="empty-state mt-4">{{ 'common.loading' | transloco }}</p>
      }
    </section>
  `,
})
export class StoreStatsSection {
  private readonly api = inject(StatsApi);

  readonly storeId = input.required<string>();
  protected readonly period = signal<PeriodQuery>({});

  protected readonly stats = rxResource({
    params: () => ({ id: this.storeId(), ...this.period() }),
    stream: ({ params }) => this.api.store(params.id, params),
  });
}
