import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { paymentTone, StorePayment } from '../../core/billing/billing-api';
import { LanguageService } from '../../core/i18n/language.service';

/**
 * Store payments (025): what, how much, how, when, and their status. With `actions` (admin) a declared payment gets
 * approve / reject buttons; `showStore` adds the store name (the admin's queue).
 */
@Component({
  selector: 'app-payment-list',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe],
  template: `
    @if (payments().length === 0) {
      <p class="empty-state">{{ 'billing.payments.none' | transloco }}</p>
    } @else {
      <ul class="divide-y divide-line">
        @for (p of payments(); track p.id) {
          <li class="flex flex-col gap-2 px-5 py-3.5 sm:flex-row sm:items-start sm:justify-between">
            <div class="min-w-0 text-sm">
              <p class="flex flex-wrap items-center gap-2">
                <span class="font-mono text-xs text-muted" dir="ltr">{{ p.number }}</span>
                <span class="pill" [class]="tone(p.status)">{{ 'billing.paymentStatus.' + p.status | transloco }}</span>
                @if (showStore()) {
                  <a [routerLink]="['/admin/stores', p.storeId]" class="link font-medium">{{ p.storeName }}</a>
                }
              </p>
              <p class="mt-1 font-medium text-ink">
                @if (p.kind === 'Subscription') {
                  {{ 'billing.payments.subscription' | transloco: { n: p.months } }}
                  @if (p.offerName) { <span class="pill pill-brand ms-1">{{ p.offerName }}</span> }
                } @else {
                  {{ 'billing.payments.commission' | transloco }}
                  @if (p.invoiceId) {
                    <a [routerLink]="[invoiceLink(), p.invoiceId]" class="link font-mono text-xs" dir="ltr">{{ p.invoiceReference }}</a>
                  }
                }
              </p>
              <p class="mt-0.5 text-xs text-muted">
                {{ 'billing.method.' + p.method | transloco }} · {{ p.paidOn | date: 'mediumDate' : undefined : language.locale() }}
                @if (p.reference) { · <span dir="ltr">{{ p.reference }}</span> }
                · {{ (p.declaredByOwner ? 'billing.payments.byOwner' : 'billing.payments.byAdmin') | transloco }}
                @if (p.coversFrom && p.coversTo) {
                  · {{ 'billing.payments.covers' | transloco: { from: (p.coversFrom | date: 'mediumDate' : undefined : language.locale()), to: (p.coversTo | date: 'mediumDate' : undefined : language.locale()) } }}
                }
              </p>
              @if (p.note) { <p class="mt-1 text-xs text-ink-soft">{{ p.note }}</p> }
              @if (p.rejectionReason) { <p class="mt-1 text-xs text-danger">{{ 'billing.payments.rejected' | transloco }} {{ p.rejectionReason }}</p> }
              @if (p.receiptUrl) {
                <a [href]="p.receiptUrl" target="_blank" rel="noopener" class="link mt-1 inline-block text-xs">{{ 'billing.payments.receipt' | transloco }} ↗</a>
              }
            </div>
            <div class="flex shrink-0 flex-col items-start gap-2 sm:items-end">
              <p class="font-bold text-ink" dir="ltr">{{ p.amount | number: '1.0-3' : language.locale() }} TND</p>
              @if (p.discount > 0) {
                <p class="text-xs text-muted" dir="ltr">−{{ p.discount | number: '1.0-3' : language.locale() }} TND</p>
              }
              @if (actions() && p.status === 'Declared') {
                <div class="flex gap-2">
                  <button type="button" class="btn btn-success btn-sm" (click)="approve.emit(p)">{{ 'billing.payments.approve' | transloco }}</button>
                  <button type="button" class="btn btn-danger-soft btn-sm" (click)="reject.emit(p)">{{ 'billing.payments.reject' | transloco }}</button>
                </div>
              }
            </div>
          </li>
        }
      </ul>
    }
  `,
})
export class PaymentList {
  protected readonly language = inject(LanguageService);

  readonly payments = input.required<StorePayment[]>();
  readonly invoiceLink = input.required<string>();
  readonly actions = input(false);
  readonly showStore = input(false);
  readonly approve = output<StorePayment>();
  readonly reject = output<StorePayment>();

  protected readonly tone = paymentTone;
}
