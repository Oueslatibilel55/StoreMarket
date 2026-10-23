import { Component, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { BillingApi, remaining, StorePayment, StorePaymentMethod } from '../../../core/billing/billing-api';
import { errorKey } from '../../../core/http/problem';
import { InvoiceView } from '../../../shared/billing/invoice-view';
import { PaymentFields } from '../../../shared/billing/payment-fields';
import { PaymentList } from '../../../shared/billing/payment-list';
import { RejectDialog } from '../../../shared/billing/reject-dialog';

/** `/admin/billing/invoices/:id` — a commission invoice (025): print / PDF, its payments, record one received in person. */
@Component({
  selector: 'app-admin-invoice-page',
  imports: [RouterLink, TranslocoPipe, InvoiceView, PaymentFields, PaymentList, RejectDialog],
  template: `
    @if (detail.error()) {
      <p class="alert-danger">{{ 'errors.billing.invoiceNotFound' | transloco }}</p>
    } @else if (detail.value(); as d) {
      <div class="mb-4 flex flex-wrap items-center justify-between gap-3 print:hidden">
        <a [routerLink]="['/admin/stores', d.storeId]" class="link text-sm">← {{ d.storeName }}</a>
        <button type="button" class="btn btn-secondary btn-sm" (click)="print()">{{ 'billing.invoice.print' | transloco }}</button>
      </div>
      <app-invoice-view [detail]="d" />

      @if (d.invoice.status !== 'Paid') {
        <section class="card mt-6 p-5 sm:p-6 print:hidden">
          <h2 class="section-title">{{ 'billing.record.invoice' | transloco }}</h2>
          <label class="mt-4 block max-w-xs">
            <span class="field-label">{{ 'billing.fields.amount' | transloco }}</span>
            <input type="number" min="0" step="0.001" class="field" dir="ltr" [value]="amount() ?? left(d)" (input)="amount.set($any($event.target).value)" />
          </label>
          <div class="mt-3">
            <app-payment-fields [(method)]="method" [(reference)]="reference" [(note)]="note" [(paidOn)]="paidOn" />
          </div>
          @if (error()) { <p class="alert-danger mt-3">{{ error()! | transloco }}</p> }
          <button type="button" class="btn btn-primary mt-4" [disabled]="busy()" (click)="record(d.invoice.id, left(d))">{{ 'billing.record.confirm' | transloco }}</button>
        </section>
      }

      <section class="card mt-6 overflow-hidden print:hidden">
        <div class="card-header"><h2 class="section-title">{{ 'billing.payments.title' | transloco }}</h2></div>
        <app-payment-list [payments]="d.payments" invoiceLink="/admin/billing/invoices" [actions]="true"
          (approve)="approve($event)" (reject)="rejecting.set($event)" />
      </section>

      @if (rejecting(); as p) {
        <app-reject-dialog [payment]="p" [busy]="busy()" [error]="error()" (confirmed)="reject(p, $event)" (cancelled)="rejecting.set(null)" />
      }
    } @else {
      <p class="empty-state">{{ 'common.loading' | transloco }}</p>
    }
  `,
})
export class AdminInvoicePage {
  private readonly api = inject(BillingApi);

  /** Route param :id */
  readonly id = input.required<string>();

  protected readonly detail = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.invoice(params) });
  protected readonly amount = signal<string | null>(null);
  protected readonly method = signal<StorePaymentMethod>('Cash');
  protected readonly reference = signal('');
  protected readonly note = signal('');
  protected readonly paidOn = signal(new Date().toISOString().slice(0, 10));
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly rejecting = signal<StorePayment | null>(null);

  protected left(d: { invoice: Parameters<typeof remaining>[0] }): number {
    return remaining(d.invoice);
  }

  protected print(): void {
    window.print();
  }

  protected record(invoiceId: string, left: number): void {
    const amount = Number(this.amount() ?? left);
    this.run(this.api.recordInvoicePayment(invoiceId, {
      amount, method: this.method(), reference: this.reference() || null, note: this.note() || null, paidOn: this.paidOn(),
    }), () => {
      this.amount.set(null);
      this.reference.set('');
      this.note.set('');
    });
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
        this.detail.reload();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(errorKey(err));
      },
    });
  }
}
