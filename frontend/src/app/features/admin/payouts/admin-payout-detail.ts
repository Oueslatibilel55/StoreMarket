import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { AdminPayoutApi, formatRib, Payout, payoutTone } from '../../../core/payouts/payout-api';
import { Seo } from '../../../core/seo/seo';
import { PayoutTimeline } from '../../../shared/payouts/payout-timeline';
import { Modal } from '../../../shared/ui/modal';

type Step = 'verify' | 'reject' | 'schedule' | 'paid';

/** Local day as yyyy-MM-dd. */
function today(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/**
 * `/admin/payouts/:id` — the verification screen (021): the request, the affiliate, every earned commission it pays
 * (oldest coins first) with whether its store was billed (025), the affiliate's other requests, and the next step.
 * The admin decides case by case; every step is audited.
 */
@Component({
  selector: 'app-admin-payout-detail',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe, Modal, PayoutTimeline],
  template: `
    <a routerLink="/admin/payouts" class="link text-sm"><span class="inline-block rtl:rotate-180" aria-hidden="true">←</span> {{ 'payouts.admin.title' | transloco }}</a>

    @if (detail.value(); as d) {
      <header class="mt-3 flex flex-wrap items-end justify-between gap-4">
        <div class="min-w-0">
          <p class="flex flex-wrap items-center gap-2">
            <span class="pill" [class]="tone(d.payout.status)">{{ 'payouts.status.' + d.payout.status | transloco }}</span>
            <span class="pill pill-neutral">{{ 'payouts.method.' + d.payout.method | transloco }}</span>
          </p>
          <h1 class="page-title mt-2 font-mono" dir="ltr">{{ d.payout.reference }}</h1>
          <p class="page-subtitle">{{ 'payouts.admin.requestedOn' | transloco: { date: (d.payout.createdAt | date: 'medium' : undefined : language.locale()) } }}</p>
        </div>
        <div class="text-end">
          <p class="text-3xl font-bold tracking-tight text-ink" dir="ltr">{{ d.payout.amountTnd | number: '1.0-3' : language.locale() }} <span class="text-base text-muted">TND</span></p>
          <p class="text-xs text-muted" dir="ltr">{{ d.payout.coins | number: '1.0-3' : language.locale() }} {{ 'settings.units.coins' | transloco }} × {{ d.payout.coinValueInTnd | number: '1.0-3' : language.locale() }} TND</p>
        </div>
      </header>

      @if (error(); as key) { <p role="alert" class="alert-danger mt-4">{{ key | transloco }}</p> }

      @if (d.payout.status === 'Requested' || d.payout.status === 'Verified' || d.payout.status === 'Scheduled') {
        <div class="card mt-6 flex flex-wrap items-center justify-between gap-3 p-4">
          <p class="text-sm text-ink-soft">{{ 'payouts.admin.next.' + d.payout.status | transloco }}</p>
          <div class="flex flex-wrap gap-2">
            @if (d.payout.status === 'Requested') {
              <button type="button" (click)="open('verify')" class="btn btn-primary btn-sm">{{ 'payouts.admin.verify' | transloco }}</button>
            }
            @if (d.payout.status === 'Verified') {
              <button type="button" (click)="open('schedule')" class="btn btn-primary btn-sm">{{ 'payouts.admin.schedule' | transloco }}</button>
            }
            @if (d.payout.status === 'Scheduled') {
              <button type="button" (click)="open('paid')" class="btn btn-success btn-sm">{{ 'payouts.admin.markPaid' | transloco }}</button>
              <button type="button" (click)="open('schedule')" class="btn btn-secondary btn-sm">{{ 'payouts.admin.reschedule' | transloco }}</button>
            }
            @if (d.payout.status !== 'Scheduled') {
              <button type="button" (click)="open('reject')" class="btn btn-danger-soft btn-sm">{{ 'payouts.admin.reject' | transloco }}</button>
            }
          </div>
        </div>
      }

      <div class="mt-6 grid gap-6 lg:grid-cols-[1.6fr_1fr]">
        <div class="min-w-0 space-y-6">
          <section class="card p-5 sm:p-6">
            <h2 class="section-title">{{ 'payouts.admin.request' | transloco }}</h2>
            <dl class="mt-4 grid gap-4 text-sm sm:grid-cols-2">
              @if (d.payout.method === 'BankTransfer') {
                <div><dt class="eyebrow">{{ 'payouts.form.bankName' | transloco }}</dt><dd class="mt-1 text-ink">{{ d.payout.bankName }}</dd></div>
                <div><dt class="eyebrow">{{ 'payouts.form.accountHolder' | transloco }}</dt><dd class="mt-1 text-ink">{{ d.payout.accountHolder }}</dd></div>
                <div class="sm:col-span-2"><dt class="eyebrow">{{ 'payouts.form.rib' | transloco }}</dt><dd class="mt-1 font-mono text-ink" dir="ltr">{{ rib(d.payout.rib) }}</dd></div>
              }
              @if (d.payout.scheduledFor) {
                <div><dt class="eyebrow">{{ 'payouts.admin.scheduledFor' | transloco }}</dt>
                  <dd class="mt-1 font-medium text-brand">{{ d.payout.scheduledFor | date: (d.payout.method === 'Cash' ? 'full' : 'fullDate') : undefined : language.locale() }}</dd></div>
              }
              @if (d.payout.place) { <div><dt class="eyebrow">{{ 'payouts.admin.place' | transloco }}</dt><dd class="mt-1 text-ink">{{ d.payout.place }}</dd></div> }
              @if (d.payout.paidAt) {
                <div><dt class="eyebrow">{{ 'payouts.admin.paidAt' | transloco }}</dt><dd class="mt-1 text-ink">{{ d.payout.paidAt | date: 'medium' : undefined : language.locale() }}</dd></div>
                <div><dt class="eyebrow">{{ 'payouts.admin.paidReference' | transloco }}</dt><dd class="mt-1 text-ink">{{ d.payout.paidReference }}</dd></div>
              }
              @if (d.payout.note) {
                <div class="sm:col-span-2"><dt class="eyebrow">{{ 'payouts.form.note' | transloco }}</dt><dd class="mt-1 rounded-lg bg-subtle px-3 py-2 text-ink-soft">{{ d.payout.note }}</dd></div>
              }
              @if (d.payout.rejectionReason) {
                <div class="sm:col-span-2"><dt class="eyebrow">{{ 'payouts.admin.rejectionReason' | transloco }}</dt><dd class="mt-1 text-danger">{{ d.payout.rejectionReason }}</dd></div>
              }
            </dl>
          </section>

          <section class="card overflow-hidden">
            <div class="card-header flex-wrap gap-2">
              <div class="min-w-0">
                <h2 class="section-title">{{ 'payouts.admin.covered' | transloco }}</h2>
                <p class="mt-0.5 text-sm text-muted">{{ 'payouts.admin.coveredHint' | transloco }}</p>
              </div>
              <span class="pill" [class]="uncovered() > 0 ? 'pill-warning' : 'pill-success'" dir="ltr">
                {{ coveredTotal() | number: '1.0-3' : language.locale() }} / {{ d.payout.coins | number: '1.0-3' : language.locale() }}</span>
            </div>
            @if (uncovered() > 0) {
              <p class="alert-info mx-4 mt-4">{{ 'payouts.admin.fromAdjustments' | transloco: { coins: (uncovered() | number: '1.0-3' : language.locale()), total: (d.adjustmentsTotal | number: '1.0-3' : language.locale()) } }}</p>
            }
            @if (d.covered.length === 0) {
              <p class="empty-state py-8">{{ 'payouts.admin.coveredNone' | transloco }}</p>
            } @else {
              <ul class="divide-y divide-line">
                @for (e of d.covered; track e.entryId) {
                  <li class="grid gap-2 px-5 py-3.5 text-sm sm:grid-cols-[1fr_auto] sm:items-center sm:px-6">
                    <div class="min-w-0">
                      <p class="truncate font-medium text-ink">{{ e.productName }} <span class="font-normal text-muted">· {{ e.storeName }}</span></p>
                      <p class="text-xs text-muted">
                        @if (e.orderReference) { <a [routerLink]="['/admin/orders', e.orderReference]" class="link font-mono" dir="ltr">{{ e.orderReference }}</a> · }
                        {{ 'payouts.admin.deliveredOn' | transloco: { date: (e.creditedAt | date: 'mediumDate' : undefined : language.locale()) } }}
                      </p>
                    </div>
                    <div class="flex flex-wrap items-center gap-2 sm:justify-end">
                      <span class="pill" [class]="e.invoiceId ? 'pill-success' : 'pill-neutral'">{{ (e.invoiceId ? 'payouts.admin.invoiced' : 'payouts.admin.notInvoiced') | transloco }}</span>
                      <span class="font-semibold text-ink" dir="ltr">{{ e.coveredCoins | number: '1.0-3' : language.locale() }}
                        @if (e.coveredCoins !== e.coins) { <span class="text-xs font-normal text-muted">/ {{ e.coins | number: '1.0-3' : language.locale() }}</span> }</span>
                    </div>
                  </li>
                }
              </ul>
            }
          </section>
        </div>

        <aside class="min-w-0 space-y-6">
          <section class="card p-5">
            <h2 class="section-title">{{ 'payouts.admin.affiliate' | transloco }}</h2>
            <a [routerLink]="['/admin/users', d.affiliate.id]" class="mt-3 block font-semibold text-ink hover:text-brand">{{ d.affiliate.fullName }}</a>
            <p class="mt-1 flex flex-wrap items-center gap-2 text-sm">
              @if (d.affiliate.code) { <span class="code-chip" dir="ltr">#{{ d.affiliate.code }}</span> }
              @if (!d.affiliate.isActive) { <span class="pill pill-danger">{{ 'admin.status.Blocked' | transloco }}</span> }
              @else if (!d.affiliate.canEarn) { <span class="pill pill-warning">{{ 'payouts.admin.cannotEarn' | transloco }}</span> }
            </p>
            <p class="mt-2 truncate text-sm text-muted" dir="ltr">{{ d.affiliate.email }}</p>
            @if (d.affiliate.phone) { <p class="text-sm text-muted" dir="ltr">{{ d.affiliate.phone }}</p> }
            <div class="mt-4 grid grid-cols-2 gap-3 border-t border-line pt-4">
              <div><p class="eyebrow">{{ 'coins.balance' | transloco }}</p><p class="mt-1 font-bold text-accent-ink" dir="ltr">{{ d.affiliate.balance | number: '1.0-3' : language.locale() }}</p></div>
              <div><p class="eyebrow">{{ 'coins.earned' | transloco }}</p><p class="mt-1 font-bold" dir="ltr">{{ d.affiliate.totalEarned | number: '1.0-3' : language.locale() }}</p></div>
            </div>
            <a routerLink="/admin/coins" [queryParams]="{ search: d.affiliate.code ?? d.affiliate.email }" class="link mt-4 inline-block text-xs">{{ 'payouts.admin.seeLedger' | transloco }}</a>
          </section>

          <section class="card p-5">
            <h2 class="section-title mb-4">{{ 'payouts.admin.steps' | transloco }}</h2>
            <app-payout-timeline [history]="d.payout.history" [affiliateName]="d.payout.affiliateName" />
          </section>

          <section class="card overflow-hidden">
            <div class="card-header"><h2 class="section-title">{{ 'payouts.admin.others' | transloco }}</h2></div>
            @if (d.otherPayouts.length === 0) {
              <p class="empty-state py-6">{{ 'payouts.admin.othersNone' | transloco }}</p>
            } @else {
              <ul class="divide-y divide-line">
                @for (o of d.otherPayouts; track o.id) {
                  <li><a [routerLink]="['/admin/payouts', o.id]" class="row-hover flex items-center justify-between gap-2 px-5 py-3 text-sm">
                    <span class="min-w-0"><span class="block font-mono text-xs text-ink-soft" dir="ltr">{{ o.reference }}</span>
                      <span class="text-xs text-muted">{{ o.createdAt | date: 'mediumDate' : undefined : language.locale() }}</span></span>
                    <span class="flex shrink-0 items-center gap-2"><span class="pill" [class]="tone(o.status)">{{ 'payouts.status.' + o.status | transloco }}</span>
                      <span class="font-semibold" dir="ltr">{{ o.amountTnd | number: '1.0-3' : language.locale() }}</span></span>
                  </a></li>
                }
              </ul>
            }
          </section>
        </aside>
      </div>

      @if (step(); as s) {
        <app-modal [label]="'payouts.admin.' + s + 'Title' | transloco" (close)="step.set(null)">
          <h2 class="section-title">{{ 'payouts.admin.' + s + 'Title' | transloco }}</h2>
          <p class="mt-1 text-sm text-muted">{{ 'payouts.admin.' + s + 'Hint' | transloco }}</p>

          @switch (s) {
            @case ('verify') {
              <label for="poNote" class="field-label mt-4">{{ 'payouts.admin.noteOptional' | transloco }}</label>
              <textarea id="poNote" rows="2" maxlength="500" [value]="text()" (input)="text.set($any($event.target).value)" class="mt-1.5 field resize-y"></textarea>
            }
            @case ('reject') {
              <label for="poReason" class="field-label mt-4">{{ 'payouts.admin.reason' | transloco }}</label>
              <textarea id="poReason" rows="3" maxlength="500" [value]="text()" (input)="text.set($any($event.target).value)" class="mt-1.5 field resize-y"></textarea>
            }
            @case ('schedule') {
              <label for="poWhen" class="field-label mt-4">{{ (d.payout.method === 'Cash' ? 'payouts.admin.whenCash' : 'payouts.admin.whenTransfer') | transloco }}</label>
              <input id="poWhen" [type]="d.payout.method === 'Cash' ? 'datetime-local' : 'date'" [min]="minDay" [value]="when()" (input)="when.set($any($event.target).value)" class="mt-1.5 field" dir="ltr" />
              @if (d.payout.method === 'Cash') {
                <label for="poPlace" class="field-label mt-4">{{ 'payouts.admin.place' | transloco }}</label>
                <input id="poPlace" type="text" maxlength="200" [value]="text()" (input)="text.set($any($event.target).value)" class="mt-1.5 field"
                  [placeholder]="'payouts.admin.placePlaceholder' | transloco" />
              }
            }
            @case ('paid') {
              <label for="poRef" class="field-label mt-4">{{ (d.payout.method === 'Cash' ? 'payouts.admin.handedBy' : 'payouts.admin.bankReference') | transloco }}</label>
              <input id="poRef" type="text" maxlength="120" [value]="text()" (input)="text.set($any($event.target).value)" class="mt-1.5 field" />
              <label for="poPaidAt" class="field-label mt-4">{{ 'payouts.admin.paidAt' | transloco }}</label>
              <input id="poPaidAt" type="date" [max]="minDay" [value]="when()" (input)="when.set($any($event.target).value)" class="mt-1.5 field" dir="ltr" />
            }
          }

          @if (stepError(); as key) { <p role="alert" class="alert-danger mt-4">{{ key | transloco }}</p> }
          <div class="mt-6 flex justify-end gap-3">
            <button type="button" (click)="step.set(null)" class="btn btn-ghost">{{ 'common.cancel' | transloco }}</button>
            <button type="button" (click)="confirm(d.payout)" [disabled]="busy() || !ready(d.payout)" class="btn" [class]="s === 'reject' ? 'btn-danger' : 'btn-primary'">
              {{ 'payouts.admin.' + s + 'Confirm' | transloco }}</button>
          </div>
        </app-modal>
      }
    } @else if (detail.error()) {
      <p class="alert-danger mt-6">{{ errorKeyOf(detail.error()) | transloco }}</p>
    } @else {
      <p class="empty-state">{{ 'common.loading' | transloco }}</p>
    }
  `,
})
export class AdminPayoutDetailPage {
  private readonly api = inject(AdminPayoutApi);
  protected readonly language = inject(LanguageService);
  readonly id = input.required<string>();

