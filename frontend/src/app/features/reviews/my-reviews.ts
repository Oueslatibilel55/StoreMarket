import { DatePipe } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { errorKey } from '../../core/http/problem';
import { LanguageService } from '../../core/i18n/language.service';
import { MAX_COMMENT_LENGTH, MyReview, ReviewApi, ReviewToWrite } from '../../core/reviews/review-api';
import { Seo } from '../../core/seo/seo';
import { StarInput } from '../../shared/reviews/star-input';
import { StarRating } from '../../shared/reviews/star-rating';

/** The review being written or edited: a delivered item (new) or one of my reviews (edit). */
interface Draft {
  key: string;
  stars: number;
  comment: string;
}

/**
 * `/reviews` — what was delivered to me and is not rated yet (products and stores), then my reviews: edit within
 * the window, the store's answer, a hidden notice (017).
 */
@Component({
  selector: 'app-my-reviews',
  imports: [DatePipe, FormsModule, RouterLink, TranslocoPipe, StarInput, StarRating],
  template: `
    <main class="page-narrow animate-fade-in">
      <header class="page-header">
        <div class="min-w-0">
          <h1 class="page-title">{{ 'reviews.mine' | transloco }}</h1>
          <p class="page-subtitle">{{ 'reviews.mineHint' | transloco }}</p>
        </div>
      </header>

      @if (data.error()) {
        <p class="alert-danger">{{ 'errors.unknown' | transloco }}</p>
      } @else if (data.value(); as d) {
        <section class="card overflow-hidden">
          <div class="card-header">
            <h2 class="section-title">{{ 'reviews.toWrite' | transloco }}</h2>
            <span class="pill pill-neutral" dir="ltr">{{ d.toWrite.length }}</span>
          </div>
          @if (d.toWrite.length === 0) {
            <p class="empty-state">{{ 'reviews.nothingToWrite' | transloco }}</p>
          } @else {
            <ul class="divide-y divide-line">
              @for (t of d.toWrite; track keyOf(t)) {
                @let key = keyOf(t);
                <li class="px-4 py-4 sm:px-5">
                  <div class="flex flex-wrap items-center gap-3 sm:flex-nowrap">
                    @if (t.target === 'Store') {
                      <!-- A store review: the store's door (arch). -->
                      <div class="arch flex h-14 w-11 shrink-0 items-center justify-center overflow-hidden bg-brand ring-1 ring-line">
                        @if (t.imageUrl) { <img [src]="t.imageUrl" alt="" class="size-full object-cover" /> }
                        @else { <span class="pt-1.5 font-display text-lg font-extrabold text-white" aria-hidden="true">{{ t.storeName.charAt(0).toUpperCase() }}</span> }
                      </div>
                    } @else {
                      <div class="size-14 shrink-0 overflow-hidden rounded-lg border border-line bg-subtle">
                        @if (t.imageUrl) { <img [src]="t.imageUrl" alt="" class="size-full object-cover" /> }
                      </div>
                    }
                    <div class="min-w-0 flex-1">
                      <p class="truncate font-semibold text-ink">
                        {{ t.target === 'Store' ? ('reviews.rateStore' | transloco: { name: t.storeName }) : t.productName }}
                      </p>
                      <p class="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-muted">
                        @if (t.target === 'Product') { <bdi>{{ t.storeName }}</bdi> }
                        <span class="code-chip" dir="ltr">{{ t.orderReference }}</span>
                        <span>{{ 'reviews.deliveredOn' | transloco: { date: (t.deliveredAt | date: 'mediumDate' : undefined : language.locale()) } }}</span>
                      </p>
                    </div>
                    @if (draft()?.key !== key) {
                      <button type="button" (click)="open(key)" class="btn btn-primary btn-sm shrink-0">{{ 'reviews.rate' | transloco }}</button>
                    }
                  </div>
                  @if (draft(); as dr) {
                    @if (dr.key === key) {
                      <form class="mt-4 space-y-3 border-t border-line pt-4 animate-fade-in" (ngSubmit)="create(t)">
                        <app-star-input [name]="'stars-' + key" [value]="dr.stars" (valueChange)="setStars($event)" />
                        <div>
                          <label class="field-label" [for]="'comment-' + key">{{ 'reviews.comment' | transloco }} <span class="text-muted">({{ 'reviews.optional' | transloco }})</span></label>
                          <textarea [id]="'comment-' + key" name="comment" rows="3" class="mt-1.5 field" [maxlength]="maxComment" dir="auto"
                            [placeholder]="(t.target === 'Store' ? 'reviews.storePlaceholder' : 'reviews.productPlaceholder') | transloco"
                            [ngModel]="dr.comment" (ngModelChange)="setComment($event)"></textarea>
                          <p class="field-hint text-end" dir="ltr">{{ dr.comment.length }} / {{ maxComment }}</p>
                        </div>
                        @if (error(); as e) { <p class="alert-danger" role="alert">{{ e | transloco }}</p> }
                        <div class="flex flex-wrap justify-end gap-2">
                          <button type="button" (click)="cancel()" class="btn btn-ghost">{{ 'common.cancel' | transloco }}</button>
                          <button type="submit" [disabled]="!dr.stars || saving()" class="btn btn-primary">{{ (saving() ? 'common.saving' : 'reviews.publish') | transloco }}</button>
                        </div>
                      </form>
                    }
                  }
                </li>
              }
            </ul>
          }
        </section>

        <section class="card mt-8 overflow-hidden">
          <div class="card-header">
            <h2 class="section-title">{{ 'reviews.written' | transloco }}</h2>
            <span class="pill pill-neutral" dir="ltr">{{ d.written.length }}</span>
          </div>
          @if (d.written.length === 0) {
            <p class="empty-state">{{ 'reviews.noneWritten' | transloco }}</p>
          } @else {
            <ul class="divide-y divide-line">
              @for (r of d.written; track r.id) {
                <li class="px-4 py-5 sm:px-5">
                  <div class="flex flex-wrap items-start justify-between gap-2">
                    <div class="min-w-0">
                      <p class="flex flex-wrap items-center gap-2 font-semibold text-ink">
                        @if (r.target === 'Store') {
                          @if (r.storeSlug) { <a [routerLink]="['/stores', r.storeSlug]" class="hover:text-brand">{{ r.storeName }}</a> } @else { <span>{{ r.storeName }}</span> }
                          <span class="pill pill-neutral">{{ 'reviews.target.Store' | transloco }}</span>
                        } @else {
                          @if (r.productSlug) { <a [routerLink]="['/products', r.productSlug]" class="hover:text-brand">{{ r.productName }}</a> } @else { <span>{{ r.productName }}</span> }
                        }
                      </p>
                      <p class="mt-0.5 flex flex-wrap gap-x-3 gap-y-0.5 text-xs text-muted">
                        @if (r.target === 'Product') { <bdi>{{ r.storeName }}</bdi> }
                        <span>{{ r.createdAt | date: 'mediumDate' : undefined : language.locale() }}</span>
                        @if (r.editedAt) { <span>{{ 'reviews.edited' | transloco }}</span> }
                      </p>
                    </div>
                    @if (r.hidden) { <span class="pill pill-danger">{{ 'reviews.hiddenPill' | transloco }}</span> }
                  </div>

                  @if (draft()?.key === r.id) {
                    @let dr = draft()!;
                    <form class="mt-4 space-y-3 animate-fade-in" (ngSubmit)="edit(r)">
                      <app-star-input [name]="'stars-' + r.id" [value]="dr.stars" (valueChange)="setStars($event)" />
                      <textarea name="comment" rows="3" class="field" [maxlength]="maxComment" dir="auto" [attr.aria-label]="'reviews.comment' | transloco"
                        [ngModel]="dr.comment" (ngModelChange)="setComment($event)"></textarea>
                      @if (error(); as e) { <p class="alert-danger" role="alert">{{ e | transloco }}</p> }
                      <div class="flex flex-wrap justify-end gap-2">
                        <button type="button" (click)="cancel()" class="btn btn-ghost">{{ 'common.cancel' | transloco }}</button>
                        <button type="submit" [disabled]="!dr.stars || saving()" class="btn btn-primary">{{ (saving() ? 'common.saving' : 'reviews.save') | transloco }}</button>
                      </div>
                    </form>
                  } @else {
                    <app-star-rating class="mt-2 block" [value]="r.stars" size="sm" />
                    @if (r.comment) { <p class="mt-2 text-sm leading-relaxed whitespace-pre-line text-ink-soft" dir="auto">{{ r.comment }}</p> }
                    @if (r.hidden) {
                      <p class="alert-danger mt-3 text-sm">{{ 'reviews.hiddenNotice' | transloco: { reason: r.hiddenReason } }}</p>
                    }
                    @if (r.reply) {
                      <div class="mt-3 rounded-e-lg border-s-2 border-brand bg-brand-soft/60 px-4 py-3 text-sm">
                        <p class="text-xs font-semibold text-brand">{{ 'reviews.storeReply' | transloco }}</p>
                        <p class="mt-1 whitespace-pre-line text-ink-soft" dir="auto">{{ r.reply }}</p>
                      </div>
                    }
                    <div class="mt-3 flex flex-wrap items-center justify-between gap-2 text-xs text-muted">
                      @if (r.canEdit) {
                        <span>{{ 'reviews.editableUntil' | transloco: { date: (r.editableUntil | date: 'mediumDate' : undefined : language.locale()) } }}</span>
                        <button type="button" (click)="openEdit(r)" class="btn btn-secondary btn-sm">{{ 'reviews.edit' | transloco }}</button>
                      } @else if (!r.hidden) {
                        <span>{{ 'reviews.editClosed' | transloco }}</span>
                      }
                    </div>
                  }
                </li>
              }
            </ul>
          }
        </section>
      } @else {
        <p class="card empty-state">{{ 'common.loading' | transloco }}</p>
      }
    </main>
  `,
})
export class MyReviews {
  private readonly api = inject(ReviewApi);
  protected readonly language = inject(LanguageService);
  protected readonly maxComment = MAX_COMMENT_LENGTH;

