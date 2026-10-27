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
      <a routerLink="/my-store/billing" class="back-link print:hidden">
        <span aria-hidden="true" class="inline-block rtl:rotate-180">←</span> {{ 'billing.mine.title' | transloco }}
      </a>
      @if (detail.error()) {
        <p role="alert" class="alert-danger">{{ 'errors.billing.invoiceNotFound' | transloco }}</p>
      } @else if (detail.value(); as d) {
        <div class="mb-6 flex flex-wrap items-center justify-end gap-3 border-b border-line pb-5 print:hidden">
          <div class="flex flex-wrap gap-2">
            <button type="button" class="btn btn-secondary btn-sm" (click)="print()">{{ 'billing.invoice.print' | transloco }}</button>
            @if (d.invoice.status !== 'Paid' && !d.invoice.paymentDeclared) {
              <button type="button" class="btn btn-primary btn-sm" (click)="declaring.set(true)">{{ 'billing.declare.invoice' | transloco }}</button>
            }
          </div>
        </div>
        @if (d.invoice.paymentDeclared) { <p class="alert-info mb-6 print:hidden">{{ 'billing.banner.waiting' | transloco }}</p> }
        <app-invoice-view [detail]="d" />

        <section class="card mt-6 overflow-hidden print:hidden">
          <div class="card-header"><h2 class="section-title">{{ 'billing.payments.title' | transloco }}</h2></div>
          <app-payment-list [payments]="d.payments" invoiceLink="/my-store/billing/invoices" />
        </section>

        @if (declaring()) {
          <app-declare-payment [invoiceId]="d.invoice.id" [left]="left()" (declared)="done()" (closed)="declaring.set(false)" />
        }
      } @else {
        <p class="card empty-state">{{ 'common.loading' | transloco }}</p>
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
