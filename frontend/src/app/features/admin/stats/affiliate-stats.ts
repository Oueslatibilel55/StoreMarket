import { DecimalPipe } from '@angular/common';
import { Component, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../../core/i18n/language.service';
import { PeriodQuery, StatsApi } from '../../../core/stats/stats-api';
import { ConversionTiles } from '../../../shared/stats/conversion-tiles';
import { KpiTile } from '../../../shared/stats/kpi-tile';
import { PeriodPicker } from '../../../shared/stats/period-picker';
import { StatsTable } from '../../../shared/stats/stats-table';

/**
 * An affiliate's results on their admin user page (023): orders with their code and what became of them, coins
 * earned (per store and per product), partner stores. Numbers open the orders / ledger / partnerships behind them.
 * The period lives in the component (the user page's URL is not a stats URL).
 */
@Component({
  selector: 'app-affiliate-stats',
  imports: [DecimalPipe, TranslocoPipe, ConversionTiles, KpiTile, PeriodPicker, StatsTable],
  template: `
    <section class="mt-8">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <h2 class="section-title">{{ 'stats.affiliateTitle' | transloco }}</h2>
        <app-period-picker [from]="period().from" [to]="period().to" (changed)="period.set($event)" />
      </div>
      @if (stats.error()) {
        <p class="alert-danger mt-4">{{ 'errors.unknown' | transloco }}</p>
      } @else if (stats.value(); as s) {
        <div class="mt-4">
          <app-conversion-tiles [orders]="s.orders" [sales]="s.sales" [period]="s.period" [ordersQuery]="{ affiliateId: affiliateId() }">
            <app-kpi [label]="'stats.kpi.coinsEarned' | transloco" link="/admin/coins"
              [query]="{ affiliateId: affiliateId(), type: 'Earned', from: s.period.from, to: s.period.to }"
              [hint]="'stats.kpi.expectedNow' | transloco: { n: (s.coinsExpected | number: '1.0-3' : language.locale()) }">
              {{ s.coinsEarned | number: '1.0-3' : language.locale() }}
            </app-kpi>
            <app-kpi [label]="'stats.kpi.partnerStores' | transloco" link="/admin/partnerships" [query]="{ affiliateId: affiliateId() }"
              [hint]="'stats.kpi.partnersHint' | transloco: { requested: s.partnerships.requested, refused: s.partnerships.refused, ended: s.partnerships.ended }">
              {{ s.partnerships.accepted | number: '1.0-0' : language.locale() }}
            </app-kpi>
          </app-conversion-tiles>
        </div>
        <div class="mt-4 grid gap-4">
          <app-stats-table [title]="'stats.coinsByStore' | transloco" [rows]="s.byStore" nameKey="stats.col.store" countKey="stats.col.lines"
            linkBase="/admin/stores" csvName="affiliate-stores" />
          <app-stats-table [title]="'stats.coinsByProduct' | transloco" [rows]="s.byProduct" nameKey="stats.col.product" countKey="stats.col.lines"
            linkBase="/admin/products" csvName="affiliate-products" />
        </div>
      } @else {
        <p class="empty-state mt-4">{{ 'common.loading' | transloco }}</p>
      }
    </section>
  `,
})
export class AffiliateStats {
  private readonly api = inject(StatsApi);
  protected readonly language = inject(LanguageService);

  readonly affiliateId = input.required<string>();
  protected readonly period = signal<PeriodQuery>({});

  protected readonly stats = rxResource({
    params: () => ({ id: this.affiliateId(), ...this.period() }),
    stream: ({ params }) => this.api.affiliate(params.id, params),
  });
}
