import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, numberAttribute, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { CoinEntry, CoinEntryType } from '../../../core/coins/coin-api';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { Modal } from '../../../shared/ui/modal';
import { FilterChip, FilterChips, shortId } from '../../../shared/ui/filter-chips';
import { Pagination } from '../../../shared/ui/pagination';
import { AdminApi } from '../admin-api';
import { AdminUserListItem } from '../admin.models';

const PAGE_SIZE = 25;

/**
 * `/admin/coins` — the coin ledger (019): every entry with what it traces back to, platform totals, and the only
 * way to change coins by hand — a signed adjustment with a reason (audited). Entries are never edited or deleted.
 */
@Component({
  selector: 'app-admin-coins',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe, FilterChips, Modal, Pagination],
  template: `
    <header class="page-header">
      <div class="min-w-0">
        <h1 class="page-title">{{ 'coins.admin.title' | transloco }}</h1>
        <p class="page-subtitle">{{ 'coins.admin.subtitle' | transloco }}</p>
      </div>
      <button type="button" (click)="openAdjust(null)" class="btn btn-primary">{{ 'coins.admin.adjust' | transloco }}</button>
    </header>

    <div class="space-y-6">
      @if (totals.value(); as t) {
        <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <div class="stat-tile"><p class="stat-label truncate">{{ 'coins.balance' | transloco }}</p><p class="stat-value" dir="ltr">{{ t.balance | number: '1.0-3' : language.locale() }}</p></div>
          <div class="stat-tile"><p class="stat-label truncate">{{ 'coins.earned' | transloco }}</p><p class="stat-value text-success" dir="ltr">{{ t.totalEarned | number: '1.0-3' : language.locale() }}</p></div>
          <div class="stat-tile"><p class="stat-label truncate">{{ 'coins.expected' | transloco }}</p><p class="stat-value text-ink-soft" dir="ltr">{{ t.expected | number: '1.0-3' : language.locale() }}</p></div>
          <div class="stat-tile"><p class="stat-label truncate">{{ 'coins.admin.affiliates' | transloco }}</p><p class="stat-value" dir="ltr">{{ t.affiliates }}</p></div>
        </div>
      }

      <div class="card overflow-hidden">
        <div class="panel-toolbar">
          <input type="search" [value]="search() ?? ''" (input)="onSearch($any($event.target).value)"
            [placeholder]="'coins.admin.searchPlaceholder' | transloco" [attr.aria-label]="'coins.admin.searchPlaceholder' | transloco" class="field min-w-0 flex-1 basis-56" />
          <select [value]="type() ?? ''" (change)="apply({ type: $any($event.target).value || null, page: null })" [attr.aria-label]="'coins.admin.type' | transloco" class="field w-full sm:w-auto">
            <option value="">{{ 'coins.admin.allTypes' | transloco }}</option>
            @for (t of types; track t) { <option [value]="t">{{ 'coins.type.' + t | transloco }}</option> }
          </select>
          <div class="flex w-full items-center gap-2 sm:w-auto" dir="ltr">
            <input type="date" [value]="from() ?? ''" (change)="apply({ from: $any($event.target).value || null, page: null })" [attr.aria-label]="'coins.admin.from' | transloco" class="field min-w-0 flex-1 sm:w-auto" />
            <span class="text-muted" aria-hidden="true">–</span>
            <input type="date" [value]="to() ?? ''" (change)="apply({ to: $any($event.target).value || null, page: null })" [attr.aria-label]="'coins.admin.to' | transloco" class="field min-w-0 flex-1 sm:w-auto" />
          </div>
        </div>
        <app-filter-chips [chips]="drillDown()" (cleared)="apply($event)" />

        @if (entries.value(); as result) {
          @if (result.items.length === 0) {
            <p class="empty-state">{{ 'coins.admin.none' | transloco }}</p>
          } @else {
            <ul class="divide-y divide-line">
              @for (e of result.items; track e.id) {
                <li class="row-hover grid gap-3 px-4 py-4 sm:px-5 md:grid-cols-[1.3fr_1.6fr_auto] md:items-center md:gap-6">
                  <div class="min-w-0">
                    <p class="flex flex-wrap items-center gap-2">
                      <a [routerLink]="['/admin/users', e.affiliateId]" class="font-semibold text-ink hover:text-brand">{{ e.affiliateName }}</a>
                      @if (e.affiliateCode) { <span class="code-chip" dir="ltr">#{{ e.affiliateCode }}</span> }
                    </p>
                    <p class="mt-1 text-xs text-muted">{{ e.createdAt | date: 'medium' : undefined : language.locale() }}</p>
                  </div>
                  <div class="min-w-0 text-sm">
                    <p class="flex flex-wrap items-center gap-2">
                      <span class="pill" [class]="e.type === 'Adjustment' ? 'pill-warning' : e.type === 'Earned' ? 'pill-success' : 'pill-neutral'">{{ 'coins.type.' + e.type | transloco }}</span>
                      @if (e.orderReference) { <a [routerLink]="['/admin/orders', e.orderReference]" class="link font-mono text-xs" dir="ltr">{{ e.orderReference }}</a> }
                    </p>
                    @if (e.productName) {
                      <p class="mt-1.5 flex flex-wrap gap-x-3 text-ink-soft">
                        <span class="min-w-0 truncate font-medium">{{ e.productName }}</span>
                        <span class="text-muted">{{ e.storeName }}</span>
                      </p>
                      <p class="mt-0.5 flex flex-wrap gap-x-3 gap-y-0.5 text-xs text-muted">
                        <span class="tabular-nums" dir="ltr">{{ e.quantity }} ×</span>
                        <span class="tabular-nums" dir="ltr">{{ e.lineTotal | number: '1.0-3' : language.locale() }} TND</span>
                        <span class="tabular-nums" dir="ltr">{{ e.affiliatePercent }} %</span>
                        <span>{{ e.clientName }} <span dir="ltr">{{ e.clientPhone }}</span></span>
                      </p>
                    }
                    @if (e.reason) { <p class="mt-1.5 rounded-lg border border-line bg-subtle px-2.5 py-1.5 text-xs text-ink-soft">{{ e.reason }}</p> }
                  </div>
                  <div class="flex items-center justify-between gap-3 md:justify-end">
                    <span class="figure text-lg" [class]="e.coins < 0 ? 'text-danger' : 'text-success'" dir="ltr">{{ e.coins > 0 ? '+' : '' }}{{ e.coins | number: '1.0-3' : language.locale() }}</span>
                    <button type="button" (click)="openAdjust(e)" class="btn btn-ghost btn-sm">{{ 'coins.admin.correct' | transloco }}</button>
                  </div>
                </li>
              }
            </ul>
            <div class="border-t border-line bg-subtle/40 px-4 py-3 sm:px-5">
              <app-pagination [page]="result.page" [pageSize]="pageSize" [total]="result.total" (pageChange)="apply({ page: $event })" />
            </div>
          }
        } @else {
          <p class="py-14 text-center text-sm text-muted">{{ 'common.loading' | transloco }}</p>
        }
      </div>
    </div>

    @if (adjusting()) {
      <app-modal [label]="'coins.admin.adjust' | transloco" (close)="close()">
        <h2 class="section-title">{{ 'coins.admin.adjust' | transloco }}</h2>
        <p class="mt-1 text-sm text-muted">{{ 'coins.admin.adjustHint' | transloco }}</p>

        @if (related(); as r) {
          <p class="alert-info mt-4">{{ 'coins.admin.correcting' | transloco: { name: r.affiliateName, reference: r.orderReference ?? '—' } }}</p>
        } @else {
          <label for="adjAffiliate" class="field-label mt-4">{{ 'coins.admin.affiliate' | transloco }}</label>
          <input id="adjAffiliate" type="search" [value]="affiliateQuery()" (input)="findAffiliate($any($event.target).value)"
            [placeholder]="'coins.admin.affiliatePlaceholder' | transloco" class="mt-1.5 field" autocomplete="off" />
          @if (candidates().length && !affiliate()) {
            <ul class="mt-1 max-h-48 overflow-y-auto rounded-lg border border-line bg-card shadow-card">
              @for (u of candidates(); track u.id) {
                <li><button type="button" (click)="pick(u)" class="flex w-full items-center justify-between gap-2 px-3 py-2 text-start text-sm hover:bg-subtle">
                  <span>{{ u.fullName }}</span><span class="code-chip" dir="ltr">#{{ u.affiliateCode }}</span>
                </button></li>
              }
            </ul>
          }
          @if (affiliate(); as a) {
            <p class="mt-2 flex flex-wrap items-center gap-2 text-sm"><span class="pill pill-brand gap-1.5"><span>{{ a.fullName }}</span><span dir="ltr">#{{ a.affiliateCode }}</span></span>
              <button type="button" (click)="affiliate.set(null)" class="text-xs text-muted hover:text-danger">{{ 'common.cancel' | transloco }}</button></p>
          }
        }

        <label for="adjCoins" class="field-label mt-4">{{ 'coins.admin.amount' | transloco }}</label>
        <input id="adjCoins" type="number" step="0.001" [value]="amount()" (input)="amount.set($any($event.target).value)" class="mt-1.5 field" dir="ltr" placeholder="-2.5" />
        <p class="field-hint">{{ 'coins.admin.amountHint' | transloco }}</p>

        <label for="adjReason" class="field-label mt-4">{{ 'coins.admin.reason' | transloco }}</label>
        <textarea id="adjReason" rows="3" maxlength="500" [value]="reason()" (input)="reason.set($any($event.target).value)" class="mt-1.5 field resize-y"></textarea>

        @if (error(); as key) { <p role="alert" class="alert-danger mt-4">{{ key | transloco }}</p> }
        <div class="mt-6 flex flex-wrap justify-end gap-2 border-t border-line pt-4">
          <button type="button" (click)="close()" class="btn btn-ghost">{{ 'common.cancel' | transloco }}</button>
          <button type="button" (click)="confirm()" [disabled]="busy() || !ready()" class="btn btn-primary">{{ 'coins.admin.save' | transloco }}</button>
        </div>
      </app-modal>
    }
  `,
})
export class AdminCoins {
  private readonly api = inject(AdminApi);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly type = input<CoinEntryType | undefined>();
  readonly search = input<string | undefined>();
  readonly from = input<string | undefined>();
  readonly to = input<string | undefined>();
  /** Set by a dashboard number (023). */
  readonly affiliateId = input<string | undefined>();
  readonly storeId = input<string | undefined>();
  readonly productId = input<string | undefined>();
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly types: CoinEntryType[] = ['Earned', 'Adjustment', 'PayoutLocked', 'PayoutReleased'];
  protected readonly pageSize = PAGE_SIZE;
  protected readonly totals = rxResource({ stream: () => this.api.coinTotals() });
  protected readonly entries = rxResource({
    params: () => ({
      type: this.type(), search: this.search(), from: this.from(), to: this.to(),
      affiliateId: this.affiliateId(), storeId: this.storeId(), productId: this.productId(), page: this.page(), pageSize: PAGE_SIZE,
    }),
    stream: ({ params }) => this.api.coinEntries(params),
  });

