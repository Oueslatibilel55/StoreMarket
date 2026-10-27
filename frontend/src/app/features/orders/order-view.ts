import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../core/i18n/language.service';
import { Order, orderStatusTone, StoreOrder } from '../../core/orders/order-api';
import { OrderTimeline } from '../../shared/orders/order-timeline';
import { LocalizedPipe } from '../../shared/i18n/localized';

/** An order as its client sees it: one block per store (what it delivers and collects), delivery details, totals. */
@Component({
  selector: 'app-order-view',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe, LocalizedPipe, OrderTimeline],
  template: `
    @let o = order();
    <div class="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_22rem] lg:gap-8">
      <div class="min-w-0 space-y-6">
        @for (s of o.storeOrders; track s.id) {
          <section class="card overflow-hidden">
            <div class="card-header bg-subtle/50">
              <div class="min-w-0">
                <p class="text-xs text-muted">{{ 'orders.deliveredBy' | transloco }}</p>
                @if (s.storeSlug) {
                  <a [routerLink]="['/stores', s.storeSlug]" class="font-display font-bold text-ink hover:text-brand">{{ s.storeName }}</a>
                } @else {
                  <span class="font-display font-bold text-ink">{{ s.storeName }}</span>
                }
              </div>
              <span class="flex flex-wrap items-center gap-2">
                @if (s.paymentStatus === 'Collected') { <span class="pill pill-success">{{ 'orders.paid' | transloco }}</span> }
                <span class="pill" [class]="tone(s.status)">{{ 'orders.status.' + s.status | transloco }}</span>
              </span>
            </div>
            <ul class="divide-y divide-line">
              @for (l of s.lines; track l.id) {
                <li class="flex gap-4 px-5 py-4 sm:px-6">
                  <a [routerLink]="['/products', l.productSlug]" class="size-16 shrink-0 overflow-hidden rounded-lg border border-line bg-subtle sm:size-20">
                    @if (l.imageUrl) { <img [src]="l.imageUrl" [alt]="l.productName" class="size-full object-cover" /> }
                  </a>
                  <div class="min-w-0 flex-1">
                    <div class="flex flex-col gap-1 sm:flex-row sm:items-start sm:justify-between sm:gap-4">
                      <p class="min-w-0 font-semibold break-words text-ink">{{ l.productName }}</p>
                      <p class="figure shrink-0 text-ink" dir="ltr">{{ l.lineTotal | number: '1.0-3' : language.locale() }} <span class="text-xs font-semibold text-muted">TND</span></p>
                    </div>
                    @if (l.options.length) {
                      <p class="mt-0.5 flex flex-wrap gap-x-3 text-sm text-muted">
                        @for (op of l.options; track $index) { <span>{{ op.axis | localized: language.lang() }} : {{ op.value | localized: language.lang() }}</span> }
                      </p>
                    }
                    <p class="mt-1 flex flex-wrap items-center gap-2 text-sm text-muted">
                      <span dir="ltr" class="tabular-nums">{{ l.quantity }} × {{ l.unitPrice | number: '1.0-3' : language.locale() }} TND</span>
                      @if (l.affiliateCode) { <span class="code-chip" dir="ltr">#{{ l.affiliateCode }}</span> }
                    </p>
                  </div>
                </li>
              }
            </ul>
            <dl class="space-y-1.5 border-t border-line px-5 py-4 text-sm sm:px-6">
              <div class="flex justify-between gap-4"><dt class="text-muted">{{ 'cart.deliveryFee' | transloco }}</dt><dd class="tabular-nums" dir="ltr">{{ s.deliveryFee | number: '1.0-3' : language.locale() }} TND</dd></div>
              <div class="flex items-baseline justify-between gap-4"><dt class="font-semibold text-ink">{{ 'orders.toCollect' | transloco }}</dt><dd class="figure text-lg text-ink" dir="ltr">{{ s.amountToCollect | number: '1.0-3' : language.locale() }} <span class="text-xs font-semibold text-muted">TND</span></dd></div>
            </dl>
            @if (s.history.length || (cancellable() && s.canCancel)) {
              <div class="flex flex-col gap-4 border-t border-line px-5 py-4 sm:flex-row sm:items-start sm:justify-between sm:px-6">
                <app-order-timeline [history]="s.history" />
                @if (cancellable() && s.canCancel) {
                  <button type="button" (click)="cancel.emit(s)" class="btn btn-danger-soft btn-sm self-start">{{ 'orders.cancel' | transloco }}</button>
                }
                @if (cancellable() && s.status === 'Delivered') {
                  <a routerLink="/reviews" class="btn btn-secondary btn-sm self-start">{{ 'reviews.rateOrder' | transloco }}</a>
                }
              </div>
            }
          </section>
        }
      </div>

      <aside class="space-y-6 lg:sticky lg:top-24">
        <section class="card">
          <div class="card-header"><h2 class="section-title">{{ 'orders.deliveryTo' | transloco }}</h2></div>
          <div class="card-body space-y-4">
            <dl class="detail-grid lg:grid-cols-1">
              <div><dt>{{ 'checkout.fullName' | transloco }}</dt><dd class="break-words">{{ o.clientName }}</dd></div>
              <div><dt>{{ 'auth.phone' | transloco }}</dt><dd><span dir="ltr">{{ o.clientPhone }}</span></dd></div>
              <div><dt>{{ 'checkout.address' | transloco }}</dt><dd class="break-words whitespace-pre-line">{{ o.address }}<br />{{ 'cities.' + o.city | transloco }}</dd></div>
              @if (o.note) { <div><dt>{{ 'checkout.note' | transloco }}</dt><dd class="font-normal break-words text-ink-soft">{{ o.note }}</dd></div> }
            </dl>
            @if (o.placedByAffiliate) { <p class="pill pill-brand">{{ 'orders.placedByAffiliate' | transloco }}</p> }
          </div>
        </section>
        <section class="card">
          <div class="card-body">
            <dl class="space-y-2.5 text-sm">
              <div class="flex justify-between gap-4"><dt class="text-muted">{{ 'orders.date' | transloco }}</dt><dd class="text-end">{{ o.createdAt | date: 'medium' : undefined : language.locale() }}</dd></div>
              <div class="flex justify-between gap-4"><dt class="text-muted">{{ 'cart.items' | transloco }}</dt><dd class="tabular-nums" dir="ltr">{{ o.itemsTotal | number: '1.0-3' : language.locale() }} TND</dd></div>
              <div class="flex justify-between gap-4"><dt class="text-muted">{{ 'cart.delivery' | transloco: { count: o.storeOrders.length } }}</dt><dd class="tabular-nums" dir="ltr">{{ o.deliveryTotal | number: '1.0-3' : language.locale() }} TND</dd></div>
              <div class="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 border-t border-line pt-4">
                <dt class="font-semibold text-ink">{{ 'checkout.toPay' | transloco }}</dt>
                <dd class="figure text-3xl text-ink" dir="ltr">{{ o.total | number: '1.0-3' : language.locale() }} <span class="text-sm font-semibold text-muted">TND</span></dd>
              </div>
            </dl>
            <p class="alert-info mt-5 text-xs">{{ 'orders.codReminder' | transloco }}</p>
          </div>
        </section>
      </aside>
    </div>
  `,
})
export class OrderView {
  readonly order = input.required<Order>();
  /** The viewer may cancel (the signed-in client or the affiliate who placed it). */
  readonly cancellable = input(false);
  readonly cancel = output<StoreOrder>();
  protected readonly tone = orderStatusTone;
  protected readonly language = inject(LanguageService);
}
