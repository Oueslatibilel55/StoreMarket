import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { catchError, of } from 'rxjs';
import { bannerOf, BillingApi } from '../../core/billing/billing-api';
import { LanguageService } from '../../core/i18n/language.service';

/**
 * The store owner's billing alert (025), on every store-owner page: what is owed, the days left before the store is
 * closed, a payment waiting for approval, the reason of a rejection, or the suspension. Nothing when all is paid.
 * Reminders by notification come with 022.
 */
@Component({
  selector: 'app-billing-banner',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe],
  template: `
    @if (kind(); as k) {
      @let s = status.value()!;
      <div role="alert" class="mb-6 flex flex-col gap-3 rounded-xl border p-4 sm:flex-row sm:items-center sm:justify-between print:hidden" [class]="tone()">
        <div class="min-w-0 text-sm">
          <p class="font-semibold">
            @switch (k) {
              @case ('suspended') { {{ 'billing.banner.suspended' | transloco }} }
              @case ('overdue') { {{ 'billing.banner.overdue' | transloco }} }
              @case ('countdown') { {{ 'billing.banner.countdown' | transloco: { n: s.daysLeft } }} }
              @case ('waiting') { {{ 'billing.banner.waiting' | transloco }} }
              @case ('rejected') { {{ 'billing.banner.rejected' | transloco }} }
              @case ('due') { {{ 'billing.banner.due' | transloco: { amount: (s.commissionsDue | number: '1.0-3' : language.locale()) } }} }
            }
          </p>
          @if (k === 'rejected') {
            <p class="mt-1">{{ 'billing.banner.reason' | transloco }} {{ s.lastRejectionReason }}</p>
          } @else if (k !== 'waiting' && s.nextDue) {
            <p class="mt-1">
              {{ 'billing.banner.detail' | transloco: { amount: (s.commissionsDue | number: '1.0-3' : language.locale()), date: (s.nextDue | date: 'mediumDate' : undefined : language.locale()) } }}
            </p>
          }
        </div>
        <a routerLink="/my-store/billing" class="btn btn-sm shrink-0" [class]="k === 'waiting' || k === 'due' ? 'btn-secondary' : 'btn-danger'">
          {{ 'billing.banner.open' | transloco }}
        </a>
      </div>
    }
  `,
})
export class BillingBanner {
  private readonly api = inject(BillingApi);
  protected readonly language = inject(LanguageService);

  // No store yet (or no access): no banner, never an error on the page.
  protected readonly status = rxResource({ stream: () => this.api.myStatus().pipe(catchError(() => of(null))) });
  protected readonly kind = computed(() => bannerOf(this.status.value()));
  protected readonly tone = computed(() => {
    const k = this.kind();
    if (k === 'waiting' || k === 'due') return 'border-line bg-subtle text-ink';
    if (k === 'countdown' || k === 'rejected') return 'border-accent/40 bg-accent/10 text-ink';
    return 'border-danger/30 bg-danger/8 text-danger';
  });
}
