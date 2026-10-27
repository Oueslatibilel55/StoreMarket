import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { BillingApi, StorePayment, StorePaymentMethod } from '../../../core/billing/billing-api';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { PaymentFields } from '../../../shared/billing/payment-fields';
import { PaymentList } from '../../../shared/billing/payment-list';
import { RejectDialog } from '../../../shared/billing/reject-dialog';
import { StatementTable } from '../../../shared/billing/statement-table';
import { LocalizedPipe } from '../../../shared/i18n/localized';
import { Modal } from '../../../shared/ui/modal';

/**
 * A store's billing on its admin page (025): monthly price (editable, audited), paid-until, what is owed, the month-by-month
 * statement (subscription paid, commissions billed / paid), payments with approve / reject, and recording a subscription
 * payment received in person (the best offer is suggested).
 */
@Component({
  selector: 'app-store-billing-section',
  imports: [DatePipe, DecimalPipe, TranslocoPipe, LocalizedPipe, Modal, PaymentFields, PaymentList, RejectDialog, StatementTable],
  template: `
    <section class="mt-10 border-t border-line pt-6">
      <div class="flex flex-wrap items-end justify-between gap-x-6 gap-y-3">
        <h2 class="section-title">{{ 'billing.title' | transloco }}</h2>
        <button type="button" class="btn btn-primary btn-sm" (click)="openRecord()">{{ 'billing.record.subscription' | transloco }}</button>
      </div>

      @if (billing.error()) {
        <p class="alert-danger mt-5">{{ 'errors.unknown' | transloco }}</p>
      } @else if (billing.value(); as b) {
        <div class="mt-5 space-y-6">
          <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <div class="stat-tile">
              <p class="stat-label">{{ 'billing.monthlyPrice' | transloco }}</p>
              @if (editingPrice()) {
                <div class="mt-1 flex flex-wrap items-center gap-1.5">
                  <input type="number" min="0" step="0.001" class="field w-24 py-1" dir="ltr" [value]="price()" (input)="price.set($any($event.target).value)" />
                  <button type="button" class="btn btn-primary btn-sm" [disabled]="busy()" (click)="savePrice()">{{ 'common.save' | transloco }}</button>
                </div>
              } @else {
                <p class="stat-value" dir="ltr">{{ b.monthlyPrice | number: '1.0-3' : language.locale() }} <span class="text-sm font-medium text-muted">TND</span></p>
                <button type="button" class="link mt-1 cursor-pointer text-xs" (click)="editPrice(b.monthlyPrice)">{{ 'common.edit' | transloco }}</button>
              }
            </div>
            <div class="stat-tile">
              <p class="stat-label">{{ 'billing.paidUntil' | transloco }}</p>
              <p class="stat-value text-xl" [class.text-danger]="b.state.late">{{ b.state.paidUntil | date: 'mediumDate' : undefined : language.locale() }}</p>
            </div>
            <div class="stat-tile">
              <p class="stat-label">{{ 'billing.commissionsDue' | transloco }}</p>
              <p class="stat-value" dir="ltr" [class.text-danger]="b.state.commissionsDue > 0">{{ b.state.commissionsDue | number: '1.0-3' : language.locale() }} <span class="text-sm font-medium text-muted">TND</span></p>
              <p class="mt-1 text-xs text-muted">{{ 'billing.notInvoicedYet' | transloco: { amount: (b.currentMonth.amount | number: '1.0-3' : language.locale()), n: b.currentMonth.lines } }}</p>
            </div>
            <div class="stat-tile">
              <p class="stat-label">{{ 'billing.state' | transloco }}</p>
              <p class="mt-2 flex flex-wrap gap-1.5">
                @if (b.state.suspended) { <span class="pill pill-danger">{{ 'billing.stateLabel.suspended' | transloco }}</span> }
                @else if (b.state.overdue) { <span class="pill pill-danger">{{ 'billing.stateLabel.overdue' | transloco }}</span> }
                @else if (b.state.daysLeft !== null) { <span class="pill pill-warning">{{ 'billing.stateLabel.countdown' | transloco: { n: b.state.daysLeft } }}</span> }
                @else { <span class="pill pill-success">{{ 'billing.stateLabel.ok' | transloco }}</span> }
                @if (b.state.waitingForApproval) { <span class="pill pill-brand">{{ 'billing.stateLabel.waiting' | transloco }}</span> }
              </p>
            </div>
          </div>
          @if (error()) { <p class="alert-danger">{{ error()! | transloco }}</p> }

          <div class="card overflow-hidden">
            <div class="card-header"><h3 class="section-title">{{ 'billing.statement.title' | transloco }}</h3></div>
            <app-statement-table [months]="b.months" invoiceLink="/admin/billing/invoices" [currentMonth]="b.currentMonth.month" />
          </div>

          <div class="card overflow-hidden">
            <div class="card-header"><h3 class="section-title">{{ 'billing.payments.title' | transloco }}</h3></div>
            <app-payment-list [payments]="b.payments" invoiceLink="/admin/billing/invoices" [actions]="true"
              (approve)="approve($event)" (reject)="rejecting.set($event)" />
          </div>
        </div>
      } @else {
        <p class="py-10 text-center text-sm text-muted">{{ 'common.loading' | transloco }}</p>
      }
    </section>

    @if (recording()) {
      <app-modal [label]="'billing.record.subscription' | transloco" (close)="recording.set(false)">
        <h2 class="section-title">{{ 'billing.record.subscription' | transloco }}</h2>
        <label class="mt-4 block">
          <span class="field-label">{{ 'billing.record.months' | transloco }}</span>
          <input type="number" min="1" max="36" class="field" dir="ltr" [value]="months()" (input)="setMonths($any($event.target).value)" />
        </label>
        @if (quote.value(); as q) {
          <label class="mt-3 block">
            <span class="field-label">{{ 'billing.record.offer' | transloco }}</span>
            <select class="field" [value]="offerId() ?? ''" (change)="offerId.set($any($event.target).value || null)">
              <option value="" [selected]="!offerId()">{{ 'billing.record.noOffer' | transloco }}</option>
              @for (o of q.offers; track o.id; let first = $first) {
                <option [value]="o.id" [selected]="o.id === offerId()">{{ o.name | localized: language.lang() }} (−{{ o.discount | number: '1.0-3' : language.locale() }} TND){{ first ? ' ★' : '' }}</option>
              }
            </select>
          </label>
          <p class="mt-3 flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1 rounded-lg border border-line bg-subtle px-3 py-2.5 text-sm">
            {{ 'billing.record.total' | transloco: { months: q.months, price: (q.monthlyPrice | number: '1.0-3' : language.locale()) } }}
            <strong class="figure text-base text-ink" dir="ltr">{{ amount() | number: '1.0-3' : language.locale() }} TND</strong>
          </p>
        }
        <div class="mt-4">
          <app-payment-fields [(method)]="method" [(reference)]="reference" [(note)]="note" [(paidOn)]="paidOn" />
        </div>
        @if (error()) { <p class="alert-danger mt-4">{{ error()! | transloco }}</p> }
        <div class="mt-6 flex flex-wrap justify-end gap-2 border-t border-line pt-4">
          <button type="button" class="btn btn-ghost" (click)="recording.set(false)">{{ 'common.cancel' | transloco }}</button>
          <button type="button" class="btn btn-primary" [disabled]="busy() || !quote.value()" (click)="record()">{{ 'billing.record.confirm' | transloco }}</button>
        </div>
      </app-modal>
    }

    @if (rejecting(); as p) {
      <app-reject-dialog [payment]="p" [busy]="busy()" [error]="error()" (confirmed)="reject(p, $event)" (cancelled)="rejecting.set(null)" />
    }
  `,
})
export class StoreBillingSection {
  private readonly api = inject(BillingApi);
  protected readonly language = inject(LanguageService);

