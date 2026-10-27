import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { InvoiceDetail, invoiceTone, remaining } from '../../core/billing/billing-api';
import { LanguageService } from '../../core/i18n/language.service';

/**
 * A commission invoice as a document (025): who, which month, due date, one line per commission, totals. Print-ready —
 * "Print / PDF" uses the browser (header, menus and buttons are hidden in print), in the active language.
 */
@Component({
  selector: 'app-invoice-view',
  imports: [DatePipe, DecimalPipe, TranslocoPipe],
  template: `
    @let i = detail().invoice;
    <article class="card overflow-hidden print:rounded-none print:border-0 print:bg-white print:text-black print:shadow-none">
      <header class="flex flex-wrap items-start justify-between gap-x-6 gap-y-4 border-b border-line px-5 py-6 sm:px-6 print:border-black/30 print:px-0">
        <div class="min-w-0">
          <p class="eyebrow print:text-black">{{ 'app.name' | transloco }}</p>
          <h1 class="mt-1 text-2xl font-bold tracking-tight text-ink print:text-black">{{ 'billing.invoice.title' | transloco }}</h1>
          <p class="mt-1.5"><span class="code-chip print:bg-transparent print:ring-0" dir="ltr">{{ i.reference }}</span></p>
        </div>
        <div class="min-w-0 text-sm sm:text-end">
          <p class="font-semibold text-ink print:text-black">{{ detail().storeName }}</p>
          @if (detail().storeAddress) { <p class="text-muted print:text-black">{{ detail().storeAddress }}</p> }
          <p class="text-muted print:text-black">{{ 'cities.' + detail().storeCity | transloco }}</p>
          <span class="pill mt-2 print:hidden" [class]="tone()">{{ 'billing.invoiceStatus.' + i.status | transloco }}</span>
        </div>
      </header>

      <dl class="detail-grid grid-cols-2 border-b border-line px-5 py-5 sm:px-6 lg:grid-cols-4 print:border-black/30 print:px-0">
        <div><dt>{{ 'billing.invoice.month' | transloco }}</dt><dd>{{ i.month | date: 'MMMM y' : undefined : language.locale() }}</dd></div>
        <div><dt>{{ 'billing.invoice.issued' | transloco }}</dt><dd>{{ detail().issuedAt | date: 'mediumDate' : undefined : language.locale() }}</dd></div>
        <div><dt>{{ 'billing.invoice.due' | transloco }}</dt><dd [class.text-danger!]="i.overdue">{{ i.dueOn | date: 'mediumDate' : undefined : language.locale() }}</dd></div>
        <div><dt>{{ 'billing.invoice.lines' | transloco }}</dt><dd class="tabular-nums" dir="ltr">{{ i.lineCount }}</dd></div>
      </dl>

      <div class="overflow-x-auto print:overflow-visible">
        <table class="data-table">
          <thead class="print:bg-transparent! print:text-black!">
            <tr>
              <th scope="col">{{ 'billing.invoice.order' | transloco }}</th>
              <th scope="col">{{ 'billing.invoice.product' | transloco }}</th>
              <th scope="col" class="text-end">{{ 'billing.invoice.quantity' | transloco }}</th>
              <th scope="col" class="text-end">{{ 'billing.invoice.lineTotal' | transloco }}</th>
              <th scope="col" class="text-end">%</th>
              <th scope="col">{{ 'billing.invoice.affiliate' | transloco }}</th>
              <th scope="col" class="text-end">{{ 'billing.invoice.amount' | transloco }}</th>
            </tr>
          </thead>
          <tbody>
            @for (l of detail().lines; track l.coinEntryId) {
              <tr class="print:break-inside-avoid">
                <td class="whitespace-nowrap">
                  <span class="font-mono text-xs" dir="ltr">{{ l.orderReference ?? '—' }}</span>
                  <span class="block text-xs text-muted print:text-black">{{ l.creditedAt | date: 'shortDate' : undefined : language.locale() }}</span>
                </td>
                <td>
                  {{ l.productName ?? '—' }}
                  @if (l.type === 'Adjustment') { <span class="pill pill-neutral ms-1">{{ 'billing.invoice.adjustment' | transloco }}</span> }
                </td>
                <td class="text-end tabular-nums" dir="ltr">{{ l.quantity ?? '—' }}</td>
                <td class="text-end tabular-nums whitespace-nowrap" dir="ltr">{{ l.lineTotal !== null ? (l.lineTotal | number: '1.0-3' : language.locale()) : '—' }}</td>
                <td class="text-end tabular-nums" dir="ltr">{{ l.affiliatePercent ?? '—' }}</td>
                <td><span class="font-mono text-xs" dir="ltr">{{ l.affiliateCode ? '#' + l.affiliateCode : '—' }}</span></td>
                <td class="figure text-end whitespace-nowrap" dir="ltr">{{ l.amount | number: '1.3-3' : language.locale() }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <dl class="ms-auto grid max-w-xs gap-2 border-t border-line px-5 py-5 text-sm sm:px-6 print:border-black/30 print:px-0">
        <div class="flex justify-between gap-6"><dt class="text-muted print:text-black">{{ 'billing.invoice.total' | transloco }}</dt><dd class="figure text-ink print:text-black" dir="ltr">{{ i.total | number: '1.3-3' : language.locale() }} TND</dd></div>
        <div class="flex justify-between gap-6"><dt class="text-muted print:text-black">{{ 'billing.invoice.paid' | transloco }}</dt><dd class="tabular-nums" dir="ltr">{{ i.paidAmount | number: '1.3-3' : language.locale() }} TND</dd></div>
        <div class="flex items-baseline justify-between gap-6 border-t border-line pt-2 print:border-black/30"><dt class="font-semibold text-ink print:text-black">{{ 'billing.invoice.left' | transloco }}</dt><dd class="figure text-lg print:text-black" [class]="left() > 0 ? 'text-danger' : 'text-ink'" dir="ltr">{{ left() | number: '1.3-3' : language.locale() }} TND</dd></div>
      </dl>
    </article>
  `,
})
export class InvoiceView {
  protected readonly language = inject(LanguageService);

  readonly detail = input.required<InvoiceDetail>();

  protected readonly left = computed(() => remaining(this.detail().invoice));
  protected readonly tone = computed(() => invoiceTone(this.detail().invoice.status, this.detail().invoice.overdue));
}