  protected readonly detail = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.detail(params) });
  protected readonly coveredTotal = computed(() => (this.detail.value()?.covered ?? []).reduce((sum, e) => sum + e.coveredCoins, 0));
  /** Coins of the request not matched by earned entries (they come from adjustments). */
  protected readonly uncovered = computed(() => Math.max(0, Math.round(((this.detail.value()?.payout.coins ?? 0) - this.coveredTotal()) * 1000) / 1000));

  protected readonly tone = payoutTone;
  protected readonly rib = formatRib;
  protected readonly errorKeyOf = errorKey;
  protected readonly minDay = today();

  protected readonly step = signal<Step | null>(null);
  protected readonly text = signal('');
  protected readonly when = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly stepError = signal<string | null>(null);

  constructor() {
    const seo = inject(Seo);
    effect(() => {
      this.language.lang();
      seo.setKey('payouts.admin.title');
    });
  }

  protected open(step: Step): void {
    const p = this.detail.value()!.payout;
    this.text.set(step === 'schedule' ? (p.place ?? '') : '');
    this.when.set(step === 'paid' ? today() : step === 'schedule' && p.scheduledFor ? this.local(p.scheduledFor, p.method === 'Cash') : '');
    this.stepError.set(null);
    this.step.set(step);
  }

  protected ready(p: Payout): boolean {
    switch (this.step()) {
      case 'reject': return !!this.text().trim();
      case 'schedule': return !!this.when() && (p.method !== 'Cash' || !!this.text().trim());
      case 'paid': return !!this.text().trim();
      default: return true;
    }
  }

  protected confirm(p: Payout): void {
    const text = this.text().trim();
    let call: Observable<Payout>;
    switch (this.step()) {
      case 'verify': call = this.api.verify(p.id, text || null); break;
      case 'reject': call = this.api.reject(p.id, text); break;
      case 'schedule': call = this.api.schedule(p.id, this.scheduledIso(p), p.method === 'Cash' ? text : null); break;
      case 'paid': call = this.api.markPaid(p.id, text, this.when() && this.when() !== today() ? `${this.when()}T12:00:00Z` : null); break;
      default: return;
    }
    this.busy.set(true);
    this.stepError.set(null);
    call.subscribe({
      next: () => {
        this.busy.set(false);
        this.step.set(null);
        this.detail.reload();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        const key = errorKey(err);
        this.stepError.set(key);
        // Someone else moved it meanwhile: show the current state.
        if (key === 'errors.payout.changed' || key === 'errors.payout.transitionNotAllowed') this.detail.reload();
      },
    });
  }

  /** Cash: the local date + time the admin picked. Transfer: that day, at noon UTC so no time zone moves it. */
  private scheduledIso(p: Payout): string {
    return p.method === 'Cash' ? new Date(this.when()).toISOString() : `${this.when()}T12:00:00Z`;
  }

  /** ISO → value of a datetime-local / date input, in local time. */
  private local(iso: string, withTime: boolean): string {
    const d = new Date(iso);
    const pad = (n: number) => String(n).padStart(2, '0');
    const date = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
    return withTime ? `${date}T${pad(d.getHours())}:${pad(d.getMinutes())}` : date;
  }
}
