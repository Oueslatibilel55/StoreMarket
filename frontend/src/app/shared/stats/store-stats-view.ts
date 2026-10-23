import { DecimalPipe } from '@angular/common';
import { Component, inject, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../core/i18n/language.service';
import { StoreStats } from '../../core/stats/stats-api';
import { ConversionTiles } from './conversion-tiles';
import { KpiTile } from './kpi-tile';
import { StatsTable } from './stats-table';

/**
 * A store's numbers (023) — on the admin's store page (`admin` = links into the admin lists) and on the owner's
 * dashboard (no links): orders, sales, coins given to affiliates (per product too), rating, partners.
 */
@Component({
  selector: 'app-store-stats-view',
  imports: [DecimalPipe, TranslocoPipe, ConversionTiles, KpiTile, StatsTable],
  template: `
    <app-conversion-tiles [orders]="stats().orders" [sales]="stats().sales" [period]="stats().period"
      [ordersQuery]="admin() ? { storeId: storeId() } : null">
      <app-kpi [label]="'stats.kpi.coinsGenerated' | transloco" [link]="admin() ? '/admin/coins' : null"
        [query]="{ storeId: storeId(), type: 'Earned', from: stats().period.from, to: stats().period.to }">
        {{ stats().coinsGenerated | number: '1.0-3' : language.locale() }}
      </app-kpi>
      <app-kpi [label]="'stats.kpi.rating' | transloco" [hint]="'stats.kpi.ratingCount' | transloco: { n: stats().rating.count }">
        {{ stats().rating.average !== null ? (stats().rating.average! | number: '1.1-2' : language.locale()) + ' ★' : '—' }}
      </app-kpi>
      <app-kpi [label]="'stats.kpi.partners' | transloco" [link]="admin() ? '/admin/partnerships' : null" [query]="{ storeId: storeId() }"
        [hint]="'stats.kpi.pendingRequests' | transloco: { n: stats().partnerships.requested }">
        {{ stats().partnerships.accepted | number: '1.0-0' : language.locale() }}
      </app-kpi>
    </app-conversion-tiles>

    <div class="mt-4 grid gap-4">
      <app-stats-table [title]="'stats.topProducts' | transloco" [rows]="stats().topProducts" nameKey="stats.col.product"
        [linkBase]="admin() ? '/admin/products' : null" csvName="products" />
      <app-stats-table [title]="'stats.topAffiliates' | transloco" [rows]="stats().topAffiliates" nameKey="stats.col.affiliate"
        countKey="stats.col.lines" [detailLtr]="true" [linkBase]="admin() ? '/admin/users' : null" csvName="affiliates" />
    </div>
  `,
})
export class StoreStatsView {
  protected readonly language = inject(LanguageService);

  readonly stats = input.required<StoreStats>();
  readonly storeId = input.required<string>();
  readonly admin = input(false);
}
