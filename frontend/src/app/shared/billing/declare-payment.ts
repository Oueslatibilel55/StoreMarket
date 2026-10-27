import { DecimalPipe } from '@angular/common';
import { Component, inject, input, output, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { BillingApi, StorePayment, StorePaymentMethod } from '../../core/billing/billing-api';
import { errorKey } from '../../core/http/problem';
import { LanguageService } from '../../core/i18n/language.service';
import { ImageUpload } from '../ui/image-upload';
import { Modal } from '../ui/modal';
import { PaymentFields } from './payment-fields';

/**
 * The store owner's "I paid" (025): months of subscription (`invoiceId` empty — the best offer applies, the admin checks
 * it) or an amount of an invoice (all that is left by default), how it was paid and an optional receipt photo.
 */
@Component({
  selector: 'app-declare-payment',
  imports: [DecimalPipe, TranslocoPipe, ImageUpload, Modal, PaymentFields],
  template: `
    <app-modal [label]="'billing.declare.title' | transloco" (close)="closed.emit()">
      <h2 class="section-title">{{ (invoiceId() ? 'billing.declare.invoice' : 'billing.declare.subscription') | transloco }}</h2>
      <p class="mt-1 text-sm text-muted">{{ 'billing.declare.hint' | transloco }}</p>

      @if (invoiceId()) {
        <label class="mt-4 block">
          <span class="field-label">{{ 'billing.fields.amount' | transloco }}</span>
          <input type="number" min="0" step="0.001" class="field" dir="ltr" [value]="amount() ?? left()" (input)="amount.set($any($event.target).value)" />
          <span class="field-hint">{{ 'billing.declare.left' | transloco: { amount: (left() | number: '1.0-3' : language.locale()) } }}</span>
        </label>
      } @else {
        <label class="mt-4 block">
          <span class="field-label">{{ 'billing.record.months' | transloco }}</span>
          <input type="number" min="1" max="36" class="field" dir="ltr" [value]="months()" (input)="months.set($any($event.target).value)" />
          <span class="field-hint">
            {{ 'billing.declare.price' | transloco: { months: months(), price: (monthlyPrice() | number: '1.0-3' : language.locale()), total: (+months() * monthlyPrice() | number: '1.0-3' : language.locale()) } }}
          </span>
        </label>
      }

      <div class="mt-3">
        <app-payment-fields [(method)]="method" [(reference)]="reference" [(note)]="note" [(paidOn)]="paidOn" />
      </div>
      <div class="mt-3">
        <span class="field-label">{{ 'billing.declare.receipt' | transloco }}</span>
        <app-image-upload purpose="PaymentReceipt" [url]="receiptUrl()" (changed)="receipt($event)" />
      </div>

      @if (error()) { <p class="alert-danger mt-3">{{ error()! | transloco }}</p> }
      <div class="mt-6 flex flex-wrap justify-end gap-2 border-t border-line pt-4">
        <button type="button" class="btn btn-ghost" (click)="closed.emit()">{{ 'common.cancel' | transloco }}</button>
        <button type="button" class="btn btn-primary" [disabled]="busy()" (click)="send()">{{ 'billing.declare.send' | transloco }}</button>
      </div>
    </app-modal>
  `,
})
export class DeclarePayment {
  private readonly api = inject(BillingApi);
  protected readonly language = inject(LanguageService);

  /** Set = paying this invoice; empty = paying months of subscription. */
  readonly invoiceId = input<string | null>(null);
  readonly left = input(0);
  readonly monthlyPrice = input(0);
  readonly declared = output<StorePayment>();
  readonly closed = output<void>();

  protected readonly amount = signal<string | null>(null);
  protected readonly months = signal('1');
  protected readonly method = signal<StorePaymentMethod>('BankTransfer');
  protected readonly reference = signal('');
  protected readonly note = signal('');
  protected readonly paidOn = signal(new Date().toISOString().slice(0, 10));
  protected readonly receiptId = signal<string | null>(null);
  protected readonly receiptUrl = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected receipt(image: { id: string; url: string } | null): void {
    this.receiptId.set(image?.id ?? null);
    this.receiptUrl.set(image?.url ?? null);
  }

  protected send(): void {
    const invoiceId = this.invoiceId();
    this.busy.set(true);
    this.error.set(null);
    this.api.declare({
      kind: invoiceId ? 'Commission' : 'Subscription',
      invoiceId,
      amount: invoiceId ? Number(this.amount() ?? this.left()) : null,
      months: invoiceId ? null : Math.trunc(Number(this.months())),
      method: this.method(),
      reference: this.reference() || null,
      note: this.note() || null,
      paidOn: this.paidOn(),
      receiptImageId: this.receiptId(),
    }).subscribe({
      next: (payment) => {
        this.busy.set(false);
        this.declared.emit(payment);
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(errorKey(err));
      },
    });
  }
}
