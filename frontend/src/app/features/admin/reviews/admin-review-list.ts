import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, numberAttribute, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { AdminReview, MAX_REASON_LENGTH, ReviewApi, ReviewTarget } from '../../../core/reviews/review-api';
import { StarRating } from '../../../shared/reviews/star-rating';
import { Modal } from '../../../shared/ui/modal';
import { Pagination } from '../../../shared/ui/pagination';

const PAGE_SIZE = 20;

/**
 * `/admin/reviews?target=&hidden=&stars=&search=&page=` — every review (017); hide an abusive one or show it again,
 * always with a reason (audited). Hidden reviews leave the public pages and the averages.
 */
@Component({
  selector: 'app-admin-review-list',
  imports: [DatePipe, RouterLink, TranslocoPipe, Modal, Pagination, StarRating],
  template: `
    <header class="page-header">
      <div class="min-w-0">
        <h1 class="page-title">{{ 'reviews.admin.title' | transloco }}</h1>
        <p class="page-subtitle">{{ 'reviews.admin.subtitle' | transloco }}</p>
      </div>
    </header>

    <div class="space-y-6">
      @if (counts.value(); as c) {
        <div class="grid grid-cols-3 gap-3">
          <button type="button" (click)="clear()" class="stat-tile min-w-0 cursor-pointer text-start transition-[translate,box-shadow,border-color] duration-200 hover:-translate-y-0.5 hover:border-brand/25 hover:shadow-card-hover">
            <p class="stat-label truncate">{{ 'reviews.admin.total' | transloco }}</p>
            <p class="stat-value" dir="ltr">{{ c.total }}</p>
          </button>
          <div class="stat-tile min-w-0">
            <p class="stat-label truncate">{{ 'reviews.admin.lowStars' | transloco }}</p>
            <p class="stat-value" [class.text-accent-ink]="c.lowStars > 0" dir="ltr">{{ c.lowStars }}</p>
          </div>
          <button type="button" (click)="apply({ hidden: 'true', stars: null, page: null })" class="stat-tile min-w-0 cursor-pointer text-start transition-[translate,box-shadow,border-color] duration-200 hover:-translate-y-0.5 hover:border-brand/25 hover:shadow-card-hover">
            <p class="stat-label truncate">{{ 'reviews.admin.hidden' | transloco }}</p>
            <p class="stat-value text-danger" dir="ltr">{{ c.hidden }}</p>
          </button>
        </div>
      }

      <div class="card overflow-hidden">
        <div class="panel-toolbar">
          <input type="search" [value]="search() ?? ''" (input)="onSearch($any($event.target).value)"
            [placeholder]="'reviews.admin.searchPlaceholder' | transloco" [attr.aria-label]="'reviews.admin.searchPlaceholder' | transloco"
            class="field min-w-0 flex-1 basis-56" />
          <select [value]="target() ?? ''" (change)="apply({ target: $any($event.target).value || null, page: null })"
            [attr.aria-label]="'reviews.admin.target' | transloco" class="field w-full sm:w-auto">
            <option value="">{{ 'reviews.admin.allTargets' | transloco }}</option>
            @for (t of targets; track t) { <option [value]="t">{{ 'reviews.target.' + t | transloco }}</option> }
          </select>
          <select [value]="stars() ?? ''" (change)="apply({ stars: $any($event.target).value || null, page: null })"
            [attr.aria-label]="'reviews.admin.stars' | transloco" class="field w-full sm:w-auto">
            <option value="">{{ 'reviews.admin.allStars' | transloco }}</option>
            @for (n of [1, 2, 3, 4, 5]; track n) { <option [value]="n">{{ 'reviews.nStars' | transloco: { n } }}</option> }
          </select>
          <select [value]="hidden() ?? ''" (change)="apply({ hidden: $any($event.target).value || null, page: null })"
            [attr.aria-label]="'reviews.admin.visibility' | transloco" class="field w-full sm:w-auto">
            <option value="">{{ 'reviews.admin.allVisibility' | transloco }}</option>
            <option value="false">{{ 'reviews.admin.visible' | transloco }}</option>
            <option value="true">{{ 'reviews.admin.hidden' | transloco }}</option>
          </select>
          @if (hasFilters()) {
            <button type="button" (click)="clear()" class="btn btn-ghost btn-sm">{{ 'common.clearFilters' | transloco }}</button>
          }
        </div>

        @if (reviews.error()) {
          <p class="alert-danger m-4 sm:m-5">{{ 'errors.unknown' | transloco }}</p>
        } @else if (reviews.value(); as result) {
          @if (result.items.length === 0) {
            <p class="empty-state">{{ 'reviews.admin.empty' | transloco }}</p>
          } @else {
            <ul class="divide-y divide-line">
              @for (r of result.items; track r.id) {
                <li class="list-row row-hover sm:items-start">
                  <div class="min-w-0">
                    <p class="flex flex-wrap items-center gap-2">
                      <app-star-rating [value]="r.stars" size="sm" />
                      <span class="pill pill-neutral">{{ 'reviews.target.' + r.target | transloco }}</span>
                      @if (r.hiddenAt) { <span class="pill pill-danger">{{ 'reviews.hiddenPill' | transloco }}</span> }
                    </p>
                    <p class="mt-2 flex flex-wrap gap-x-3 gap-y-0.5 text-sm font-medium text-ink">
                      @if (r.target === 'Product') {
                        @if (r.productSlug) { <a [routerLink]="['/products', r.productSlug]" class="hover:text-brand">{{ r.productName }}</a> } @else { <span>{{ r.productName }}</span> }
                      }
                      <a [routerLink]="['/admin/stores', r.storeId]" class="text-ink-soft hover:text-brand">{{ r.storeName }}</a>
                    </p>
                    @if (r.comment) {
                      <p class="mt-1.5 text-sm whitespace-pre-line text-ink-soft" dir="auto">{{ r.comment }}</p>
                    } @else {
                      <p class="mt-1.5 text-sm text-muted italic">{{ 'reviews.noComment' | transloco }}</p>
                    }
                    @if (r.reply) {
                      <p class="mt-2 border-s-2 border-brand ps-3 text-xs text-ink-soft" dir="auto"><span class="font-semibold">{{ 'reviews.storeReply' | transloco }} :</span> {{ r.reply }}</p>
                    }
                    @if (r.hiddenReason) {
                      <p class="mt-2 text-xs text-danger">{{ 'reviews.admin.hiddenBecause' | transloco: { reason: r.hiddenReason } }}</p>
                    }
                    <p class="mt-2 flex flex-wrap gap-x-3 gap-y-0.5 text-xs text-muted">
                      <a [routerLink]="['/admin/users', r.authorId]" class="font-medium text-ink-soft hover:text-brand">{{ r.authorFullName }}</a>
                      <span class="min-w-0 truncate" dir="ltr">{{ r.authorEmail }}</span>
                      <span class="font-mono" dir="ltr">{{ r.orderReference }}</span>
                      <span>{{ r.createdAt | date: 'medium' : undefined : language.locale() }}</span>
                    </p>
                  </div>
                  <div class="flex shrink-0 sm:justify-end">
                    @if (r.hiddenAt) {
                      <button type="button" (click)="ask(r, 'unhide')" class="btn btn-secondary btn-sm">{{ 'reviews.admin.unhide' | transloco }}</button>
                    } @else {
                      <button type="button" (click)="ask(r, 'hide')" class="btn btn-danger-soft btn-sm">{{ 'reviews.admin.hide' | transloco }}</button>
                    }
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

    @if (acting(); as a) {
      <app-modal [label]="'reviews.admin.' + a.action | transloco" (close)="close()">
        <h2 class="section-title">{{ 'reviews.admin.' + a.action + 'Title' | transloco }}</h2>
        <p class="mt-2 text-sm text-muted">{{ 'reviews.admin.' + a.action + 'Hint' | transloco }}</p>
        @if (a.review.comment) {
          <blockquote class="mt-3 rounded-lg bg-subtle px-3 py-2 text-sm text-ink-soft" dir="auto">« {{ a.review.comment }} »</blockquote>
        }
        <label for="moderationReason" class="field-label mt-4">{{ 'reviews.admin.reason' | transloco }}</label>
        <textarea id="moderationReason" rows="3" [attr.maxlength]="maxReason" [value]="reason()" (input)="reason.set($any($event.target).value)"
          class="mt-1.5 field resize-y"></textarea>
        @if (error(); as key) { <p role="alert" class="alert-danger mt-4">{{ key | transloco }}</p> }
        <div class="mt-6 flex flex-wrap justify-end gap-2 border-t border-line pt-4">
          <button type="button" (click)="close()" class="btn btn-ghost">{{ 'common.cancel' | transloco }}</button>
          <button type="button" (click)="confirm()" [disabled]="busy() || !reason().trim()" class="btn"
            [class]="a.action === 'hide' ? 'btn-danger' : 'btn-primary'">{{ 'reviews.admin.' + a.action | transloco }}</button>
        </div>
      </app-modal>
    }
  `,
})
export class AdminReviewList {
  private readonly api = inject(ReviewApi);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly target = input<ReviewTarget | undefined>();
  readonly hidden = input<'true' | 'false' | undefined>();
  readonly stars = input<string | undefined>();
  readonly search = input<string | undefined>();
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly targets: ReviewTarget[] = ['Product', 'Store'];
  protected readonly pageSize = PAGE_SIZE;
  protected readonly maxReason = MAX_REASON_LENGTH;
  protected readonly hasFilters = computed(() => !!this.target() || !!this.hidden() || !!this.stars() || !!this.search());