  readonly storeId = input.required<string>();

  protected readonly billing = rxResource({ params: () => this.storeId(), stream: ({ params }) => this.api.store(params) });
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  // Price
  protected readonly editingPrice = signal(false);
  protected readonly price = signal('');

  // Recording a subscription payment
  protected readonly recording = signal(false);
  protected readonly months = signal(1);
  protected readonly offerId = signal<string | null>(null);
  protected readonly method = signal<StorePaymentMethod>('Cash');
  protected readonly reference = signal('');
  protected readonly note = signal('');
  protected readonly paidOn = signal(new Date().toISOString().slice(0, 10));
  protected readonly quote = rxResource({
    params: () => (this.recording() ? { id: this.storeId(), months: this.months() } : undefined),
    stream: ({ params }) => (params ? this.api.quote(params.id, params.months) : of(undefined)),
  });
  protected readonly amount = computed(() => {
    const q = this.quote.value();
    if (!q) return 0;
    return q.offers.find((o) => o.id === this.offerId())?.amount ?? q.gross;
  });

  protected readonly rejecting = signal<StorePayment | null>(null);

  constructor() {
    // The best offer is suggested each time a new quote comes (the admin may pick another or none).
    effect(() => {
      const best = this.quote.value()?.offers[0]?.id ?? null;
      untracked(() => this.offerId.set(best));
    });
  }

  protected editPrice(current: number): void {
    this.price.set(String(current));
    this.editingPrice.set(true);
  }

  protected savePrice(): void {
    this.run(this.api.setPrice(this.storeId(), Number(this.price()), null), () => this.editingPrice.set(false));
  }

  protected openRecord(): void {
    this.error.set(null);
    this.months.set(1);
    this.offerId.set(null);
    this.reference.set('');
    this.note.set('');
    this.recording.set(true);
  }

  protected setMonths(value: string): void {
    const n = Math.trunc(Number(value));
    if (n >= 1 && n <= 36) this.months.set(n);
  }

  protected record(): void {
    this.run(this.api.recordSubscription(this.storeId(), {
      months: this.months(), offerId: this.offerId(), method: this.method(), reference: this.reference() || null,
      note: this.note() || null, paidOn: this.paidOn(),
    }), () => this.recording.set(false));
  }

  protected approve(p: StorePayment): void {
    this.run(this.api.approve(p.id));
  }

  protected reject(p: StorePayment, reason: string): void {
    this.run(this.api.reject(p.id, reason), () => this.rejecting.set(null));
  }

  private run(call: Observable<unknown>, done?: () => void): void {
    this.busy.set(true);
    this.error.set(null);
    call.subscribe({
      next: () => {
        this.busy.set(false);
        done?.();
        this.billing.reload();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(errorKey(err));
      },
    });
  }
}