  protected readonly data = rxResource({ stream: () => this.api.mine() });
  protected readonly draft = signal<Draft | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    const seo = inject(Seo);
    effect(() => {
      this.language.lang();
      seo.setKey('reviews.mine');
    });
  }

  protected keyOf(t: ReviewToWrite): string {
    return t.orderLineId ?? `store-${t.storeOrderId}`;
  }

  protected open(key: string): void {
    this.error.set(null);
    this.draft.set({ key, stars: 0, comment: '' });
  }

  protected openEdit(r: MyReview): void {
    this.error.set(null);
    this.draft.set({ key: r.id, stars: r.stars, comment: r.comment ?? '' });
  }

  protected cancel(): void {
    this.draft.set(null);
  }

  protected setStars(stars: number): void {
    this.draft.update((d) => (d ? { ...d, stars } : d));
  }

  protected setComment(comment: string): void {
    this.draft.update((d) => (d ? { ...d, comment } : d));
  }

  protected create(t: ReviewToWrite): void {
    const d = this.draft();
    if (!d?.stars) return;
    this.save(this.api.create({ storeOrderId: t.storeOrderId, orderLineId: t.orderLineId, stars: d.stars, comment: d.comment.trim() || null }));
  }

  protected edit(r: MyReview): void {
    const d = this.draft();
    if (!d?.stars) return;
    this.save(this.api.edit(r.id, { stars: d.stars, comment: d.comment.trim() || null }));
  }

  private save(request: ReturnType<ReviewApi['edit']>): void {
    this.saving.set(true);
    this.error.set(null);
    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.draft.set(null);
        this.data.reload();
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.error.set(errorKey(err));
      },
    });
  }
}
