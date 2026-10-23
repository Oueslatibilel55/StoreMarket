import { DecimalPipe } from '@angular/common';
import { Component, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../../core/i18n/language.service';
import { PeriodQuery, StatsApi } from '../../../core/stats/stats-api';
import { ConversionTiles } from '../../../shared/stats/conversion-tiles';
import { KpiTile } from '../../../shared/stats/kpi-tile';
import { PeriodPicker } from '../../../shared/stats/period-picker';
import { StatsTable } from '../../../shared/stats/stats-table';

/** `/admin/products/:id?from=&to=` — one product's results (023): sales, orders via affiliates, coins earned on it. */
@Component({
  selector: 'app-product-stats-page',
  imports: [DecimalPipe, RouterLink, TranslocoPipe, ConversionTiles, KpiTile, PeriodPicker, StatsTable],
  template: `
    <a routerLink="/admin/products" class="link text-sm">← {{ 'stats.backToProducts' | transloco }}</a>
    @if (stats.error()) {
      <p class="alert-danger mt-6">{{ 'errors.product.notFound' | transloco }}</p>
    } @else if (stats.value(); as p) {
      <header class="mt-3 flex flex-wrap items-end justify-between gap-4">
        <div class="min-w-0">
          <h1 class="page-title break-words">{{ p.name }}</h1>
          <p class="page-subtitle flex flex-wrap items-center gap-2">
            <a [routerLink]="['/admin/stores', p.storeId]" class="link">{{ p.storeName }}</a>
            <span dir="ltr">{{ p.price | number: '1.0-3' : language.locale() }} TND</span>
            <span class="pill pill-brand" dir="ltr">{{ p.affiliatePercent }} %</span>
            <span class="pill" [class]="p.isVisible ? 'pill-success' : 'pill-neutral'">{{ (p.isVisible ? 'stats.visible' : 'stats.hidden') | transloco }}</span>
            @if (p.isVisible) { <a [routerLink]="['/products', p.slug]" class="link">{{ 'stats.seeInShop' | transloco }}</a> }
          </p>
        </div>
        <app-period-picker [from]="from()" [to]="to()" (changed)="setPeriod($event)" />
      </header>

      <div class="mt-6">
        <app-conversion-tiles [orders]="p.orders" [sales]="p.sales" [period]="p.period" [ordersQuery]="{ productId: p.id }">
          <app-kpi [label]="'stats.kpi.coinsEarnedHere' | transloco" link="/admin/coins"
            [query]="{ productId: p.id, type: 'Earned', from: p.period.from, to: p.period.to }">
            {{ p.coinsEarned | number: '1.0-3' : language.locale() }}
          </app-kpi>
          <app-kpi [label]="'stats.kpi.rating' | transloco" [hint]="'stats.kpi.ratingCount' | transloco: { n: p.rating.count }">
            {{ p.rating.average !== null ? (p.rating.average! | number: '1.1-2' : language.locale()) + ' ★' : '—' }}
          </app-kpi>
        </app-conversion-tiles>
      </div>

      <div class="mt-4">
        <app-stats-table [title]="'stats.topAffiliates' | transloco" [rows]="p.topAffiliates" nameKey="stats.col.affiliate" countKey="stats.col.lines"
          [detailLtr]="true" linkBase="/admin/users" csvName="product-affiliates" />
      </div>
    } @else {
      <p class="empty-state mt-6">{{ 'common.loading' | transloco }}</p>
    }
  `,
})
export class ProductStatsPage {
  private readonly api = inject(StatsApi);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  /** Route param :id */
  readonly id = input.required<string>();
  readonly from = input<string | undefined>();
  readonly to = input<string | undefined>();

  protected readonly stats = rxResource({
    params: () => ({ id: this.id(), from: this.from(), to: this.to() }),
    stream: ({ params }) => this.api.product(params.id, params),
  });

  protected setPeriod(q: PeriodQuery): void {
    this.router.navigate([], { queryParams: { from: q.from ?? null, to: q.to ?? null }, queryParamsHandling: 'merge' });
  }
}
