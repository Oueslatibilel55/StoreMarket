import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { NotDeliveredReason, orderStatusTone, StoreOrderStatus } from '../../../core/orders/order-api';
import { StoreOrderApi } from '../../../core/orders/store-order-api';
import { Seo } from '../../../core/seo/seo';
import { LocalizedPipe } from '../../../shared/i18n/localized';
import { OrderTimeline } from '../../../shared/orders/order-timeline';
import { Modal } from '../../../shared/ui/modal';
import { MyStoreTabs } from '../my-store-tabs';

const NOT_DELIVERED: NotDeliveredReason[] = ['Refused', 'Unreachable', 'WrongAddress', 'Other'];

/**
 * `/my-store/orders/:id` — prepare, ship and deliver one order (016). Delivered means the cash was collected
 * (015) and is final; a refused or returned parcel is "not delivered", with a reason. Both put nothing to pay.
 */
@Component({
  selector: 'app-store-order-detail',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe, LocalizedPipe, OrderTimeline, Modal, MyStoreTabs],
  template: `
    <main class="page max-w-5xl animate-fade-in">
      <app-my-store-tabs />
      <div><a routerLink="/my-store/orders" class="btn btn-ghost btn-sm -ms-3"><span aria-hidden="true" class="inline-block rtl:rotate-180">←</span> {{ 'storeOrders.title' | transloco }}</a></div>

      @if (order.value(); as o) {
        <header class="mt-2 flex flex-wrap items-end justify-between gap-4">
          <div>
            <h1 class="page-title"><span dir="ltr">{{ o.reference }}</span></h1>
            <p class="page-subtitle">{{ o.createdAt | date: 'medium' : undefined : language.locale() }}</p>
          </div>
          <p class="flex flex-wrap items-center gap-2">
            <span class="pill px-3 py-1 text-sm" [class]="tone(o.status)">{{ 'orders.status.' + o.status | transloco }}</span>
            <span class="pill px-3 py-1 text-sm" [class]="o.paymentStatus === 'Collected' ? 'pill-success' : 'pill-neutral'">{{ 'orders.payment.' + o.paymentStatus | transloco }}</span>
          </p>
        </header>

        @if (o.next.length) {
          <section class="card card-body mt-6 flex flex-col gap-4 border-brand/20 sm:flex-row sm:items-center sm:justify-between">
            <p class="text-sm text-ink-soft">{{ 'storeOrders.nextHint.' + o.status | transloco }}</p>
            <div class="flex flex-wrap gap-2">
              @for (to of o.next; track to) {
                <button type="button" (click)="ask(to)" class="btn btn-sm"
                  [class]="to === 'Cancelled' || to === 'NotDelivered' ? 'btn-danger-soft' : to === 'Delivered' ? 'btn-success' : 'btn-primary'">
                  {{ 'storeOrders.do.' + to | transloco }}
                </button>
              }
            </div>
          </section>
        }

        <div class="mt-6 grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
          <section class="card overflow-hidden">
            <div class="card-header"><h2 class="section-title">{{ 'storeOrders.toPrepare' | transloco }}</h2></div>
            <ul class="divide-y divide-line">
              @for (l of o.lines; track l.id) {
                <li class="flex gap-4 px-5 py-4 sm:px-6">
                  <span class="size-14 shrink-0 overflow-hidden rounded-lg bg-subtle ring-1 ring-line">
                    @if (l.imageUrl) { <img [src]="l.imageUrl" [alt]="l.productName" class="size-full object-cover" /> }
                  </span>
                  <div class="min-w-0 flex-1">
                    <p class="font-semibold text-ink"><span dir="ltr">{{ l.quantity }} ×</span> {{ l.productName }}</p>
                    @if (l.options.length) {
                      <p class="text-sm text-muted">@for (op of l.options; track $index) { {{ op.axis | localized: language.lang() }} : {{ op.value | localized: language.lang() }}@if (!$last) { · } }</p>
                    }
                    @if (l.affiliateCode) {
                      <p class="mt-1 flex flex-wrap items-center gap-2 text-xs text-muted">
                        <span class="code-chip" dir="ltr">#{{ l.affiliateCode }}</span>
                        {{ 'storeOrders.commission' | transloco: { percent: l.affiliatePercent } }}
                        <span class="font-semibold text-ink-soft" dir="ltr">{{ l.commission | number: '1.0-3' : language.locale() }} TND</span>
                      </p>
                    }
                  </div>
                  <p class="shrink-0 font-semibold text-ink" dir="ltr">{{ l.lineTotal | number: '1.0-3' : language.locale() }} TND</p>
                </li>
              }
            </ul>
            <dl class="space-y-1.5 border-t border-line bg-subtle/40 px-5 py-3 text-sm sm:px-6">
              <div class="flex justify-between"><dt class="text-muted">{{ 'cart.items' | transloco }}</dt><dd dir="ltr">{{ o.subtotal | number: '1.0-3' : language.locale() }} TND</dd></div>
              <div class="flex justify-between"><dt class="text-muted">{{ 'cart.deliveryFee' | transloco }}</dt><dd dir="ltr">{{ o.deliveryFee | number: '1.0-3' : language.locale() }} TND</dd></div>
              <div class="flex justify-between text-base font-bold"><dt>{{ 'orders.toCollect' | transloco }}</dt><dd dir="ltr">{{ o.amountToCollect | number: '1.0-3' : language.locale() }} TND</dd></div>
            </dl>
          </section>

          <aside class="space-y-6">
            <section class="card card-body space-y-2 text-sm">
              <h2 class="section-title">{{ 'orders.deliveryTo' | transloco }}</h2>
              <p class="font-semibold text-ink">{{ o.clientName }}</p>
              <a [href]="'tel:' + o.clientPhone" class="link block" dir="ltr">{{ o.clientPhone }}</a>
              <p class="whitespace-pre-line text-ink-soft">{{ o.address }}<br />{{ 'cities.' + o.city | transloco }}</p>
              @if (o.note) { <p class="rounded-lg bg-subtle px-3 py-2 text-ink-soft">{{ o.note }}</p> }
              @if (o.placedByAffiliate) { <p class="pill pill-brand">{{ 'orders.placedByAffiliateForClient' | transloco }}</p> }
            </section>
            <section class="card card-body">
              <h2 class="section-title mb-4">{{ 'orders.history' | transloco }}</h2>
              <app-order-timeline [history]="o.history" />
              @if (!o.history.length) { <p class="text-sm text-muted">{{ 'storeOrders.noHistory' | transloco }}</p> }
            </section>
          </aside>
        </div>

        @if (pending(); as to) {
          <app-modal [label]="'storeOrders.do.' + to | transloco" (close)="close()">
            <h2 class="section-title">{{ 'storeOrders.confirm.' + to | transloco: { reference: o.reference } }}</h2>
            <p class="mt-2 text-sm text-muted">{{ 'storeOrders.confirmHint.' + to | transloco: { amount: (o.amountToCollect | number: '1.0-3' : language.locale()) } }}</p>
            @if (to === 'NotDelivered') {
              <fieldset class="mt-4 grid gap-2 sm:grid-cols-2">
                <legend class="field-label mb-2">{{ 'storeOrders.whyNotDelivered' | transloco }}</legend>
                @for (r of reasons; track r) {
                  <label class="flex cursor-pointer items-center gap-2 rounded-lg border px-3 py-2 text-sm transition-colors"
                    [class]="notDelivered() === r ? 'border-brand bg-brand-soft' : 'border-line-strong/80 hover:bg-subtle'">
                    <input type="radio" name="ndReason" [checked]="notDelivered() === r" (change)="notDelivered.set(r)" class="checkbox" />
                    {{ 'orders.notDelivered.' + r | transloco }}
                  </label>
                }
              </fieldset>
            }
            @if (to === 'Cancelled' || to === 'NotDelivered') {
              <label for="statusReason" class="field-label mt-4">{{ (to === 'Cancelled' ? 'storeOrders.reasonRequired' : 'storeOrders.reasonOptional') | transloco }}</label>
              <textarea id="statusReason" rows="3" maxlength="500" [value]="reason()" (input)="reason.set($any($event.target).value)" class="mt-1.5 field resize-y"></textarea>
            }
            @if (error(); as key) { <p role="alert" class="alert-danger mt-4">{{ key | transloco }}</p> }
            <div class="mt-6 flex justify-end gap-3">
              <button type="button" (click)="close()" class="btn btn-ghost">{{ 'common.cancel' | transloco }}</button>
              <button type="button" (click)="confirm()" [disabled]="busy() || !ready()" class="btn"
                [class]="to === 'Cancelled' || to === 'NotDelivered' ? 'btn-danger' : to === 'Delivered' ? 'btn-success' : 'btn-primary'">
                {{ 'storeOrders.do.' + to | transloco }}
              </button>
            </div>
          </app-modal>
        }
      } @else if (order.error()) {
        <div class="card empty-state mt-6"><p>{{ 'errors.order.storeOrderNotFound' | transloco }}</p></div>
      } @else {
        <p class="card empty-state mt-6">{{ 'common.loading' | transloco }}</p>
      }
    </main>
  `,
})
export class StoreOrderDetail {
  private readonly api = inject(StoreOrderApi);
  protected readonly language = inject(LanguageService);

