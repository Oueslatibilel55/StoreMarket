import { DecimalPipe } from '@angular/common';
import { Component, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../../core/i18n/language.service';
import { PeriodQuery, StatsApi } from '../../../core/stats/stats-api';
import { ConversionTiles } from '../../../shared/stats/conversion-tiles';
import { DailyChart } from '../../../shared/stats/daily-chart';
import { KpiTile } from '../../../shared/stats/kpi-tile';
import { PeriodPicker } from '../../../shared/stats/period-picker';
import { StatsTable } from '../../../shared/stats/stats-table';

/**
 * `/admin?from=&to=` — the platform at a glance (023): orders and sales of the period, coins and payouts, stores,
 * a daily chart and the best stores / products / affiliates. Every tile opens the list behind its number.
 */
@Component({
  selector: 'app-admin-dashboard',
  imports: [DecimalPipe, TranslocoPipe, ConversionTiles, DailyChart, KpiTile, PeriodPicker, StatsTable],
  template: `
    <header class="page-header">
      <div class="min-w-0">
        <h1 class="page-title">{{ 'stats.title' | transloco }}</h1>
        <p class="page-subtitle">{{ 'stats.subtitle' | transloco }}</p>
      </div>
      <app-period-picker [from]="from()" [to]="to()" (changed)="setPeriod($event)" />
    </header>

    @if (overview.error()) {
      <p class="alert-danger">{{ 'errors.unknown' | transloco }}</p>
    } @else if (overview.value(); as o) {
      <div class="space-y-10">
        <section>
          <h2 class="section-title">{{ 'stats.ordersAndSales' | transloco }}</h2>
          <div class="mt-4">
            <app-conversion-tiles [orders]="o.orders" [sales]="o.sales" [period]="o.period" [ordersQuery]="{}" />
          </div>
        </section>

        <section>
          <h2 class="section-title">{{ 'stats.coinsAndPayouts' | transloco }}</h2>
          <div class="mt-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
            <app-kpi [label]="'stats.kpi.coinsEarned' | transloco" link="/admin/coins" [query]="{ type: 'Earned', from: o.period.from, to: o.period.to }"
              [hint]="'stats.kpi.adjusted' | transloco: { n: (o.coins.adjusted | number: '1.0-3' : language.locale()) }">
              <span class="text-success">{{ o.coins.earned | number: '1.0-3' : language.locale() }}</span>
            </app-kpi>
            <app-kpi [label]="'stats.kpi.coinsExpected' | transloco" [hint]="'stats.kpi.expectedHint' | transloco">
              {{ o.coins.expected | number: '1.0-3' : language.locale() }}
            </app-kpi>
            <app-kpi [label]="'stats.kpi.payoutsPending' | transloco" link="/admin/payouts"
              [hint]="'stats.kpi.payoutsPendingHint' | transloco: { coins: (o.payouts.pendingCoins | number: '1.0-3' : language.locale()), tnd: (o.payouts.pendingTnd | number: '1.0-3' : language.locale()) }">
              <span [class.text-accent-ink]="o.payouts.pending > 0">{{ o.payouts.pending | number: '1.0-0' : language.locale() }}</span>
            </app-kpi>
            <app-kpi [label]="'stats.kpi.paidOut' | transloco" link="/admin/payouts" [query]="{ status: 'Paid', from: o.period.from, to: o.period.to }"
              [hint]="'stats.kpi.paidOutHint' | transloco: { n: o.payouts.paid, coins: (o.coins.paidOut | number: '1.0-3' : language.locale()) }">
              {{ o.coins.paidOutTnd | number: '1.0-3' : language.locale() }} TND
            </app-kpi>
          </div>
        </section>

        <section>
          <h2 class="section-title">{{ 'stats.billing' | transloco }}</h2>
          <div class="mt-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
            <app-kpi [label]="'stats.kpi.subscriptionsCollected' | transloco" link="/admin/billing" [query]="{ tab: 'revenue', from: o.period.from, to: o.period.to }">
              {{ o.billing.subscriptionsCollected | number: '1.0-3' : language.locale() }} TND
            </app-kpi>
            <app-kpi [label]="'stats.kpi.commissionsBilled' | transloco" link="/admin/billing" [query]="{ tab: 'revenue', from: o.period.from, to: o.period.to }"
              [hint]="'stats.kpi.commissionsCollected' | transloco: { n: (o.billing.commissionsCollected | number: '1.0-3' : language.locale()) }">
              {{ o.billing.commissionsBilled | number: '1.0-3' : language.locale() }} TND
            </app-kpi>
            <app-kpi [label]="'stats.kpi.commissionsDue' | transloco" link="/admin/billing" [query]="{ tab: 'overdue' }">
              <span [class.text-danger]="o.billing.commissionsDue > 0">{{ o.billing.commissionsDue | number: '1.0-3' : language.locale() }} TND</span>
            </app-kpi>
            <app-kpi [label]="'stats.kpi.declaredPayments' | transloco" link="/admin/billing">
              <span [class.text-accent-ink]="o.billing.declaredPayments > 0">{{ o.billing.declaredPayments | number: '1.0-0' : language.locale() }}</span>
            </app-kpi>
          </div>
        </section>

        <section>
          <h2 class="section-title">{{ 'stats.platform' | transloco }}</h2>
          <div class="mt-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
            <app-kpi [label]="'stats.kpi.activeStores' | transloco" link="/admin/stores" [query]="{ status: 'Active' }"
              [hint]="'stats.kpi.closedStores' | transloco: { n: o.stores.closed }">{{ o.stores.active | number: '1.0-0' : language.locale() }}</app-kpi>
            <app-kpi [label]="'stats.kpi.suspendedStores' | transloco" link="/admin/stores" [query]="{ status: 'Suspended' }"
              [hint]="'stats.kpi.disabledStores' | transloco: { n: o.stores.disabled }">{{ o.stores.suspended | number: '1.0-0' : language.locale() }}</app-kpi>
            <app-kpi [label]="'stats.kpi.activeAffiliates' | transloco" [hint]="'stats.kpi.activeAffiliatesHint' | transloco">
              {{ o.activeAffiliates | number: '1.0-0' : language.locale() }}
            </app-kpi>
            <app-kpi [label]="'stats.kpi.newUsers' | transloco" link="/admin/users">{{ o.newUsers | number: '1.0-0' : language.locale() }}</app-kpi>
          </div>
        </section>

        <app-daily-chart [daily]="o.daily" />

        <div class="grid gap-6">
          <app-stats-table [title]="'stats.topStores' | transloco" [rows]="o.topStores" nameKey="stats.col.store" linkBase="/admin/stores" csvName="stores" />
          <app-stats-table [title]="'stats.topProducts' | transloco" [rows]="o.topProducts" nameKey="stats.col.product" linkBase="/admin/products" csvName="products" />
          <app-stats-table [title]="'stats.topAffiliates' | transloco" [rows]="o.topAffiliates" nameKey="stats.col.affiliate" countKey="stats.col.lines"
            [detailLtr]="true" linkBase="/admin/users" csvName="affiliates" />
        </div>
      </div>
    } @else {
      <p class="py-14 text-center text-sm text-muted">{{ 'common.loading' | transloco }}</p>
    }
  `,
})
export class AdminDashboard {
  private readonly api = inject(StatsApi);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly from = input<string | undefined>();
  readonly to = input<string | undefined>();

  protected readonly overview = rxResource({
    params: () => ({ from: this.from(), to: this.to() }),
    stream: ({ params }) => this.api.overview(params),
  });

  protected setPeriod(q: PeriodQuery): void {
    this.router.navigate([], { queryParams: { from: q.from ?? null, to: q.to ?? null }, queryParamsHandling: 'merge' });
  }
}
