import { Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { BillingApi, remaining } from '../../../core/billing/billing-api';
import { DeclarePayment } from '../../../shared/billing/declare-payment';
import { InvoiceView } from '../../../shared/billing/invoice-view';
import { PaymentList } from '../../../shared/billing/payment-list';

/** `/my-store/billing/invoices/:id` — one of the store's commission invoices (025): print / PDF and "I paid". */
@Component({
  selector: 'app-my-invoice-page',
  imports: [RouterLink, TranslocoPipe, DeclarePayment, InvoiceView, PaymentList],
  template: `
    <main class="page max-w-5xl">
      @if (detail.error()) {
        <p class="alert-danger">{{ 'errors.billing.invoiceNotFound' | transloco }}</p>
      } @else if (detail.value(); as d) {
        <div class="mb-4 flex flex-wrap items-center justify-between gap-3 print:hidden">
          <a routerLink="/my-store/billing" class="link text-sm">← {{ 'billing.mine.title' | transloco }}</a>
          <div class="flex gap-2">
            <button type="button" class="btn btn-secondary btn-sm" (click)="print()">{{ 'billing.invoice.print' | transloco }}</button>
            @if (d.invoice.status !== 'Paid' && !d.invoice.paymentDeclared) {
              <button type="button" class="btn btn-primary btn-sm" (click)="declaring.set(true)">{{ 'billing.declare.invoice' | transloco }}</button>
            }
          </div>
        </div>
        @if (d.invoice.paymentDeclared) { <p class="alert-info mb-4 print:hidden">{{ 'billing.banner.waiting' | transloco }}</p> }
        <app-invoice-view [detail]="d" />

        <section class="card mt-6 overflow-hidden print:hidden">
          <div class="card-header"><h2 class="section-title">{{ 'billing.payments.title' | transloco }}</h2></div>
          <app-payment-list [payments]="d.payments" invoiceLink="/my-store/billing/invoices" />
        </section>

        @if (declaring()) {
          <app-declare-payment [invoiceId]="d.invoice.id" [left]="left()" (declared)="done()" (closed)="declaring.set(false)" />
        }
      } @else {
        <p class="empty-state">{{ 'common.loading' | transloco }}</p>
      }
    </main>
  `,
})
export class MyInvoicePage {
  private readonly api = inject(BillingApi);

  /** Route param :id */
  readonly id = input.required<string>();

  protected readonly detail = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.myInvoice(params) });
  protected readonly declaring = signal(false);
  protected readonly left = computed(() => {
    const d = this.detail.value();
    return d ? remaining(d.invoice) : 0;
  });

  protected print(): void {
    window.print();
  }

  protected done(): void {
    this.declaring.set(false);
    this.detail.reload();
  }
}