  readonly id = input.required<string>();
  protected readonly order = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.get(params) });

  protected readonly tone = orderStatusTone;
  protected readonly reasons = NOT_DELIVERED;
  protected readonly pending = signal<StoreOrderStatus | null>(null);
  protected readonly reason = signal('');
  protected readonly notDelivered = signal<NotDeliveredReason | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    const seo = inject(Seo);
    effect(() => {
      this.language.lang();
      seo.setKey('storeOrders.title');
    });
  }

  protected ask(to: StoreOrderStatus): void {
    this.reason.set('');
    this.notDelivered.set(null);
    this.error.set(null);
    this.pending.set(to);
  }

  protected close(): void {
    if (!this.busy()) this.pending.set(null);
  }

  /** Cancelling needs a reason; "not delivered" needs one of the listed reasons. */
  protected ready(): boolean {
    const to = this.pending();
    if (to === 'Cancelled') return !!this.reason().trim();
    if (to === 'NotDelivered') return !!this.notDelivered();
    return true;
  }

  protected confirm(): void {
    const to = this.pending();
    if (!to || !this.ready()) return;
    this.busy.set(true);
    this.api.change(this.id(), { status: to, reason: this.reason().trim() || null, notDeliveredReason: this.notDelivered() }).subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.busy.set(false);
        this.pending.set(null);
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.busy.set(false);
        // Someone else moved it meanwhile: show the current state.
        if (this.error() === 'errors.order.changed' || this.error() === 'errors.order.transitionNotAllowed') this.order.reload();
      },
    });
  }
}
