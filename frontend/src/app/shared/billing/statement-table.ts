import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { invoiceTone, MonthStatement } from '../../core/billing/billing-api';
import { LanguageService } from '../../core/i18n/language.service';

/**
 * A store's statement month by month (025): subscription paid that month, the month's affiliate commissions
 * (billed / paid / left) and the invoice status. The same table for the admin and the store owner.
 */
@Component({
  selector: 'app-statement-table',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe],
  template: `
    <div class="overflow-x-auto">
      <table class="data-table">
        <thead>
          <tr>
            <th scope="col">{{ 'billing.statement.month' | transloco }}</th>
            <th scope="col" class="text-end">{{ 'billing.statement.subscription' | transloco }}</th>
            <th scope="col" class="text-end">{{ 'billing.statement.commissions' | transloco }}</th>
            <th scope="col" class="text-end">{{ 'billing.statement.paid' | transloco }}</th>
            <th scope="col" class="text-end">{{ 'billing.statement.left' | transloco }}</th>
            <th scope="col">{{ 'billing.statement.invoice' | transloco }}</th>
          </tr>
        </thead>
        <tbody>
          @for (m of months(); track m.month) {
            <tr>
              <td class="whitespace-nowrap font-medium text-ink">{{ m.month | date: 'MMMM y' : undefined : language.locale() }}</td>
              <td class="text-end tabular-nums whitespace-nowrap" dir="ltr">
                @if (m.subscriptionPaid || m.subscriptionMonthsPaid) {
                  {{ m.subscriptionPaid | number: '1.0-3' : language.locale() }}
                  <span class="block text-xs text-muted">{{ 'billing.statement.months' | transloco: { n: m.subscriptionMonthsPaid } }}</span>
                } @else { <span class="text-muted">—</span> }
              </td>
              <td class="text-end tabular-nums whitespace-nowrap" dir="ltr">{{ m.invoiceId ? (m.commissionsBilled | number: '1.0-3' : language.locale()) : '—' }}</td>
              <td class="text-end tabular-nums whitespace-nowrap" dir="ltr">{{ m.invoiceId ? (m.commissionsPaid | number: '1.0-3' : language.locale()) : '—' }}</td>
              <td class="figure text-end whitespace-nowrap" dir="ltr"
                [class.text-danger]="m.invoiceId && m.invoiceStatus !== 'Paid'">
                {{ m.invoiceId ? (m.commissionsBilled - m.commissionsPaid | number: '1.0-3' : language.locale()) : '—' }}
              </td>
              <td class="whitespace-nowrap">
                @if (m.invoiceId) {
                  <span class="inline-flex items-center gap-2">
                    <a [routerLink]="[invoiceLink(), m.invoiceId]" class="link font-mono text-xs" dir="ltr">{{ m.invoiceReference }}</a>
                    <span class="pill" [class]="tone(m.invoiceStatus)">{{ 'billing.invoiceStatus.' + m.invoiceStatus | transloco }}</span>
                  </span>
                } @else {
                  <span class="text-xs text-muted">{{ (m.month === currentMonth() ? 'billing.statement.inProgress' : 'billing.statement.none') | transloco }}</span>
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class StatementTable {
  protected readonly language = inject(LanguageService);

  readonly months = input.required<MonthStatement[]>();
  /** `/admin/billing/invoices` or `/my-store/billing/invoices`. */
  readonly invoiceLink = input.required<string>();
  /** First day of the current month (its commissions are billed after it ends). */
  readonly currentMonth = input<string | null>(null);

  protected tone(status: MonthStatement['invoiceStatus']): string {
    return invoiceTone(status);
  }
}
