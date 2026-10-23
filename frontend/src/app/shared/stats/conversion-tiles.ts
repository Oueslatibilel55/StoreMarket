import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { Params } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../core/i18n/language.service';
import { ConversionStats, percent, SalesStats, StatsPeriod } from '../../core/stats/stats-api';
import { KpiTile } from './kpi-tile';

/**
 * Orders and sales tiles shared by every stats screen (023): placed / delivered / not delivered / cancelled
 * (orders placed in the period) and sales (delivered in the period). With `ordersQuery` (admin) each tile opens
 * the admin order list filtered on the same orders.
 */
@Component({
  selector: 'app-conversion-tiles',
  imports: [DecimalPipe, TranslocoPipe, KpiTile],
  template: `
    <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
      <app-kpi [label]="'stats.kpi.placed' | transloco" [link]="link()" [query]="q({})"
        [hint]="'stats.kpi.openHint' | transloco: { n: orders().open }">{{ orders().placed | number: '1.0-0' : language.locale() }}</app-kpi>
      <app-kpi [label]="'stats.kpi.delivered' | transloco" [link]="link()" [query]="q({ status: 'Delivered' })"
        [hint]="'stats.kpi.rate' | transloco: { n: rate(orders().delivered) }">{{ orders().delivered | number: '1.0-0' : language.locale() }}</app-kpi>
      <app-kpi [label]="'stats.kpi.notDelivered' | transloco" [link]="link()" [query]="q({ status: 'NotDelivered' })"
        [hint]="'stats.kpi.rate' | transloco: { n: rate(orders().notDelivered) }">{{ orders().notDelivered | number: '1.0-0' : language.locale() }}</app-kpi>
      <app-kpi [label]="'stats.kpi.cancelled' | transloco" [link]="link()" [query]="q({ status: 'Cancelled' })"
        [hint]="'stats.kpi.rate' | transloco: { n: rate(orders().cancelled) }">{{ orders().cancelled | number: '1.0-0' : language.locale() }}</app-kpi>
      <app-kpi [label]="'stats.kpi.sales' | transloco" [link]="link()" [query]="q({ dateField: 'Delivered' }, true)"
        [hint]="'stats.kpi.salesHint' | transloco: { orders: sales().orders, quantity: sales().quantity }">{{ sales().amount | number: '1.0-3' : language.locale() }} TND</app-kpi>
      <app-kpi [label]="'stats.kpi.viaAffiliates' | transloco"
        [hint]="'stats.kpi.viaHint' | transloco: { orders: sales().ordersViaAffiliates, n: viaShare() }">{{ sales().amountViaAffiliates | number: '1.0-3' : language.locale() }} TND</app-kpi>
      <ng-content />
    </div>
  `,
})
export class ConversionTiles {
  protected readonly language = inject(LanguageService);

  readonly orders = input.required<ConversionStats>();
  readonly sales = input.required<SalesStats>();
  readonly period = input.required<StatsPeriod>();
  /** Filters of the admin order list for this scope (`{ storeId }`, `{ affiliateId }`…); none = no links. */
  readonly ordersQuery = input<Params | null>(null);

  protected readonly link = computed(() => (this.ordersQuery() ? '/admin/orders' : null));
  protected readonly viaShare = computed(() => percent(this.sales().amountViaAffiliates, this.sales().amount));

  protected rate(part: number): number {
    return percent(part, this.orders().placed);
  }

  /** The same orders as the tile: placed in the period, or delivered in it for the sales tile. */
  protected q(extra: Params, delivered = false): Params | null {
    const scope = this.ordersQuery();
    if (!scope) return null;
    return { ...scope, from: this.period().from, to: this.period().to, ...(delivered ? { status: 'Delivered' } : {}), ...extra };
  }
}
