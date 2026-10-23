import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, numberAttribute, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { BillingApi, Offer, StorePayment } from '../../../core/billing/billing-api';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { PeriodQuery } from '../../../core/stats/stats-api';
import { PaymentList } from '../../../shared/billing/payment-list';
import { RejectDialog } from '../../../shared/billing/reject-dialog';
import { LocalizedPipe } from '../../../shared/i18n/localized';
import { PeriodPicker } from '../../../shared/stats/period-picker';
import { Pagination } from '../../../shared/ui/pagination';
import { OfferEditor } from './offer-editor';

export type BillingTab = 'queue' | 'overdue' | 'offers' | 'revenue';
const TABS: BillingTab[] = ['queue', 'overdue', 'offers', 'revenue'];
const PAGE_SIZE = 20;

/**
 * `/admin/billing?tab=` — store billing (025): payments declared by owners to check, stores late or suspended,
 * subscription offers, and revenue per month (subscriptions vs commissions billed / collected). "Run checks now" issues
 * the invoices of past months and suspends / reactivates stores, like the hourly job.
 */
@Component({
  selector: 'app-admin-billing',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe, LocalizedPipe, Pagination, PaymentList, PeriodPicker, RejectDialog, OfferEditor],
  template: `
    <header class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="page-title">{{ 'billing.admin.title' | transloco }}</h1>
        <p class="page-subtitle">{{ 'billing.admin.subtitle' | transloco }}</p>
      </div>
      <button type="button" class="btn btn-secondary btn-sm" [disabled]="busy()" (click)="runNow()">{{ 'billing.admin.run' | transloco }}</button>
    </header>
    @if (runResult(); as r) {
      <p class="alert-success mt-4">{{ 'billing.admin.ran' | transloco: { invoices: r.invoicesIssued, suspended: r.storesSuspended, reactivated: r.storesReactivated } }}</p>
    }
    @if (error()) { <p class="alert-danger mt-4">{{ error()! | transloco }}</p> }

    <nav class="mt-6 flex gap-1 overflow-x-auto border-b border-line" [attr.aria-label]="'billing.admin.title' | transloco">
      @for (t of tabs; track t) {
        <button type="button" (click)="go({ tab: t === 'queue' ? null : t, page: null })"
          class="-mb-px shrink-0 cursor-pointer border-b-2 px-3 py-2 text-sm font-medium transition-colors"
          [class]="current() === t ? 'border-brand text-brand' : 'border-transparent text-muted hover:text-ink'">
          {{ 'billing.admin.tabs.' + t | transloco }}
        </button>
      }
    </nav>

    @switch (current()) {
      @case ('queue') {
        <section class="card mt-6 overflow-hidden">
          @if (queue.value(); as q) {
            <app-payment-list [payments]="q.items" invoiceLink="/admin/billing/invoices" [actions]="true" [showStore]="true"
              (approve)="approve($event)" (reject)="rejecting.set($event)" />
            @if (q.total > pageSize) {
              <div class="border-t border-line px-5 py-3"><app-pagination [page]="q.page" [pageSize]="pageSize" [total]="q.total" (pageChange)="go({ page: $event })" /></div>
            }
          } @else { <p class="empty-state">{{ 'common.loading' | transloco }}</p> }
        </section>
      }
      @case ('overdue') {
        <section class="card mt-6 overflow-hidden">
          @if (overdue.value(); as list) {
            @if (list.length === 0) {
              <p class="empty-state">{{ 'billing.admin.noOverdue' | transloco }}</p>
            } @else {
              <ul class="divide-y divide-line">
                @for (s of list; track s.storeId) {
                  <li>
                    <a [routerLink]="['/admin/stores', s.storeId]" class="row-hover flex flex-wrap items-center justify-between gap-3 px-5 py-3.5">
                      <div class="min-w-0">
                        <p class="font-semibold text-ink">{{ s.storeName }}</p>
                        <p class="text-xs text-muted">{{ 'billing.paidUntil' | transloco }} {{ s.paidUntil | date: 'mediumDate' : undefined : language.locale() }}</p>
                      </div>
                      <div class="flex flex-wrap items-center gap-2">
                        @if (s.commissionsDue > 0) { <span class="text-sm font-semibold text-danger" dir="ltr">{{ s.commissionsDue | number: '1.0-3' : language.locale() }} TND</span> }
                        @if (s.status === 'Suspended') { <span class="pill pill-danger">{{ 'billing.stateLabel.suspended' | transloco }}</span> }
                        @else if (s.overdue) { <span class="pill pill-danger">{{ 'billing.stateLabel.overdue' | transloco }}</span> }
                        @else if (s.daysLeft !== null) { <span class="pill pill-warning">{{ 'billing.stateLabel.countdown' | transloco: { n: s.daysLeft } }}</span> }
                        @if (s.waitingForApproval) { <span class="pill pill-brand">{{ 'billing.stateLabel.waiting' | transloco }}</span> }
                      </div>
                    </a>
                  </li>
                }
              </ul>
            }
          } @else { <p class="empty-state">{{ 'common.loading' | transloco }}</p> }
        </section>
      }
      @case ('offers') {
        <div class="mt-6 flex justify-end">
          <button type="button" class="btn btn-primary btn-sm" (click)="editing.set('new')">{{ 'billing.offers.new' | transloco }}</button>
        </div>
        <section class="card mt-3 overflow-hidden">
          @if (offers.value(); as list) {
            @if (list.length === 0) {
              <p class="empty-state">{{ 'billing.offers.none' | transloco }}</p>
            } @else {
              <ul class="divide-y divide-line">
                @for (o of list; track o.id) {
                  <li class="flex flex-wrap items-center justify-between gap-3 px-5 py-3.5">
                    <div class="min-w-0">
                      <p class="flex flex-wrap items-center gap-2 font-semibold text-ink">
                        {{ o.name | localized: language.lang() }}
                        <span class="pill" [class]="o.isActive ? 'pill-success' : 'pill-neutral'">{{ (o.isActive ? 'billing.offers.on' : 'billing.offers.off') | transloco }}</span>
                      </p>
                      <p class="mt-0.5 text-xs text-muted">
                        <span dir="ltr">{{ o.discountPercent !== null ? '−' + o.discountPercent + ' %' : '−' + (o.discountAmount | number: '1.0-3' : language.locale()) + ' TND' }}</span>
                        · {{ o.months ? ('billing.offers.forMonths' | transloco: { n: o.months }) : ('billing.offers.anyMonths' | transloco) }}
                        · {{ 'billing.offers.targets.' + o.target | transloco }}@if (o.target !== 'AllStores') { ({{ o.targetIds.length }}) }
                        @if (o.firstPaymentOnly) { · {{ 'billing.offers.firstPaymentOnly' | transloco }} }
                        @if (o.startsOn || o.endsOn) { · {{ o.startsOn ?? '…' }} → {{ o.endsOn ?? '…' }} }
                      </p>
                    </div>
                    <div class="flex items-center gap-3">
                      <span class="text-xs text-muted">{{ 'billing.offers.used' | transloco: { n: o.timesUsed, amount: (o.totalDiscount | number: '1.0-3' : language.locale()) } }}</span>
                      <button type="button" class="btn btn-ghost btn-sm" (click)="editing.set(o)">{{ 'common.edit' | transloco }}</button>
                    </div>
                  </li>
                }
              </ul>
            }
          } @else { <p class="empty-state">{{ 'common.loading' | transloco }}</p> }
        </section>
      }
      @case ('revenue') {
        <div class="mt-6"><app-period-picker [from]="from()" [to]="to()" (changed)="setPeriod($event)" /></div>
        <section class="card mt-4 overflow-hidden">
          @if (revenue.value(); as r) {
            <div class="overflow-x-auto">
              <table class="w-full text-sm">
                <thead class="bg-subtle/60 text-xs text-muted">
                  <tr>
                    <th scope="col" class="px-4 py-2 text-start font-medium">{{ 'billing.statement.month' | transloco }}</th>
                    <th scope="col" class="px-4 py-2 text-end font-medium">{{ 'billing.revenue.subscriptions' | transloco }}</th>
                    <th scope="col" class="px-4 py-2 text-end font-medium">{{ 'billing.revenue.billed' | transloco }}</th>
                    <th scope="col" class="px-4 py-2 text-end font-medium">{{ 'billing.revenue.collected' | transloco }}</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-line">
                  @for (m of r.months; track m.month) {
                    <tr>
                      <td class="px-4 py-2.5 whitespace-nowrap">{{ m.month | date: 'MMMM y' : undefined : language.locale() }}</td>
                      <td class="px-4 py-2.5 text-end tabular-nums" dir="ltr">{{ m.subscriptions | number: '1.0-3' : language.locale() }}</td>
                      <td class="px-4 py-2.5 text-end tabular-nums" dir="ltr">{{ m.commissionsBilled | number: '1.0-3' : language.locale() }}</td>
                      <td class="px-4 py-2.5 text-end tabular-nums" dir="ltr">{{ m.commissionsCollected | number: '1.0-3' : language.locale() }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else { <p class="empty-state">{{ 'common.loading' | transloco }}</p> }
        </section>
      }
    }

    @if (rejecting(); as p) {
      <app-reject-dialog [payment]="p" [busy]="busy()" [error]="error()" (confirmed)="reject(p, $event)" (cancelled)="rejecting.set(null)" />
    }
    @if (editing(); as e) {
      <app-offer-editor [offer]="e === 'new' ? null : e" (saved)="offerSaved()" (closed)="editing.set(null)" />
    }
  `,
})
export class AdminBilling {
  private readonly api = inject(BillingApi);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly tab = input<BillingTab | undefined>();
  readonly from = input<string | undefined>();
  readonly to = input<string | undefined>();
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly tabs = TABS;
  protected readonly pageSize = PAGE_SIZE;
  protected readonly current = computed<BillingTab>(() => (TABS.includes(this.tab() as BillingTab) ? this.tab()! : 'queue'));

