import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../core/i18n/language.service';
import { ReviewApi } from '../../core/reviews/review-api';
import { Pagination } from '../ui/pagination';
import { StarRating } from './star-rating';

/**
 * Public reviews of a product or a store (017): average, 1★…5★ bars (click to filter), newest reviews with the
 * store's answer, paged. `<app-review-section kind="product" [slug]="p.slug" />`.
 */
@Component({
  selector: 'app-review-section',
  imports: [DatePipe, DecimalPipe, TranslocoPipe, Pagination, StarRating],
  template: `
    <section id="reviews" class="scroll-mt-24">
      <div class="flex flex-wrap items-end justify-between gap-3 border-b border-line pb-4">
        <div class="min-w-0">
          <h2 class="section-title text-2xl">{{ (kind() === 'product' ? 'reviews.productTitle' : 'reviews.storeTitle') | transloco }}</h2>
          @if (kind() === 'store') { <p class="mt-1 text-sm text-muted">{{ 'reviews.storeHint' | transloco }}</p> }
        </div>
      </div>

      @if (reviews.error()) {
        <p class="alert-danger mt-6">{{ 'errors.unknown' | transloco }}</p>
      } @else if (reviews.value(); as r) {
        @if (r.stats.count === 0) {
          <div class="card empty-state mt-6">
            <p class="font-medium text-ink-soft">{{ 'reviews.none' | transloco }}</p>
            <p class="text-xs">{{ 'reviews.verifiedHint' | transloco }}</p>
          </div>
        } @else {
          <div class="mt-6 grid gap-6 lg:grid-cols-[16rem_1fr] lg:gap-10">
            <div class="card self-start p-5 lg:sticky lg:top-24">
              <p class="flex items-baseline gap-2">
                <span class="figure text-5xl text-ink" dir="ltr">{{ r.stats.average | number: '1.1-1' : language.locale() }}</span>
                <span class="text-sm text-muted">/ 5</span>
              </p>
              <app-star-rating class="mt-1 block" [value]="r.stats.average" />
              <p class="mt-1 text-sm text-muted">{{ 'reviews.count' | transloco: { count: r.stats.count } }}</p>
              <ul class="mt-4 space-y-1.5">
                @for (n of [5, 4, 3, 2, 1]; track n) {
                  @let c = r.stats.stars[n - 1];
                  <li>
                    <button type="button" (click)="filter(n)" [disabled]="!c" [attr.aria-pressed]="stars() === n"
                      class="flex w-full items-center gap-2 rounded-md px-1 py-0.5 text-xs transition-colors enabled:cursor-pointer enabled:hover:bg-subtle disabled:opacity-50"
                      [class.bg-brand-soft]="stars() === n">
                      <span class="w-7 shrink-0 text-ink-soft" dir="ltr">{{ n }} ★</span>
                      <span class="h-2 flex-1 overflow-hidden rounded-full bg-subtle ring-1 ring-line">
                        <span class="block h-full rounded-full bg-accent" [style.width.%]="(c / r.stats.count) * 100"></span>
                      </span>
                      <span class="w-6 shrink-0 text-end tabular-nums text-muted">{{ c }}</span>
                    </button>
                  </li>
                }
              </ul>
              @if (stars()) {
                <button type="button" (click)="filter(null)" class="link mt-3 cursor-pointer text-xs">{{ 'reviews.allStars' | transloco }}</button>
              }
              <p class="mt-4 border-t border-line pt-3 text-xs text-muted">{{ 'reviews.verifiedHint' | transloco }}</p>
            </div>

            <div class="min-w-0">
              <ul class="card divide-y divide-line overflow-hidden">
                @for (review of r.reviews.items; track review.id) {
                  <li class="px-5 py-5 sm:px-6">
                    <div class="flex flex-wrap items-center justify-between gap-2">
                      <div class="flex min-w-0 items-center gap-3">
                        <span class="flex size-9 shrink-0 items-center justify-center rounded-full bg-brand-soft font-display text-sm font-bold text-brand" aria-hidden="true">{{ review.authorName.charAt(0) }}</span>
                        <div class="min-w-0">
                          <p class="truncate text-sm font-semibold text-ink">{{ review.authorName }}</p>
                          <p class="flex flex-wrap gap-x-3 text-xs text-muted">
                            <span>{{ review.createdAt | date: 'longDate' : undefined : language.locale() }}</span>
                            @if (review.editedAt) { <span>{{ 'reviews.edited' | transloco }}</span> }
                          </p>
                        </div>
                      </div>
                      <app-star-rating [value]="review.stars" size="sm" />
                    </div>
                    @if (review.comment) {
                      <p class="mt-3 leading-relaxed break-words whitespace-pre-line text-ink-soft" dir="auto">{{ review.comment }}</p>
                    }
                    @if (review.reply) {
                      <div class="mt-4 rounded-e-lg border-s-2 border-brand bg-brand-soft/60 px-4 py-3 text-sm">
                        <p class="text-xs font-semibold text-brand">{{ 'reviews.storeReply' | transloco }}</p>
                        <p class="mt-1 whitespace-pre-line text-ink-soft" dir="auto">{{ review.reply }}</p>
                      </div>
                    }
                  </li>
                } @empty {
                  <li class="empty-state">{{ 'reviews.noneWithStars' | transloco }}</li>
                }
              </ul>
              <div class="mt-6">
                <app-pagination [page]="r.reviews.page" [pageSize]="r.reviews.pageSize" [total]="r.reviews.total" (pageChange)="page.set($event)" />
              </div>
            </div>
          </div>
        }
      } @else {
        <p class="empty-state">{{ 'common.loading' | transloco }}</p>
      }
    </section>
  `,
})
export class ReviewSection {
  readonly kind = input.required<'product' | 'store'>();
  readonly slug = input.required<string>();
  protected readonly language = inject(LanguageService);
  private readonly api = inject(ReviewApi);

  protected readonly stars = signal<number | null>(null);
  protected readonly page = signal(1);
  private readonly query = computed(() => ({ kind: this.kind(), slug: this.slug(), stars: this.stars() ?? undefined, page: this.page(), pageSize: 5 }));

  protected readonly reviews = rxResource({
    params: () => this.query(),
    stream: ({ params: { kind, slug, ...q } }) => (kind === 'product' ? this.api.productReviews(slug, q) : this.api.storeReviews(slug, q)),
  });

  protected filter(stars: number | null): void {
    this.stars.set(this.stars() === stars ? null : stars);
    this.page.set(1);
  }
}
