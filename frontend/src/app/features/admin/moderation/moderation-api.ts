import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ProductSort } from '../../../core/catalog/catalog-api';
import { ProductStatus } from '../../../core/stores/product-api';
import { StoreCategoryRef, StoreStatus } from '../../../core/stores/store-api';
import { LocalizedName } from '../../../shared/i18n/localized';
import { toParams } from '../admin-api';
import { PagedResult, UserStatus } from '../admin.models';

export const STORE_STATUSES: StoreStatus[] = ['Active', 'Closed', 'Disabled', 'Suspended'];

/** Moderation view of a product; `Live` = what the shop shows. Without a state, deleted ones are left out. */
export type AdminProductState = 'Live' | 'Draft' | 'Archived' | 'Disabled' | 'Deleted';
export const PRODUCT_STATES: AdminProductState[] = ['Live', 'Draft', 'Archived', 'Disabled', 'Deleted'];

export interface AdminStoreQuery {
  search?: string;
  status?: StoreStatus;
  category?: string;
  city?: string;
  /** true = deleted stores only. */
  deleted?: boolean;
  createdFrom?: string;
  createdTo?: string;
  page?: number;
  pageSize?: number;
}

export interface AdminStoreListItem {
  id: string;
  name: string;
  slug: string;
  logoUrl: string | null;
  categoryName: LocalizedName;
  city: string;
  status: StoreStatus;
  isDeleted: boolean;
  moderationReason: string | null;
  ownerId: string;
  ownerName: string;
  ownerEmail: string;
  productCount: number;
  createdAt: string;
}

export interface AdminStore {
  id: string;
  name: string;
  slug: string;
  category: StoreCategoryRef;
  description: string | null;
  phone: string;
  city: string;
  address: string | null;
  logoUrl: string | null;
  bannerUrl: string | null;
  status: StoreStatus;
  isDeleted: boolean;
  moderationReason: string | null;
  disabledAt: string | null;
  deletedAt: string | null;
  defaultAffiliatePercent: number;
  deliveryFee: number;
  deliveryCities: string[];
  deliveryMinDays: number;
  deliveryMaxDays: number;
  facebookUrl: string | null;
  instagramUrl: string | null;
  websiteUrl: string | null;
  subscriptionMonthlyPrice: number;
  createdAt: string;
  updatedAt: string;
  owner: { id: string; fullName: string; email: string; phone: string; status: UserStatus; isStoreOwner: boolean };
  productCount: number;
  publishedCount: number;
  disabledCount: number;
}

export interface AdminProductQuery {
  search?: string;
  storeId?: string;
  category?: string;
  minPrice?: number;
  maxPrice?: number;
  state?: AdminProductState;
  sort?: ProductSort;
  page?: number;
  pageSize?: number;
}

export interface AdminProductListItem {
  id: string;
  slug: string;
  name: string;
  mainImageUrl: string | null;
  price: number;
  minPrice: number;
  imageCount: number;
  status: ProductStatus;
  isDisabled: boolean;
  isDeleted: boolean;
  moderationReason: string | null;
  categoryName: LocalizedName;
  storeId: string;
  storeName: string;
  storeSlug: string;
  createdAt: string;
}

/** Admin moderation of stores and products (010). Disable / delete always take a reason. */
@Injectable({ providedIn: 'root' })
export class ModerationApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/admin`;

  stores(query: AdminStoreQuery): Observable<PagedResult<AdminStoreListItem>> {
    return this.http.get<PagedResult<AdminStoreListItem>>(`${this.base}/stores`, { params: toParams(query) });
  }

  store(id: string): Observable<AdminStore> {
    return this.http.get<AdminStore>(`${this.base}/stores/${id}`);
  }

  disableStore(id: string, reason: string): Observable<AdminStore> {
    return this.http.post<AdminStore>(`${this.base}/stores/${id}/disable`, { reason });
  }

  enableStore(id: string): Observable<AdminStore> {
    return this.http.post<AdminStore>(`${this.base}/stores/${id}/enable`, null);
  }

  deleteStore(id: string, reason: string): Observable<AdminStore> {
    return this.http.post<AdminStore>(`${this.base}/stores/${id}/delete`, { reason });
  }

  products(query: AdminProductQuery): Observable<PagedResult<AdminProductListItem>> {
    return this.http.get<PagedResult<AdminProductListItem>>(`${this.base}/products`, { params: toParams(query) });
  }

  disableProduct(id: string, reason: string): Observable<void> {
    return this.http.post<void>(`${this.base}/products/${id}/disable`, { reason });
  }

  enableProduct(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/products/${id}/enable`, null);
  }

  deleteProduct(id: string, reason: string): Observable<void> {
    return this.http.post<void>(`${this.base}/products/${id}/delete`, { reason });
  }

  disableProducts(ids: string[], reason: string): Observable<{ changed: number }> {
    return this.http.post<{ changed: number }>(`${this.base}/products/disable`, { ids, reason });
  }
}
