import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, numberAttribute } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../../core/i18n/language.service';
import { AdminPayoutApi, PAYOUT_STATUSES, PayoutMethod, PayoutStatus, payoutTone } from '../../../core/payouts/payout-api';
import { Pagination } from '../../../shared/ui/pagination';

const PAGE_SIZE = 25;

/** Local day as yyyy-MM-dd (what the date inputs and the API filters use). */
function day(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/**
 * `/admin/payouts` — payout requests (021): what waits for a check, what is planned, quick views for today's cash
 * handovers and this week's transfers. Filters live in the URL.
 */
@Component({
  selector: 'app-admin-payout-list',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe, Pagination],
  template: `
    <header class="page-header">
      <div class="min-w-0">
        <h1 class="page-title">{{ 'payouts.admin.title' | transloco }}</h1>
        <p class="page-subtitle">{{ 'payouts.admin.subtitle' | transloco }}</p>
      </div>
      <div class="flex flex-wrap gap-2">
        <button type="button" (click)="today()" class="btn btn-secondary btn-sm">{{ 'payouts.admin.todayCash' | transloco }}</button>
        <button type="button" (click)="week()" class="btn btn-secondary btn-sm">{{ 'payouts.admin.weekTransfers' | transloco }}</button>
        @if (filtered()) { <button type="button" (click)="clear()" class="btn btn-ghost btn-sm">{{ 'common.clearFilters' | transloco }}</button> }
      </div>
    </header>

    <div class="space-y-6">
      @if (counts.value(); as c) {
        <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <button type="button" (click)="apply({ status: 'Requested', from: null, to: null, method: null, page: null })" class="stat-tile cursor-pointer text-start transition-[translate,box-shadow,border-color] duration-200 hover:-translate-y-0.5 hover:border-brand/25 hover:shadow-card-hover">
            <p class="stat-label truncate">{{ 'payouts.admin.toVerify' | transloco }}</p><p class="stat-value" [class.text-accent-ink]="c.requested > 0" dir="ltr">{{ c.requested }}</p></button>
          <button type="button" (click)="apply({ status: 'Verified', from: null, to: null, method: null, page: null })" class="stat-tile cursor-pointer text-start transition-[translate,box-shadow,border-color] duration-200 hover:-translate-y-0.5 hover:border-brand/25 hover:shadow-card-hover">
            <p class="stat-label truncate">{{ 'payouts.admin.toSchedule' | transloco }}</p><p class="stat-value" dir="ltr">{{ c.verified }}</p></button>
          <button type="button" (click)="apply({ status: 'Scheduled', from: null, to: null, method: null, page: null })" class="stat-tile cursor-pointer text-start transition-[translate,box-shadow,border-color] duration-200 hover:-translate-y-0.5 hover:border-brand/25 hover:shadow-card-hover">
            <p class="stat-label truncate">{{ 'payouts.admin.planned' | transloco }}</p><p class="stat-value text-brand" dir="ltr">{{ c.scheduled }}</p></button>
          <div class="stat-tile"><p class="stat-label truncate">{{ 'payouts.admin.locked' | transloco }}</p>
            <p class="stat-value text-accent-ink" dir="ltr">{{ c.lockedCoins | number: '1.0-3' : language.locale() }}</p></div>
        </div>
      }

      <div class="card overflow-hidden">
        <div class="panel-toolbar">
          <input type="search" [value]="search() ?? ''" (input)="onSearch($any($event.target).value)"
            [placeholder]="'payouts.admin.searchPlaceholder' | transloco" [attr.aria-label]="'payouts.admin.searchPlaceholder' | transloco" class="field min-w-0 flex-1 basis-56" />
          <select [value]="status() ?? ''" (change)="apply({ status: $any($event.target).value || null, page: null })" [attr.aria-label]="'payouts.admin.status' | transloco" class="field w-full sm:w-auto">
            <option value="">{{ 'payouts.admin.allStatuses' | transloco }}</option>
            @for (s of statuses; track s) { <option [value]="s">{{ 'payouts.status.' + s | transloco }}</option> }
          </select>
          <select [value]="method() ?? ''" (change)="apply({ method: $any($event.target).value || null, page: null })" [attr.aria-label]="'payouts.form.method' | transloco" class="field w-full sm:w-auto">
            <option value="">{{ 'payouts.admin.allMethods' | transloco }}</option>
            @for (m of methods; track m) { <option [value]="m">{{ 'payouts.method.' + m | transloco }}</option> }
          </select>
          <div class="flex w-full items-center gap-2 sm:w-auto" dir="ltr">
            <input type="date" [value]="from() ?? ''" (change)="apply({ from: $any($event.target).value || null, page: null })" [attr.aria-label]="'payouts.admin.from' | transloco" [title]="'payouts.admin.from' | transloco" class="field min-w-0 flex-1 sm:w-auto" />
            <span class="text-muted" aria-hidden="true">–</span>
            <input type="date" [value]="to() ?? ''" (change)="apply({ to: $any($event.target).value || null, page: null })" [attr.aria-label]="'payouts.admin.to' | transloco" [title]="'payouts.admin.to' | transloco" class="field min-w-0 flex-1 sm:w-auto" />
          </div>
        </div>

        @if (payouts.value(); as result) {
          @if (result.items.length === 0) {
            <p class="empty-state">{{ 'payouts.admin.none' | transloco }}</p>
          } @else {
            <ul class="divide-y divide-line">
              @for (p of result.items; track p.id) {
                <li>
                  <a [routerLink]="['/admin/payouts', p.id]" class="row-hover grid gap-3 px-4 py-4 sm:px-5 md:grid-cols-[1.2fr_1fr_auto] md:items-center md:gap-6">
                    <div class="min-w-0">
                      <p class="flex flex-wrap items-center gap-2">
                        <span class="min-w-0 truncate font-semibold text-ink">{{ p.affiliateName }}</span>
                        @if (p.affiliateCode) { <span class="code-chip" dir="ltr">#{{ p.affiliateCode }}</span> }
                      </p>
                      <p class="mt-1 flex flex-wrap gap-x-3 text-xs text-muted">
                        <span class="font-mono" dir="ltr">{{ p.reference }}</span>
                        <span>{{ p.createdAt | date: 'medium' : undefined : language.locale() }}</span>
                      </p>
                    </div>
                    <div class="min-w-0 text-sm">
                      <p class="flex flex-wrap items-center gap-2">
                        <span class="pill" [class]="tone(p.status)">{{ 'payouts.status.' + p.status | transloco }}</span>
                        <span class="pill pill-neutral">{{ 'payouts.method.' + p.method | transloco }}</span>
                      </p>
                      @if (p.scheduledFor && p.status === 'Scheduled') {
                        <p class="mt-1.5 flex flex-wrap gap-x-3 text-xs text-brand">
                          <span>{{ p.scheduledFor | date: (p.method === 'Cash' ? 'medium' : 'mediumDate') : undefined : language.locale() }}</span>
                          @if (p.place) { <span class="min-w-0 truncate">{{ p.place }}</span> }
                        </p>
                      }
                    </div>
                    <p class="figure text-lg text-ink md:text-end" dir="ltr">{{ p.amountTnd | number: '1.0-3' : language.locale() }} <span class="text-xs font-medium text-muted">TND</span></p>
                  </a>
                </li>
              }
            </ul>
            <div class="border-t border-line bg-subtle/40 px-4 py-3 sm:px-5">
              <app-pagination [page]="result.page" [pageSize]="pageSize" [total]="result.total" (pageChange)="apply({ page: $event })" />
            </div>
          }
        } @else if (payouts.error()) {
          <p class="alert-danger m-4">{{ 'errors.unknown' | transloco }}</p>
        } @else {
          <p class="py-14 text-center text-sm text-muted">{{ 'common.loading' | transloco }}</p>
        }
      </div>
    </div>
  `,
})
export class AdminPayoutList {
  private readonly api = inject(AdminPayoutApi);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly status = input<PayoutStatus | undefined>();
  readonly method = input<PayoutMethod | undefined>();
  readonly from = input<string | undefined>();
  readonly to = input<string | undefined>();
  readonly search = input<string | undefined>();
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly statuses = PAYOUT_STATUSES;
  protected readonly methods: PayoutMethod[] = ['Cash', 'BankTransfer'];
  protected readonly pageSize = PAGE_SIZE;
  protected readonly tone = payoutTone;
  protected readonly filtered = computed(() => !!(this.status() || this.method() || this.from() || this.to() || this.search()));

