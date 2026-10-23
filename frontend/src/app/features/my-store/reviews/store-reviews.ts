import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, numberAttribute, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { MAX_REPLY_LENGTH, ReviewApi, ReviewTarget, StoreReview } from '../../../core/reviews/review-api';
import { StarRating } from '../../../shared/reviews/star-rating';
import { Pagination } from '../../../shared/ui/pagination';
import { MyStoreTabs } from '../my-store-tabs';

const PAGE_SIZE = 20;

/**
 * `/my-store/reviews?target=&stars=&replied=&page=` — what clients say about my products and my service (017), with a
 * public answer per review. Reviews hidden by an admin stay listed, flagged, with the reason.
 */
@Component({
  selector: 'app-store-reviews',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe, MyStoreTabs, Pagination, StarRating],
  template: `
    <main class="page max-w-4xl animate-fade-in">
      <app-my-store-tabs />
      <h1 class="page-title">{{ 'reviews.store.title' | transloco }}</h1>
      <p class="page-subtitle">{{ 'reviews.store.subtitle' | transloco }}</p>

      @if (data.error(); as err) {
        <p class="alert-danger mt-6">{{ errorOf(err) | transloco }}</p>
      } @else if (data.value(); as d) {
        <div class="mt-6 grid gap-3 sm:grid-cols-3">
          <div class="card p-4">
            <p class="eyebrow">{{ 'reviews.store.serviceRating' | transloco }}</p>
            @if (d.store.count) {
              <p class="mt-1 flex items-center gap-2">
                <span class="text-2xl font-bold text-ink" dir="ltr">{{ d.store.average | number: '1.1-1' : language.locale() }}</span>
                <app-star-rating [value]="d.store.average" [count]="d.store.count" size="sm" />
              </p>
            } @else { <p class="mt-1 text-sm text-muted">{{ 'reviews.none' | transloco }}</p> }
          </div>
          <div class="card p-4">
            <p class="eyebrow">{{ 'reviews.store.productsRating' | transloco }}</p>
            @if (d.products.count) {
              <p class="mt-1 flex items-center gap-2">
                <span class="text-2xl font-bold text-ink" dir="ltr">{{ d.products.average | number: '1.1-1' : language.locale() }}</span>
                <app-star-rating [value]="d.products.average" [count]="d.products.count" size="sm" />
              </p>
            } @else { <p class="mt-1 text-sm text-muted">{{ 'reviews.none' | transloco }}</p> }
          </div>
          <button type="button" (click)="apply({ replied: 'false', page: null })" class="card cursor-pointer p-4 text-start transition-shadow hover:shadow-card-hover">
            <p class="eyebrow">{{ 'reviews.store.unanswered' | transloco }}</p>
            <p class="mt-1 text-2xl font-bold" [class]="d.unanswered ? 'text-accent-ink' : 'text-ink'">{{ d.unanswered }}</p>
          </button>
        </div>

        <div class="card mt-6 overflow-hidden">
          <div class="flex flex-wrap gap-3 border-b border-line p-4">
            <select [value]="target() ?? ''" (change)="apply({ target: $any($event.target).value || null, page: null })"
              [attr.aria-label]="'reviews.admin.target' | transloco" class="field w-auto">
              <option value="">{{ 'reviews.admin.allTargets' | transloco }}</option>
              @for (t of targets; track t) { <option [value]="t">{{ 'reviews.target.' + t | transloco }}</option> }
            </select>
            <select [value]="stars() ?? ''" (change)="apply({ stars: $any($event.target).value || null, page: null })"
              [attr.aria-label]="'reviews.admin.stars' | transloco" class="field w-auto">
              <option value="">{{ 'reviews.admin.allStars' | transloco }}</option>
              @for (n of [5, 4, 3, 2, 1]; track n) { <option [value]="n">{{ 'reviews.nStars' | transloco: { n } }}</option> }
            </select>
            <select [value]="replied() ?? ''" (change)="apply({ replied: $any($event.target).value || null, page: null })"
              [attr.aria-label]="'reviews.store.answered' | transloco" class="field w-auto">
              <option value="">{{ 'reviews.store.allAnswers' | transloco }}</option>
              <option value="false">{{ 'reviews.store.toAnswer' | transloco }}</option>
              <option value="true">{{ 'reviews.store.answered' | transloco }}</option>
            </select>
            @if (hasFilters()) {
              <button type="button" (click)="clear()" class="btn btn-ghost">{{ 'common.clearFilters' | transloco }}</button>
            }
          </div>

          @if (d.reviews.items.length === 0) {
            <div class="empty-state">
              <span class="text-3xl" aria-hidden="true">⭐</span>
              <p>{{ (hasFilters() ? 'reviews.noneWithStars' : 'reviews.store.empty') | transloco }}</p>
            </div>
          } @else {
            <ul class="divide-y divide-line">
              @for (r of d.reviews.items; track r.id) {
                <li class="px-5 py-4">
                  <div class="flex flex-wrap items-center gap-2">
                    <app-star-rating [value]="r.stars" size="sm" />
                    <span class="pill pill-neutral">{{ 'reviews.target.' + r.target | transloco }}</span>
                    @if (r.hidden) { <span class="pill pill-danger">{{ 'reviews.hiddenPill' | transloco }}</span> }
                    <span class="ms-auto text-xs text-muted">{{ r.createdAt | date: 'mediumDate' : undefined : language.locale() }}</span>
                  </div>
                  <p class="mt-1.5 text-sm text-muted">
                    <bdi class="font-medium text-ink">{{ r.authorName }}</bdi>
                    @if (r.productName) {
                      ·
                      @if (r.productSlug) { <a [routerLink]="['/products', r.productSlug]" class="hover:text-brand"><bdi>{{ r.productName }}</bdi></a> } @else { <bdi>{{ r.productName }}</bdi> }
                    }
                    · <a [routerLink]="['/my-store/orders', r.storeOrderId]" class="hover:text-brand" dir="ltr">{{ r.orderReference }}</a>
                  </p>
                  @if (r.comment) { <p class="mt-2 text-sm whitespace-pre-line text-ink-soft" dir="auto">{{ r.comment }}</p> }
                  @if (r.hidden) {
                    <p class="alert-danger mt-3 text-sm">{{ 'reviews.store.hiddenNotice' | transloco: { reason: r.hiddenReason } }}</p>
                  } @else if (editing() === r.id) {
                    <div class="mt-3 space-y-2 animate-fade-in">
                      <label class="field-label" [for]="'reply-' + r.id">{{ 'reviews.store.yourReply' | transloco }}</label>
                      <textarea [id]="'reply-' + r.id" rows="3" class="field" [attr.maxlength]="maxReply" dir="auto" [value]="reply()"
                        (input)="reply.set($any($event.target).value)" [placeholder]="'reviews.store.replyPlaceholder' | transloco"></textarea>
                      <p class="field-hint">{{ 'reviews.store.replyHint' | transloco }}</p>
                      @if (error(); as e) { <p class="alert-danger" role="alert">{{ e | transloco }}</p> }
                      <div class="flex flex-wrap justify-end gap-2">
                        <button type="button" (click)="editing.set(null)" class="btn btn-ghost">{{ 'common.cancel' | transloco }}</button>
                        <button type="button" (click)="save(r)" [disabled]="busy() || !reply().trim()" class="btn btn-primary">
                          {{ (busy() ? 'common.saving' : 'reviews.store.publishReply') | transloco }}
                        </button>
                      </div>
                    </div>
                  } @else if (r.reply) {
                    <div class="mt-3 rounded-lg border-s-2 border-brand bg-subtle px-4 py-3 text-sm">
                      <p class="text-xs font-semibold text-ink">{{ 'reviews.store.yourReply' | transloco }}</p>
                      <p class="mt-1 whitespace-pre-line text-ink-soft" dir="auto">{{ r.reply }}</p>
                      <div class="mt-2 flex gap-2">
                        <button type="button" (click)="edit(r)" class="btn btn-ghost btn-sm">{{ 'reviews.edit' | transloco }}</button>
                        <button type="button" (click)="remove(r)" [disabled]="busy()" class="btn btn-ghost btn-sm text-danger">{{ 'reviews.store.removeReply' | transloco }}</button>
                      </div>
                    </div>
                  } @else {
                    <button type="button" (click)="edit(r)" class="btn btn-secondary btn-sm mt-3">{{ 'reviews.store.reply' | transloco }}</button>
                  }
                </li>
              }
            </ul>
            <div class="border-t border-line bg-subtle/40 px-5 py-3">
              <app-pagination [page]="d.reviews.page" [pageSize]="pageSize" [total]="d.reviews.total" (pageChange)="apply({ page: $event })" />
            </div>
          }
        </div>
      } @else {
        <p class="card empty-state mt-6">{{ 'common.loading' | transloco }}</p>
      }
    </main>
  `,
})
export class StoreReviewsPage {
  private readonly api = inject(ReviewApi);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly target = input<ReviewTarget | undefined>();
  readonly stars = input<string | undefined>();
  readonly replied = input<'true' | 'false' | undefined>();
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly targets: ReviewTarget[] = ['Product', 'Store'];
  protected readonly pageSize = PAGE_SIZE;
  protected readonly maxReply = MAX_REPLY_LENGTH;
  protected readonly hasFilters = computed(() => !!this.target() || !!this.stars() || !!this.replied());

  protected readonly data = rxResource({
    params: () => ({
      target: this.target(),
      stars: this.stars() ? Number(this.stars()) : undefined,
      replied: this.replied() === undefined ? undefined : this.replied() === 'true',
      page: this.page(),
      pageSize: PAGE_SIZE,
    }),
    stream: ({ params }) => this.api.forMyStore(params),
  });

  protected readonly editing = signal<string | null>(null);
  protected readonly reply = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly errorOf = errorKey;

  protected apply(params: Record<string, string | number | null>): void {
    this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge' });
  }

  protected clear(): void {
    this.router.navigate([], { queryParams: {} });
  }

  protected edit(r: StoreReview): void {
    this.error.set(null);
    this.reply.set(r.reply ?? '');
    this.editing.set(r.id);
  }

  protected save(r: StoreReview): void {
    const text = this.reply().trim();
    if (!text) return;
    this.run(this.api.reply(r.id, text));
  }

  protected remove(r: StoreReview): void {
    this.run(this.api.removeReply(r.id));
  }

  private run(request: ReturnType<ReviewApi['reply']>): void {
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: () => {
        this.busy.set(false);
        this.editing.set(null);
        this.data.reload();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(errorKey(err));
      },
    });
  }
}
