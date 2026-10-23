import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { toParams } from '../coins/coin-api';
import { Paged } from '../catalog/catalog-api';

/** What a review rates (017): a delivered product (order line) or the store's service (store order). */
export type ReviewTarget = 'Product' | 'Store';

export const MAX_COMMENT_LENGTH = 1000;
export const MAX_REPLY_LENGTH = 1000;
export const MAX_REASON_LENGTH = 500;

/** Average, count and how many reviews gave 1★ … 5★ (`stars[0]` = 1★). */
export interface ReviewStats {
  average: number | null;
  count: number;
  stars: number[];
}

/** First name and initial only. */
export interface PublicReview {
  id: string;
  authorName: string;
  stars: number;
  comment: string | null;
  createdAt: string;
  editedAt: string | null;
  reply: string | null;
  repliedAt: string | null;
}

export interface PublicReviews {
  stats: ReviewStats;
  reviews: Paged<PublicReview>;
}

export interface ReviewToWrite {
  target: ReviewTarget;
  orderReference: string;
  storeOrderId: string;
  orderLineId: string | null;
  storeName: string;
  storeSlug: string | null;
  productName: string | null;
  productSlug: string | null;
  imageUrl: string | null;
  deliveredAt: string;
}

export interface MyReview {
  id: string;
  target: ReviewTarget;
  orderReference: string;
  storeOrderId: string;
  orderLineId: string | null;
  storeName: string;
  storeSlug: string | null;
  productName: string | null;
  productSlug: string | null;
  imageUrl: string | null;
  stars: number;
  comment: string | null;
  createdAt: string;
  editedAt: string | null;
  editableUntil: string;
  canEdit: boolean;
  reply: string | null;
  repliedAt: string | null;
  hidden: boolean;
  hiddenReason: string | null;
}

export interface MyReviews {
  toWrite: ReviewToWrite[];
  written: MyReview[];
}

export interface StoreReview {
  id: string;
  target: ReviewTarget;
  authorName: string;
  orderReference: string;
  storeOrderId: string;
  productName: string | null;
  productSlug: string | null;
  stars: number;
  comment: string | null;
  createdAt: string;
  editedAt: string | null;
  reply: string | null;
  repliedAt: string | null;
  hidden: boolean;
  hiddenReason: string | null;
}

export interface StoreReviews {
  store: ReviewStats;
  products: ReviewStats;
  unanswered: number;
  reviews: Paged<StoreReview>;
}

export interface StoreReviewQuery {
  target?: ReviewTarget;
  stars?: number;
  replied?: boolean;
  page?: number;
  pageSize?: number;
}

export interface AdminReview {
  id: string;
  target: ReviewTarget;
  authorId: string;
  authorFullName: string;
  authorEmail: string;
  storeId: string;
  storeName: string;
  storeSlug: string;
  productId: string | null;
  productName: string | null;
  productSlug: string | null;
  orderReference: string;
  stars: number;
  comment: string | null;
  createdAt: string;
  editedAt: string | null;
  reply: string | null;
  repliedAt: string | null;
  hiddenAt: string | null;
  hiddenReason: string | null;
}

export interface AdminReviewQuery {
  target?: ReviewTarget;
  hidden?: boolean;
  stars?: number;
  storeId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface AdminReviewCounts {
  total: number;
  hidden: number;
  /** Visible 1–2★ reviews: where abuse usually is. */
  lowStars: number;
}

/** Ratings and reviews (017): public lists, the client's reviews, the store owner's answers, admin moderation. */
@Injectable({ providedIn: 'root' })
export class ReviewApi {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  productReviews(slug: string, query: { stars?: number; page?: number; pageSize?: number } = {}): Observable<PublicReviews> {
    return this.http.get<PublicReviews>(`${this.base}/products/${slug}/reviews`, { params: toParams(query) });
  }

  storeReviews(slug: string, query: { stars?: number; page?: number; pageSize?: number } = {}): Observable<PublicReviews> {
    return this.http.get<PublicReviews>(`${this.base}/stores/${slug}/reviews`, { params: toParams(query) });
  }

  mine(): Observable<MyReviews> {
    return this.http.get<MyReviews>(`${this.base}/me/reviews`);
  }

  /** With `orderLineId`: rates that product; without: rates the store. */
  create(body: { storeOrderId: string; orderLineId: string | null; stars: number; comment: string | null }): Observable<MyReview> {
    return this.http.post<MyReview>(`${this.base}/me/reviews`, body);
  }

  edit(id: string, body: { stars: number; comment: string | null }): Observable<MyReview> {
    return this.http.put<MyReview>(`${this.base}/me/reviews/${id}`, body);
  }

  forMyStore(query: StoreReviewQuery): Observable<StoreReviews> {
    return this.http.get<StoreReviews>(`${this.base}/me/store/reviews`, { params: toParams(query) });
  }

  reply(id: string, reply: string): Observable<StoreReview> {
    return this.http.put<StoreReview>(`${this.base}/me/store/reviews/${id}/reply`, { reply });
  }

  removeReply(id: string): Observable<StoreReview> {
    return this.http.delete<StoreReview>(`${this.base}/me/store/reviews/${id}/reply`);
  }

  adminList(query: AdminReviewQuery): Observable<Paged<AdminReview>> {
    return this.http.get<Paged<AdminReview>>(`${this.base}/admin/reviews`, { params: toParams(query) });
  }

  adminCounts(): Observable<AdminReviewCounts> {
    return this.http.get<AdminReviewCounts>(`${this.base}/admin/reviews/counts`);
  }

  hide(id: string, reason: string): Observable<AdminReview> {
    return this.http.post<AdminReview>(`${this.base}/admin/reviews/${id}/hide`, { reason });
  }

  unhide(id: string, reason: string): Observable<AdminReview> {
    return this.http.post<AdminReview>(`${this.base}/admin/reviews/${id}/unhide`, { reason });
  }
}