  protected readonly counts = rxResource({ stream: () => this.api.counts() });
  protected readonly payouts = rxResource({
    params: () => ({ status: this.status(), method: this.method(), from: this.from(), to: this.to(), search: this.search(), page: this.page(), pageSize: PAGE_SIZE }),
    stream: ({ params }) => this.api.list(params),
  });
  private searchTimer?: ReturnType<typeof setTimeout>;

  protected apply(params: Record<string, string | number | null>): void {
    this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge' });
  }

  protected clear(): void {
    this.router.navigate([]);
  }

  protected onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.apply({ search: value.trim() || null, page: null }), 300);
  }

  /** Cash handovers planned today. */
  protected today(): void {
    const d = day(new Date());
    this.router.navigate([], { queryParams: { status: 'Scheduled', method: 'Cash', from: d, to: d } });
  }

  /** Transfers planned from Monday to Sunday of this week. */
  protected week(): void {
    const now = new Date();
    const monday = new Date(now);
    monday.setDate(now.getDate() - ((now.getDay() + 6) % 7));
    const sunday = new Date(monday);
    sunday.setDate(monday.getDate() + 6);
    this.router.navigate([], { queryParams: { status: 'Scheduled', method: 'BankTransfer', from: day(monday), to: day(sunday) } });
  }
}