  protected readonly counts = rxResource({ stream: () => this.api.adminCounts() });
  protected readonly reviews = rxResource({
    params: () => ({
      target: this.target(),
      hidden: this.hidden() === undefined ? undefined : this.hidden() === 'true',
      stars: this.stars() ? Number(this.stars()) : undefined,
      search: this.search(),
      page: this.page(),
      pageSize: PAGE_SIZE,
    }),
    stream: ({ params }) => this.api.adminList(params),
  });

  protected readonly acting = signal<{ review: AdminReview; action: 'hide' | 'unhide' } | null>(null);
  protected readonly reason = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  private searchTimer?: ReturnType<typeof setTimeout>;

  protected apply(params: Record<string, string | number | null>): void {
    this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge' });
  }

  protected onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.apply({ search: value.trim() || null, page: null }), 300);
  }

  protected clear(): void {
    this.router.navigate([], { queryParams: {} });
  }

  protected ask(review: AdminReview, action: 'hide' | 'unhide'): void {
    this.error.set(null);
    this.reason.set('');
    this.acting.set({ review, action });
  }

  protected close(): void {
    if (!this.busy()) this.acting.set(null);
  }

  protected confirm(): void {
    const a = this.acting();
    const reason = this.reason().trim();
    if (!a || !reason) return;

    this.busy.set(true);
    const request = a.action === 'hide' ? this.api.hide(a.review.id, reason) : this.api.unhide(a.review.id, reason);
    request.subscribe({
      next: () => {
        this.busy.set(false);
        this.acting.set(null);
        this.reviews.reload();
        this.counts.reload();
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.busy.set(false);
      },
    });
  }
}
