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
    <div class="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
      <div class="space-y-6">
        @for (s of o.storeOrders; track s.id) {
          <section class="card overflow-hidden">
            <div class="card-header">
              <div class="min-w-0">
                @if (s.storeSlug) {
                  <a [routerLink]="['/stores', s.storeSlug]" class="font-semibold text-ink hover:text-brand">{{ s.storeName }}</a>
                } @else {
                  <span class="font-semibold text-ink">{{ s.storeName }}</span>
                }
                <p class="text-xs text-muted">{{ 'orders.deliveredBy' | transloco }}</p>
              </div>
              <span class="flex flex-wrap items-center gap-2">
                @if (s.paymentStatus === 'Collected') { <span class="pill pill-success">{{ 'orders.paid' | transloco }}</span> }
                <span class="pill" [class]="tone(s.status)">{{ 'orders.status.' + s.status | transloco }}</span>
              </span>
            </div>
            <ul class="divide-y divide-line">
              @for (l of s.lines; track l.id) {
                <li class="flex gap-4 px-5 py-4 sm:px-6">
                  <a [routerLink]="['/products', l.productSlug]" class="size-16 shrink-0 overflow-hidden rounded-lg bg-subtle ring-1 ring-line">
                    @if (l.imageUrl) { <img [src]="l.imageUrl" [alt]="l.productName" class="size-full object-cover" /> }
                  </a>
                  <div class="min-w-0 flex-1">
                    <p class="font-semibold text-ink">{{ l.productName }}</p>
                    @if (l.options.length) {
                      <p class="text-sm text-muted">
                        @for (op of l.options; track $index) { {{ op.axis | localized: language.lang() }} : {{ op.value | localized: language.lang() }}@if (!$last) { · } }
                      </p>
                    }
                    <p class="mt-1 flex flex-wrap items-center gap-2 text-sm text-muted">
                      <span dir="ltr">{{ l.quantity }} × {{ l.unitPrice | number: '1.0-3' : language.locale() }} TND</span>
                      @if (l.affiliateCode) { <span class="code-chip" dir="ltr">#{{ l.affiliateCode }}</span> }
                    </p>
                  </div>
                  <p class="shrink-0 font-semibold text-ink" dir="ltr">{{ l.lineTotal | number: '1.0-3' : language.locale() }} TND</p>
                </li>
              }
            </ul>
            <dl class="space-y-1.5 border-t border-line bg-subtle/40 px-5 py-3 text-sm sm:px-6">
              <div class="flex justify-between"><dt class="text-muted">{{ 'cart.deliveryFee' | transloco }}</dt><dd dir="ltr">{{ s.deliveryFee | number: '1.0-3' : language.locale() }} TND</dd></div>
              <div class="flex justify-between font-semibold"><dt>{{ 'orders.toCollect' | transloco }}</dt><dd dir="ltr">{{ s.amountToCollect | number: '1.0-3' : language.locale() }} TND</dd></div>
            </dl>
            @if (s.history.length || (cancellable() && s.canCancel)) {
              <div class="flex flex-col gap-4 border-t border-line px-5 py-4 sm:flex-row sm:items-start sm:justify-between sm:px-6">
                <app-order-timeline [history]="s.history" />
                @if (cancellable() && s.canCancel) {
                  <button type="button" (click)="cancel.emit(s)" class="btn btn-danger-soft btn-sm self-start">{{ 'orders.cancel' | transloco }}</button>
                }
              </div>
            }
          </section>
        }
      </div>

      <aside class="space-y-6 lg:sticky lg:top-24">
        <section class="card card-body space-y-3 text-sm">
          <h2 class="section-title">{{ 'orders.deliveryTo' | transloco }}</h2>
          <p class="font-semibold text-ink">{{ o.clientName }}</p>
          <p class="text-ink-soft" dir="ltr">{{ o.clientPhone }}</p>
          <p class="whitespace-pre-line text-ink-soft">{{ o.address }}<br />{{ 'cities.' + o.city | transloco }}</p>
          @if (o.note) { <p class="rounded-lg bg-subtle px-3 py-2 text-ink-soft">{{ o.note }}</p> }
          @if (o.placedByAffiliate) { <p class="pill pill-brand">{{ 'orders.placedByAffiliate' | transloco }}</p> }
        </section>
        <section class="card card-body">
          <dl class="space-y-2 text-sm">
            <div class="flex justify-between"><dt class="text-muted">{{ 'orders.date' | transloco }}</dt><dd>{{ o.createdAt | date: 'medium' : undefined : language.locale() }}</dd></div>
            <div class="flex justify-between"><dt class="text-muted">{{ 'cart.items' | transloco }}</dt><dd dir="ltr">{{ o.itemsTotal | number: '1.0-3' : language.locale() }} TND</dd></div>
            <div class="flex justify-between"><dt class="text-muted">{{ 'cart.delivery' | transloco: { count: o.storeOrders.length } }}</dt><dd dir="ltr">{{ o.deliveryTotal | number: '1.0-3' : language.locale() }} TND</dd></div>
            <div class="flex items-baseline justify-between border-t border-line pt-3">
              <dt class="font-semibold text-ink">{{ 'checkout.toPay' | transloco }}</dt>
              <dd class="text-xl font-bold tracking-tight" dir="ltr">{{ o.total | number: '1.0-3' : language.locale() }} TND</dd>
            </div>
          </dl>
          <p class="mt-4 rounded-lg bg-subtle px-3 py-2 text-xs text-ink-soft">💵 {{ 'orders.codReminder' | transloco }}</p>
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
