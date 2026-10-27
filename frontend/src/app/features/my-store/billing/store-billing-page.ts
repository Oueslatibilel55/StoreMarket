import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { BillingApi } from '../../../core/billing/billing-api';
import { LanguageService } from '../../../core/i18n/language.service';
import { Seo } from '../../../core/seo/seo';
import { DeclarePayment } from '../../../shared/billing/declare-payment';
import { PaymentList } from '../../../shared/billing/payment-list';
import { StatementTable } from '../../../shared/billing/statement-table';
import { LocalizedPipe } from '../../../shared/i18n/localized';
import { MyStoreTabs } from '../my-store-tabs';

/**
 * `/my-store/billing` — what the store owes the platform (025): monthly price and paid-until date, affiliate commissions
 * owed (month by month, with every invoice line), payments and their status, offers they can use. "I paid" sends a
 * payment for an admin to check.
 */
@Component({
  selector: 'app-store-billing-page',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe, LocalizedPipe, DeclarePayment, MyStoreTabs, PaymentList, StatementTable],
  template: `
    <main class="page">
      <app-my-store-tabs />
      <header class="page-header">
        <div class="min-w-0">
          <h1 class="page-title">{{ 'billing.mine.title' | transloco }}</h1>
          <p class="page-subtitle">{{ 'billing.mine.subtitle' | transloco }}</p>
        </div>
        @if (billing.value()) {
          <button type="button" class="btn btn-primary" (click)="declaring.set(true)">{{ 'billing.declare.subscription' | transloco }}</button>
        }
      </header>

      @if (noStore()) {
        <div class="card empty-state">
          <p>{{ 'errors.store.notFound' | transloco }}</p>
          <a routerLink="/my-store" class="btn btn-primary mt-3">{{ 'myStore.createTitle' | transloco }}</a>
        </div>
      } @else if (billing.error()) {
        <p role="alert" class="alert-danger">{{ 'errors.unknown' | transloco }}</p>
      } @else if (billing.value(); as b) {
        <div class="grid grid-cols-1 gap-3 min-[400px]:grid-cols-2 sm:gap-4 lg:grid-cols-4">
          <div class="stat-tile">
            <p class="stat-label">{{ 'billing.monthlyPrice' | transloco }}</p>
            <p class="stat-value" dir="ltr">{{ b.monthlyPrice | number: '1.0-3' : language.locale() }} TND</p>
          </div>
          <div class="stat-tile">
            <p class="stat-label">{{ 'billing.paidUntil' | transloco }}</p>
            <p class="stat-value text-xl" [class.text-danger]="b.state.late">{{ b.state.paidUntil | date: 'mediumDate' : undefined : language.locale() }}</p>
          </div>
          <div class="stat-tile">
            <p class="stat-label">{{ 'billing.commissionsDue' | transloco }}</p>
            <p class="stat-value" dir="ltr" [class.text-danger]="b.state.commissionsDue > 0">{{ b.state.commissionsDue | number: '1.0-3' : language.locale() }} TND</p>
          </div>
          <div class="stat-tile">
            <p class="stat-label">{{ 'billing.thisMonth' | transloco }}</p>
            <p class="stat-value" dir="ltr">{{ b.currentMonth.amount | number: '1.0-3' : language.locale() }} TND</p>
            <p class="mt-1 text-xs text-muted">{{ 'billing.thisMonthHint' | transloco: { n: b.currentMonth.lines } }}</p>
          </div>
        </div>
        <p class="mt-3 text-sm text-muted">{{ 'billing.mine.rule' | transloco: { n: b.graceDays } }}</p>

        @if (b.offers.length) {
          <section class="card mt-6 overflow-hidden">
            <div class="card-header"><h2 class="section-title">{{ 'billing.mine.offers' | transloco }}</h2></div>
            <ul class="divide-y divide-line">
              @for (o of b.offers; track o.id) {
                <li class="list-row">
                  <span class="font-medium text-ink">{{ o.name | localized: language.lang() }}</span>
                  <span class="flex flex-wrap items-center gap-2 text-sm text-muted">
                    <span class="pill pill-brand figure" dir="ltr">{{ o.discountPercent !== null ? '−' + o.discountPercent + ' %' : '−' + (o.discountAmount | number: '1.0-3' : language.locale()) + ' TND' }}</span>
                    @if (o.months) { <span>{{ 'billing.offers.forMonths' | transloco: { n: o.months } }}</span> }
                  </span>
                </li>
              }
            </ul>
          </section>
        }

        <section class="card mt-6 overflow-hidden">
          <div class="card-header"><h2 class="section-title">{{ 'billing.statement.title' | transloco }}</h2></div>
          <app-statement-table [months]="b.months" invoiceLink="/my-store/billing/invoices" [currentMonth]="b.currentMonth.month" />
        </section>

        <section class="card mt-6 overflow-hidden">
          <div class="card-header"><h2 class="section-title">{{ 'billing.payments.title' | transloco }}</h2></div>
          <app-payment-list [payments]="b.payments" invoiceLink="/my-store/billing/invoices" />
        </section>

        @if (declaring()) {
          <app-declare-payment [monthlyPrice]="b.monthlyPrice" (declared)="done()" (closed)="declaring.set(false)" />
        }
      } @else {
        <p class="card empty-state">{{ 'common.loading' | transloco }}</p>
      }
    </main>
  `,
})
export class StoreBillingPage {
  private readonly api = inject(BillingApi);
  protected readonly language = inject(LanguageService);

  protected readonly billing = rxResource({ stream: () => this.api.myBilling() });
  protected readonly declaring = signal(false);
  protected readonly noStore = computed(() => {
    const err = this.billing.error();
    return err instanceof HttpErrorResponse && err.status === 404;
  });

  constructor() {
    const seo = inject(Seo);
    effect(() => seo.setKey('billing.mine.title'));
  }

  protected done(): void {
    this.declaring.set(false);
    this.billing.reload();
  }
}