  protected readonly drillDown = computed(() => {
    const chips: FilterChip[] = [];
    if (this.affiliateId()) chips.push({ label: 'filters.affiliate', value: shortId(this.affiliateId()!), clear: { affiliateId: null } });
    if (this.storeId()) chips.push({ label: 'filters.store', value: shortId(this.storeId()!), clear: { storeId: null } });
    if (this.productId()) chips.push({ label: 'filters.product', value: shortId(this.productId()!), clear: { productId: null } });
    return chips;
  });

  protected readonly adjusting = signal(false);
  protected readonly related = signal<CoinEntry | null>(null);
  protected readonly affiliateQuery = signal('');
  protected readonly candidates = signal<AdminUserListItem[]>([]);
  protected readonly affiliate = signal<AdminUserListItem | null>(null);
  protected readonly amount = signal('');
  protected readonly reason = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  private searchTimer?: ReturnType<typeof setTimeout>;
  private affiliateTimer?: ReturnType<typeof setTimeout>;

  protected apply(params: Record<string, string | number | null>): void {
    this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge' });
  }

  protected onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.apply({ search: value.trim() || null, page: null }), 300);
  }

  /** Correct a given entry (its affiliate and the link are filled in), or start a free adjustment. */
  protected openAdjust(entry: CoinEntry | null): void {
    this.related.set(entry);
    this.affiliate.set(null);
    this.affiliateQuery.set('');
    this.candidates.set([]);
    this.amount.set(entry ? String(-entry.coins) : '');
    this.reason.set('');
    this.error.set(null);
    this.adjusting.set(true);
  }

  protected findAffiliate(value: string): void {
    this.affiliateQuery.set(value);
    this.affiliate.set(null);
    clearTimeout(this.affiliateTimer);
    if (value.trim().length < 2) {
      this.candidates.set([]);
      return;
    }
    this.affiliateTimer = setTimeout(() =>
      this.api.listUsers({ search: value.trim(), role: 'Affiliate', pageSize: 8 }).subscribe((r) => this.candidates.set(r.items)), 250);
  }

  protected pick(user: AdminUserListItem): void {
    this.affiliate.set(user);
    this.affiliateQuery.set(user.fullName);
  }

  protected close(): void {
    if (!this.busy()) this.adjusting.set(false);
  }

  protected ready(): boolean {
    const coins = Number(this.amount());
    return (!!this.related() || !!this.affiliate()) && !!this.reason().trim() && Number.isFinite(coins) && coins !== 0;
  }

  protected confirm(): void {
    if (!this.ready()) return;
    const affiliateId = this.related()?.affiliateId ?? this.affiliate()!.id;
    this.busy.set(true);
    this.api.adjustCoins(affiliateId, Number(this.amount()), this.reason().trim(), this.related()?.id ?? null).subscribe({
      next: () => {
        this.busy.set(false);
        this.adjusting.set(false);
        this.entries.reload();
        this.totals.reload();
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.busy.set(false);
      },
    });
  }
}