  protected readonly queue = rxResource({
    params: () => (this.current() === 'queue' ? { page: this.page() } : undefined),
    stream: ({ params }) => (params ? this.api.payments({ status: 'Declared', page: params.page, pageSize: PAGE_SIZE }) : of(undefined)),
  });
  protected readonly overdue = rxResource({
    params: () => this.current() === 'overdue' || undefined,
    stream: ({ params }) => (params ? this.api.overdue() : of(undefined)),
  });
  protected readonly offers = rxResource({
    params: () => this.current() === 'offers' || undefined,
    stream: ({ params }) => (params ? this.api.offers() : of(undefined)),
  });
  protected readonly revenue = rxResource({
    params: () => (this.current() === 'revenue' ? { from: this.from(), to: this.to() } : undefined),
    stream: ({ params }) => (params ? this.api.revenue(params) : of(undefined)),
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly rejecting = signal<StorePayment | null>(null);
  protected readonly editing = signal<Offer | 'new' | null>(null);
  protected readonly runResult = signal<{ invoicesIssued: number; storesSuspended: number; storesReactivated: number } | null>(null);

  protected go(params: Record<string, string | number | null>): void {
    this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge' });
  }

  protected setPeriod(q: PeriodQuery): void {
    this.go({ from: q.from ?? null, to: q.to ?? null });
  }

  protected approve(p: StorePayment): void {
    this.run(this.api.approve(p.id), () => this.queue.reload());
  }

  protected reject(p: StorePayment, reason: string): void {
    this.run(this.api.reject(p.id, reason), () => {
      this.rejecting.set(null);
      this.queue.reload();
    });
  }

  protected offerSaved(): void {
    this.editing.set(null);
    this.offers.reload();
  }

  protected runNow(): void {
    this.runResult.set(null);
    this.run(this.api.run(), (r) => {
      this.runResult.set(r as { invoicesIssued: number; storesSuspended: number; storesReactivated: number });
      this.overdue.reload();
    });
  }

  private run(call: Observable<unknown>, done: (value: unknown) => void): void {
    this.busy.set(true);
    this.error.set(null);
    call.subscribe({
      next: (value) => {
        this.busy.set(false);
        done(value);
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(errorKey(err));
      },
    });
  }
}
