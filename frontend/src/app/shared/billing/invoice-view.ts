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
    <article class="card overflow-hidden print:border-0 print:shadow-none">
      <header class="flex flex-wrap items-start justify-between gap-4 border-b border-line p-5 sm:p-6">
        <div>
          <p class="eyebrow">{{ 'app.name' | transloco }}</p>
          <h1 class="mt-1 text-xl font-bold text-ink">{{ 'billing.invoice.title' | transloco }}</h1>
          <p class="mt-1 font-mono text-sm text-muted" dir="ltr">{{ i.reference }}</p>
        </div>
        <div class="text-sm sm:text-end">
          <p class="font-semibold text-ink">{{ detail().storeName }}</p>
          @if (detail().storeAddress) { <p class="text-muted">{{ detail().storeAddress }}</p> }
          <p class="text-muted">{{ 'cities.' + detail().storeCity | transloco }}</p>
          <span class="pill mt-2 print:hidden" [class]="tone()">{{ 'billing.invoiceStatus.' + i.status | transloco }}</span>
        </div>
      </header>

      <dl class="grid grid-cols-2 gap-4 border-b border-line p-5 text-sm sm:grid-cols-4 sm:p-6">
        <div><dt class="stat-label">{{ 'billing.invoice.month' | transloco }}</dt><dd class="mt-0.5 font-medium text-ink">{{ i.month | date: 'MMMM y' : undefined : language.locale() }}</dd></div>
        <div><dt class="stat-label">{{ 'billing.invoice.issued' | transloco }}</dt><dd class="mt-0.5 font-medium text-ink">{{ detail().issuedAt | date: 'mediumDate' : undefined : language.locale() }}</dd></div>
        <div><dt class="stat-label">{{ 'billing.invoice.due' | transloco }}</dt><dd class="mt-0.5 font-medium" [class]="i.overdue ? 'text-danger' : 'text-ink'">{{ i.dueOn | date: 'mediumDate' : undefined : language.locale() }}</dd></div>
        <div><dt class="stat-label">{{ 'billing.invoice.lines' | transloco }}</dt><dd class="mt-0.5 font-medium text-ink" dir="ltr">{{ i.lineCount }}</dd></div>
      </dl>

      <div class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead class="bg-subtle/60 text-xs text-muted">
            <tr>
              <th scope="col" class="px-4 py-2 text-start font-medium">{{ 'billing.invoice.order' | transloco }}</th>
              <th scope="col" class="px-4 py-2 text-start font-medium">{{ 'billing.invoice.product' | transloco }}</th>
              <th scope="col" class="px-4 py-2 text-end font-medium">{{ 'billing.invoice.quantity' | transloco }}</th>
              <th scope="col" class="px-4 py-2 text-end font-medium">{{ 'billing.invoice.lineTotal' | transloco }}</th>
              <th scope="col" class="px-4 py-2 text-end font-medium">%</th>
              <th scope="col" class="px-4 py-2 text-start font-medium">{{ 'billing.invoice.affiliate' | transloco }}</th>
              <th scope="col" class="px-4 py-2 text-end font-medium">{{ 'billing.invoice.amount' | transloco }}</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-line">
            @for (l of detail().lines; track l.coinEntryId) {
              <tr>
                <td class="px-4 py-2 whitespace-nowrap">
                  <span class="font-mono text-xs" dir="ltr">{{ l.orderReference ?? '—' }}</span>
                  <span class="block text-xs text-muted">{{ l.creditedAt | date: 'shortDate' : undefined : language.locale() }}</span>
                </td>
                <td class="px-4 py-2">
                  {{ l.productName ?? '—' }}
                  @if (l.type === 'Adjustment') { <span class="pill pill-neutral ms-1">{{ 'billing.invoice.adjustment' | transloco }}</span> }
                </td>
                <td class="px-4 py-2 text-end tabular-nums" dir="ltr">{{ l.quantity ?? '—' }}</td>
                <td class="px-4 py-2 text-end tabular-nums" dir="ltr">{{ l.lineTotal !== null ? (l.lineTotal | number: '1.0-3' : language.locale()) : '—' }}</td>
                <td class="px-4 py-2 text-end tabular-nums" dir="ltr">{{ l.affiliatePercent ?? '—' }}</td>
                <td class="px-4 py-2"><span class="font-mono text-xs" dir="ltr">{{ l.affiliateCode ? '#' + l.affiliateCode : '—' }}</span></td>
                <td class="px-4 py-2 text-end font-medium tabular-nums" dir="ltr">{{ l.amount | number: '1.3-3' : language.locale() }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <dl class="ms-auto grid max-w-xs gap-1.5 border-t border-line p-5 text-sm sm:p-6">
        <div class="flex justify-between gap-6"><dt class="text-muted">{{ 'billing.invoice.total' | transloco }}</dt><dd class="font-semibold tabular-nums" dir="ltr">{{ i.total | number: '1.3-3' : language.locale() }} TND</dd></div>
        <div class="flex justify-between gap-6"><dt class="text-muted">{{ 'billing.invoice.paid' | transloco }}</dt><dd class="tabular-nums" dir="ltr">{{ i.paidAmount | number: '1.3-3' : language.locale() }} TND</dd></div>
        <div class="flex justify-between gap-6 border-t border-line pt-1.5"><dt class="font-semibold text-ink">{{ 'billing.invoice.left' | transloco }}</dt><dd class="font-bold tabular-nums" [class]="left() > 0 ? 'text-danger' : 'text-ink'" dir="ltr">{{ left() | number: '1.3-3' : language.locale() }} TND</dd></div>
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
